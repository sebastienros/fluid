using Fluid.Accessors;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Fluid
{
    public class DefaultMemberAccessStrategy : MemberAccessStrategy
    {
        internal record struct AccessorKey(Type Type, string Name);

        private static readonly bool _dynamicCodeSupported = IsDynamicCodeSupported();

        private volatile Dictionary<AccessorKey, IMemberAccessor> _map = [];

        // A standalone token rather than the map itself. Using the map would mean every cached accessor
        // pinned the superseded copy it was resolved against, and would churn on every cold resolution,
        // because GetAccessor registers what it resolves. Volatile so reading it is an acquire, keeping
        // the map read that follows from being reordered before it.
        private volatile object _accessorCacheToken = new();

        // Only the exact type opts in. A derived strategy may override GetAccessor to resolve from its
        // own source, which this map -- and therefore the token -- would not reflect; it would then serve
        // a stale accessor forever. Subclasses give up the caching, not correctness.
        private readonly bool _accessorCachingSupported;

        public DefaultMemberAccessStrategy()
        {
            _accessorCachingSupported = GetType() == typeof(DefaultMemberAccessStrategy);
        }

        protected internal override object AccessorCacheToken => _accessorCachingSupported ? _accessorCacheToken : null;

        public override IMemberAccessor GetAccessor(Type type, string name, StringComparer stringComparer)
        {
            if (!TryGetAccessor(type, name, stringComparer, out var accessor))
            {
                // Memoize what was resolved, but without invalidating cached accessors: this only fills
                // in a pair that had no entry, so it cannot change what any other pair resolves to.
                AddToMap(type, name, accessor = GetMemberAccessor(type, name, stringComparer) ?? GetAccessorUnlikely(type, name, stringComparer));
            }

            return accessor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetAccessor(Type type, string name, StringComparer stringComparer, out IMemberAccessor accessor)
        {
            // Search for a specific accessor first, or a wildcard accessor.
            // A wildcard accessor is only used when an accessor is provided by users.
            return _map.TryGetValue(new AccessorKey(type, name), out accessor) || _map.TryGetValue(new AccessorKey(type, "*"), out accessor);
        }

        private static IMemberAccessor GetMemberAccessor(Type type, string name, StringComparer stringComparer)
        {
            foreach (var propertyInfo in type.GetTypeInfo().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (propertyInfo.GetIndexParameters().Length > 0)
                {
                    // Indexed property...
                    continue;
                }

                if (propertyInfo.GetGetMethod() == null)
                {
                    // Write-only property...
                    continue;
                }

                // Use the comparer to match a property name
                if (!stringComparer.Equals(propertyInfo.Name, name))
                {
                    continue;
                }

                if (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    return new AsyncDelegateAccessor(async (o, n) =>
                    {
                        var asyncValue = (Task)propertyInfo.GetValue(o);
                        await asyncValue.ConfigureAwait(false);
                        return ((dynamic)asyncValue).Result;
                    });
                }
                else if (propertyInfo.GetGetMethod().IsStatic)
                {
                    // For static properties, use DelegateAccessor that ignores the instance
                    return new DelegateAccessor((o, n) => propertyInfo.GetValue(null));
                }
                else
                {
                    return _dynamicCodeSupported
                        ? new PropertyInfoAccessor(propertyInfo)
                        : new ReflectionPropertyInfoAccessor(propertyInfo);
                }
            }

            foreach (var fieldInfo in type.GetTypeInfo().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                // Use the comparer to match a field name
                if (!stringComparer.Equals(fieldInfo.Name, name))
                {
                    continue;
                }

                if (fieldInfo.FieldType.IsGenericType && fieldInfo.FieldType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    return new AsyncDelegateAccessor(async (o, n) =>
                    {
                        var asyncValue = (Task)fieldInfo.GetValue(o);
                        await asyncValue.ConfigureAwait(false);
                        return ((dynamic)asyncValue).Result;
                    });
                }
                else if (fieldInfo.IsStatic)
                {
                    // For static fields, use DelegateAccessor that ignores the instance
                    return new DelegateAccessor((o, n) => fieldInfo.GetValue(null));
                }
                else
                {
                    return _dynamicCodeSupported
                        ? new FieldInfoAccessor(fieldInfo)
                        : new ReflectionFieldInfoAccessor(fieldInfo);
                }
            }            

            return null;
        }

        private static bool IsDynamicCodeSupported()
        {
#if NETSTANDARD2_0
            var runtimeFeatureType = Type.GetType("System.Runtime.CompilerServices.RuntimeFeature, System.Runtime");
            var property = runtimeFeatureType?.GetProperty("IsDynamicCodeSupported", BindingFlags.Public | BindingFlags.Static);

            if (property?.PropertyType == typeof(bool))
            {
                return (bool)property.GetValue(null);
            }

            return true;
#else
            return RuntimeFeature.IsDynamicCodeSupported;
#endif
        }

        // Creates accessors based on base types and interfaces
        private IMemberAccessor GetAccessorUnlikely(Type type, string name, StringComparer stringComparer)
        {
            var currentType = type.GetTypeInfo().BaseType;
            while (currentType != typeof(object) && currentType != null)
            {
                // Look for specific property map
                if (TryGetAccessor(currentType, name, stringComparer, out var accessor))
                {
                    return accessor;
                }

                currentType = currentType.GetTypeInfo().BaseType;
            }

            // Search for accessors defined on interfaces
            foreach (var interfaceType in type.GetTypeInfo().GetInterfaces())
            {
                // NB: Here we could also register this accessor in typeMap[type] such that
                // next lookup on this type won't need to resolve its interfaces
                if (TryGetAccessor(interfaceType, name, stringComparer, out var accessor))
                {
                    return accessor;
                }
            }

            return null;
        }

        public override void Register(Type type, string name, IMemberAccessor accessor)
        {
            AddToMap(type, name, accessor);

            // A registration can replace what an already-resolved pair maps to, so retire the token and
            // make every call site re-resolve.
            _accessorCacheToken = new object();
        }

        private void AddToMap(Type type, string name, IMemberAccessor accessor)
        {
            var map = new Dictionary<AccessorKey, IMemberAccessor>(_map);
            map[new AccessorKey(type, name)] = accessor;
            _map = map;
        }
    }
}

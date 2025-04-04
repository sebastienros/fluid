using Fluid.Accessors;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Fluid
{
    public record struct AccessorKey(Type Type, string Name);

    public class DefaultMemberAccessStrategy : MemberAccessStrategy
    {
        private Dictionary<AccessorKey, IMemberAccessor> _map = [];

        public override IMemberAccessor GetAccessor(Type type, string name, StringComparer stringComparer)
        {
            if (!TryGetAccessor(type, name, stringComparer, out var accessor))
            {
                Register(type, name, accessor = GetMemberAccessor(type, name, stringComparer) ?? GetAccessorUnlikely(type, name, stringComparer));
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
            foreach (var propertyInfo in type.GetTypeInfo().GetProperties(BindingFlags.Public | BindingFlags.Instance))
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
                else
                {
                    return new PropertyInfoAccessor(propertyInfo);
                }
            }

            foreach (var fieldInfo in type.GetTypeInfo().GetFields(BindingFlags.Public | BindingFlags.Instance))
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
                else
                {
                    return new FieldInfoAccessor(fieldInfo);
                }
            }            

            return null;
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
            var map = new Dictionary<AccessorKey, IMemberAccessor>(_map);
            map[new AccessorKey(type, name)] = accessor;
            _map = map;
        }
    }
}

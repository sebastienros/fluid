using System.ComponentModel;
using Fluid.Accessors;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Fluid
{
    public class DefaultMemberAccessStrategy : MemberAccessStrategy
    {
        internal record struct AccessorKey(Type Type, string Name);
        internal record struct ReflectedAccessorKey(Type Type, string Name, StringComparer StringComparer);

        private sealed class ReflectionCache
        {
            public ReflectionCache(object registrationToken)
            {
                RegistrationToken = registrationToken;
            }

            public object RegistrationToken { get; }
            public volatile Dictionary<ReflectedAccessorKey, MemberAccessor> Accessors = [];
        }

        private sealed class AccessorCacheState
        {
            public AccessorCacheState(object registrations, object generatedRegistrations)
            {
                Registrations = registrations;
                GeneratedRegistrations = generatedRegistrations;
            }

            public object Registrations { get; }
            public object GeneratedRegistrations { get; }
        }

        private static readonly bool _dynamicCodeSupported = IsDynamicCodeSupported();

        private volatile Dictionary<AccessorKey, MemberAccessor> _registrations = [];
        private volatile Dictionary<Type, GeneratedMemberAccessorRegistration[]> _generatedRegistrations = [];
        private volatile object _generatedRegistryToken = GeneratedMemberAccessorRegistry.CacheToken;
        private volatile Type _lastGeneratedType;
        private volatile ReflectionCache _reflectionCache;
        private volatile AccessorCacheState _accessorCacheState;

        // Only the exact type opts in. A derived strategy may override GetAccessor to resolve from its
        // own source, which these maps -- and therefore the token -- would not reflect; it would then serve
        // a stale accessor forever. Subclasses give up the caching, not correctness.
        private readonly bool _accessorCachingSupported;

        public DefaultMemberAccessStrategy()
        {
            _accessorCachingSupported = GetType() == typeof(DefaultMemberAccessStrategy);
            _accessorCacheState = new AccessorCacheState(_registrations, _generatedRegistrations);
            _reflectionCache = new ReflectionCache(_accessorCacheState);
        }

        protected internal override object AccessorCacheToken
            => _accessorCachingSupported ? GetAccessorCacheState(_registrations, _generatedRegistrations) : null;

        /// <summary>
        /// Registers an accessor emitted by the Fluid source generator.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterSourceGeneratedAccessor(Type type, MemberAccessor accessor, params string[] memberNames)
            => GeneratedMemberAccessorRegistry.Register(type, accessor, memberNames);

        public override MemberAccessor GetAccessor(Type type, string name, StringComparer stringComparer)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(stringComparer);

            var registrations = _registrations;
            var generatedRegistrations = _generatedRegistrations;

            if (TryGetRegisteredAccessor(registrations, type, name, out var accessor))
            {
                return accessor;
            }

            if (generatedRegistrations.TryGetValue(type, out var generatedAccessors))
            {
                foreach (var generatedAccessor in generatedAccessors)
                {
                    if (generatedAccessor.CanAccess(name, stringComparer))
                    {
                        return generatedAccessor.Accessor;
                    }
                }
            }

            var reflectionCache = GetReflectionCache(GetAccessorCacheState(registrations, generatedRegistrations));
            var key = new ReflectedAccessorKey(type, name, stringComparer);
            var reflectedAccessors = reflectionCache.Accessors;

            if (reflectedAccessors.TryGetValue(key, out accessor))
            {
                return accessor;
            }

            accessor = GetMemberAccessor(type, name, stringComparer)
                ?? GetAccessorUnlikely(registrations, type, name, stringComparer);

            AddReflectedAccessor(reflectionCache, key, accessor);
            return accessor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetRegisteredAccessor(
            Dictionary<AccessorKey, MemberAccessor> registrations,
            Type type,
            string name,
            out MemberAccessor accessor)
        {
            return registrations.TryGetValue(new AccessorKey(type, name), out accessor)
                || registrations.TryGetValue(new AccessorKey(type, "*"), out accessor);
        }

        private static MemberAccessor GetMemberAccessor(Type type, string name, StringComparer stringComparer)
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
        private static MemberAccessor GetAccessorUnlikely(
            Dictionary<AccessorKey, MemberAccessor> registrations,
            Type type,
            string name,
            StringComparer stringComparer)
        {
            var currentType = type.GetTypeInfo().BaseType;
            while (currentType != typeof(object) && currentType != null)
            {
                if (TryGetRegisteredAccessor(registrations, currentType, name, out var accessor))
                {
                    return accessor;
                }

                accessor = GetMemberAccessor(currentType, name, stringComparer);
                if (accessor != null)
                {
                    return accessor;
                }

                currentType = currentType.GetTypeInfo().BaseType;
            }

            // Search for accessors defined on interfaces
            foreach (var interfaceType in type.GetTypeInfo().GetInterfaces())
            {
                if (TryGetRegisteredAccessor(registrations, interfaceType, name, out var accessor))
                {
                    return accessor;
                }

                accessor = GetMemberAccessor(interfaceType, name, stringComparer);
                if (accessor != null)
                {
                    return accessor;
                }
            }

            return null;
        }

        public override void Register(Type type, string name, MemberAccessor accessor)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(name);

            while (true)
            {
                var registrations = _registrations;
                var updated = new Dictionary<AccessorKey, MemberAccessor>(registrations)
                {
                    [new AccessorKey(type, name)] = accessor
                };

                if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _registrations, updated, registrations),
                    registrations))
                {
                    _reflectionCache = new ReflectionCache(GetAccessorCacheState(updated, _generatedRegistrations));
                    return;
                }
            }
        }

        internal override void RegisterGeneratedAccessor(Type type)
        {
            while (true)
            {
                var registryToken = GeneratedMemberAccessorRegistry.CacheToken;
                var generatedRegistrations = _generatedRegistrations;

                if (ReferenceEquals(_generatedRegistryToken, registryToken))
                {
                    if (ReferenceEquals(_lastGeneratedType, type))
                    {
                        return;
                    }

                    if (generatedRegistrations.ContainsKey(type))
                    {
                        _lastGeneratedType = type;
                        return;
                    }
                }

                var accessors = GeneratedMemberAccessorRegistry.GetAccessors(type, out registryToken);
                if (accessors is null)
                {
                    _generatedRegistryToken = registryToken;
                    return;
                }

                if (generatedRegistrations.TryGetValue(type, out var registeredAccessors) &&
                    ReferenceEquals(registeredAccessors, accessors))
                {
                    _generatedRegistryToken = registryToken;
                    _lastGeneratedType = type;
                    return;
                }

                var updated = new Dictionary<Type, GeneratedMemberAccessorRegistration[]>(generatedRegistrations)
                {
                    [type] = accessors
                };

                if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _generatedRegistrations, updated, generatedRegistrations),
                    generatedRegistrations))
                {
                    _generatedRegistryToken = registryToken;
                    _lastGeneratedType = type;
                    _reflectionCache = new ReflectionCache(GetAccessorCacheState(_registrations, updated));
                    return;
                }
            }
        }

        private ReflectionCache GetReflectionCache(object registrationToken)
        {
            while (true)
            {
                var reflectionCache = _reflectionCache;
                if (ReferenceEquals(reflectionCache.RegistrationToken, registrationToken))
                {
                    return reflectionCache;
                }

                var updated = new ReflectionCache(registrationToken);
                if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _reflectionCache, updated, reflectionCache),
                    reflectionCache))
                {
                    return updated;
                }
            }
        }

        private AccessorCacheState GetAccessorCacheState(object registrations, object generatedRegistrations)
        {
            while (true)
            {
                var state = _accessorCacheState;

                if (ReferenceEquals(state.Registrations, registrations) &&
                    ReferenceEquals(state.GeneratedRegistrations, generatedRegistrations))
                {
                    return state;
                }

                var updated = new AccessorCacheState(registrations, generatedRegistrations);
                if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _accessorCacheState, updated, state),
                    state))
                {
                    return updated;
                }
            }
        }

        private static void AddReflectedAccessor(
            ReflectionCache reflectionCache,
            ReflectedAccessorKey key,
            MemberAccessor accessor)
        {
            while (true)
            {
                var reflectedAccessors = reflectionCache.Accessors;

                if (reflectedAccessors.ContainsKey(key))
                {
                    return;
                }

                var updated = new Dictionary<ReflectedAccessorKey, MemberAccessor>(reflectedAccessors)
                {
                    [key] = accessor
                };

                if (ReferenceEquals(
                    Interlocked.CompareExchange(ref reflectionCache.Accessors, updated, reflectedAccessors),
                    reflectedAccessors))
                {
                    return;
                }
            }
        }
    }
}

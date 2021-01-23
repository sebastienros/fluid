using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Fluid
{
    public class ConcurrentMemberAccessStrategy : IMemberAccessStrategy
    {
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, IMemberAccessor>> _map;

        public ConcurrentMemberAccessStrategy()
        {
            _map = new ConcurrentDictionary<Type, ConcurrentDictionary<string, IMemberAccessor>>();
        }

        public MemberNameStrategy MemberNameStrategy { get; set; } = MemberNameStrategies.Default;

        public IMemberAccessor GetAccessor(Type type, string name)
        {
            var currentType = type;

            // Look for specific property map
            if (TryGetAccessor(currentType, name, out var accessor))
            {
                return accessor;
            }

            return GetAccessorUnlikely(type.GetTypeInfo().BaseType, name);
        }

        private IMemberAccessor GetAccessorUnlikely(Type type, string name)
        {
            var currentType = type;
            while (currentType != typeof(object) && currentType != null)
            {
                // Look for specific property map
                if (TryGetAccessor(currentType, name, out var accessor))
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
                if (TryGetAccessor(interfaceType, name, out var accessor))
                {
                    return accessor;
                }
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetAccessor(Type type, string name, out IMemberAccessor accessor)
        {
            if (_map.TryGetValue(type, out var typeMap))
            {
                if (typeMap.TryGetValue(name, out accessor) || typeMap.TryGetValue("*", out accessor))
                {
                    return true;
                }
            }

            accessor = null;
            return false;
        }

        public bool IgnoreCasing { get; set; }

        public void Register(Type type, string name, IMemberAccessor getter)
        {
#if NETSTANDARD2_0
            var typeMap = _map.GetOrAdd(
                type,
                _
                    => new ConcurrentDictionary<string, IMemberAccessor>(IgnoreCasing ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal));
#else
            // we can give state and remove closure allocation
            var typeMap = _map.GetOrAdd(
                type,
                (_, ignoreCasing)
                    => new ConcurrentDictionary<string, IMemberAccessor>(ignoreCasing ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal),
                IgnoreCasing);
#endif

            typeMap[name] = getter;
        }
    }
}

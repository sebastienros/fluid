using System;
using System.Collections.Concurrent;
using System.Reflection;

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
            IMemberAccessor accessor = null;

            var currentType = type;

            while (currentType != typeof(object) && currentType != null)
            {
                // Look for specific property map
                if (_map.TryGetValue(currentType, out var typeMap))
                {
                    if (typeMap.TryGetValue(name, out accessor) || typeMap.TryGetValue("*", out accessor))
                    {
                        return accessor;
                    }
                }

                currentType = currentType.GetTypeInfo().BaseType;
            }

            // Search for accessors defined on interfaces
            foreach (var interfaceType in type.GetTypeInfo().GetInterfaces())
            {
                if (_map.TryGetValue(interfaceType, out var typeMap))
                {
                    if (typeMap.TryGetValue(name, out accessor) || typeMap.TryGetValue("*", out accessor))
                    {

                        // NB: Here we could also register this accessor in typeMap[type] such that
                        // next lookup on this type won't need to resolve its interfaces

                        return accessor;
                    }
                }
            }

            return null;
        }

        public bool IgnoreCasing { get; set; }

        public void Register(Type type, string name, IMemberAccessor getter)
        {
            var typeMap = _map.GetOrAdd(type, _ =>
            {
                return new ConcurrentDictionary<string, IMemberAccessor>(IgnoreCasing
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal);
            });

            typeMap[name] = getter;
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Fluid
{
    public class ConcurrentMemberAccessStrategy : IMemberAccessStrategy
    {
        private ConcurrentDictionary<Type, ConcurrentDictionary<string, IMemberAccessor>> _map;

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

            foreach (var interfaceType in type.GetTypeInfo().GetInterfaces())
            {
                accessor = GetAccessor(interfaceType, name);

                if (accessor != null)
                {
                    return accessor;
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

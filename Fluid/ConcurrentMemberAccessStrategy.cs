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

            while (type != typeof(object))
            {
                // Look for specific property map
                if (_map.TryGetValue(type, out var typeMap))
                {
                    if (typeMap.TryGetValue(name, out accessor) || typeMap.TryGetValue("*", out accessor))
                    {
                        return accessor;
                    }
                }

                type = type.GetTypeInfo().BaseType;
            }

            return null;
        }

        public void Register(Type type, string name, IMemberAccessor getter)
        {
            var typeMap = _map.GetOrAdd(type, _ =>
            {
                return new ConcurrentDictionary<string, IMemberAccessor>();
            });

            typeMap[name] = getter;
        }
    }
}

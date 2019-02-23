using System;
using System.Collections.Generic;
using System.Reflection;

namespace Fluid
{
    public class MemberAccessStrategy : IMemberAccessStrategy
    {
        private Dictionary<Type, IDictionary<string, IMemberAccessor>> _map;
        private readonly IMemberAccessStrategy _parent;

        public MemberAccessStrategy()
        {
            _map = new Dictionary<Type, IDictionary<string, IMemberAccessor>>();
        }

        public MemberAccessStrategy(IMemberAccessStrategy parent) : this()
        {
            _parent = parent;
        }

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

                accessor = accessor ?? _parent?.GetAccessor(type, name);

                if (accessor != null)
                {
                    return accessor;
                }

                type = type.GetTypeInfo().BaseType;
            }

            return null;
        }

        public void Register(Type type, string name, IMemberAccessor getter)
        {
            if (!_map.TryGetValue(type, out var typeMap))
            {
                typeMap = CreateTypeMap(type);
            }

            typeMap[name] = getter;
        }

        private Dictionary<String, IMemberAccessor> CreateTypeMap(Type type)
        {
            Dictionary<String, IMemberAccessor> typeMap;

            typeMap = new Dictionary<string, IMemberAccessor>();

            _map[type] = typeMap;

            return typeMap;
        }
    }
}

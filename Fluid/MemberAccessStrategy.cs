using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Fluid
{
    public class MemberAccessStrategy : IMemberAccessStrategy
    {
        private IDictionary<Type, IDictionary<string, IMemberAccessor>> _map;
        private readonly IMemberAccessStrategy _parent;
        private readonly bool _concurrent;

        public MemberAccessStrategy(bool concurrent = true)
        {
            if (concurrent)
            {
                _map = new ConcurrentDictionary<Type, IDictionary<string, IMemberAccessor>>();
            }
            else
            {
                _map = new Dictionary<Type, IDictionary<string, IMemberAccessor>>();
            }

            _concurrent = concurrent;
        }

        public MemberAccessStrategy(IMemberAccessStrategy parent, bool concurrent = true) : this(concurrent)
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

        private IDictionary<String, IMemberAccessor> CreateTypeMap(Type type)
        {
            IDictionary<String, IMemberAccessor> typeMap;

            if (_concurrent)
            {
                typeMap = new ConcurrentDictionary<string, IMemberAccessor>();
            }
            else
            {
                typeMap = new Dictionary<string, IMemberAccessor>();
            }

            _map[type] = typeMap;

            return typeMap;
        }
    }
}

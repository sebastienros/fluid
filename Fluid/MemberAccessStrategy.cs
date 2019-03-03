using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Fluid
{
    public class MemberAccessStrategy : IMemberAccessStrategy
    {
        private Dictionary<Type, Dictionary<string, IMemberAccessor>> _map;
        private readonly IMemberAccessStrategy _parent;
        private bool _initialized;

        public MemberAccessStrategy()
        {
            _map = new Dictionary<Type, Dictionary<string, IMemberAccessor>>();
        }

        public MemberAccessStrategy(IMemberAccessStrategy parent) : this()
        {
            _parent = parent;
        }

        public IMemberAccessor GetAccessor(Type type, string name)
        {
            IMemberAccessor accessor = null;

            if (!_initialized)
            {
                _initialized = true;
            }

            // Get a reference on the map as it can be changed by a call to Register
            var locaMap = _map;

            while (type != typeof(object))
            {
                // Look for specific property map
                if (locaMap.TryGetValue(type, out var typeMap))
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
            lock (_map)
            {
                var localMap = _map;

                // If it is already being used, we clone the structure
                if (_initialized)
                {
                    localMap = new Dictionary<Type, Dictionary<string, IMemberAccessor>>(_map);
                    foreach (var entry in localMap)
                    {
                        localMap[entry.Key] = new Dictionary<string, IMemberAccessor>(entry.Value);
                    }
                }

                if (!localMap.TryGetValue(type, out var typeMap))
                {
                    typeMap = new Dictionary<string, IMemberAccessor>();

                    localMap[type] = typeMap;
                }

                typeMap[name] = getter;

                _map = localMap;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Fluid
{
    public class MemberAccessStrategy : IMemberAccessStrategy
    {
        private Dictionary<Type, Dictionary<string, IMemberAccessor>> _map;
        private readonly IMemberAccessStrategy _parent;

        public MemberAccessStrategy()
        {
            _map = new Dictionary<Type, Dictionary<string, IMemberAccessor>>();
        }

        public MemberAccessStrategy(IMemberAccessStrategy parent) : this()
        {
            _parent = parent;
            MemberNameStrategy = _parent.MemberNameStrategy;
        }

        public MemberNameStrategy MemberNameStrategy { get; set; } = MemberNameStrategies.Default;

        public bool IgnoreCasing { get; set; }

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

                accessor = accessor ?? _parent?.GetAccessor(currentType, name);

                if (accessor != null)
                {
                    return accessor;
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
                        if (accessor != null)
                        {
                            return accessor;
                        }
                    }
                }
            }

            return null;
        }

        public void Register(Type type, string name, IMemberAccessor getter)
        {
            if (!_map.TryGetValue(type, out var typeMap))
            {
                typeMap = new Dictionary<string, IMemberAccessor>(IgnoreCasing
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal);

                _map[type] = typeMap;
            }

            typeMap[name] = getter;
        }
    }
}

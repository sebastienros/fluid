using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Fluid
{
    public class DefaultMemberAccessStrategy : MemberAccessStrategy
    {
        private Dictionary<Type, Dictionary<string, IMemberAccessor>> _map;
        private readonly MemberAccessStrategy _parent;

        public DefaultMemberAccessStrategy()
        {
            _map = new Dictionary<Type, Dictionary<string, IMemberAccessor>>();
        }

        public DefaultMemberAccessStrategy(MemberAccessStrategy parent) : this()
        {
            _parent = parent;
            MemberNameStrategy = _parent.MemberNameStrategy;
        }

        public override IMemberAccessor GetAccessor(Type type, string name)
        {
            if (_map is null)
            {
                return _parent?.GetAccessor(type, name);
            }

            // Look for specific property map
            if (TryGetAccessor(type, name, out var accessor))
            {
                return accessor;
            }

            accessor ??= _parent?.GetAccessor(type, name);

            if (accessor != null)
            {
                return accessor;
            }

            return GetAccessorUnlikely(type, name);
        }

        private IMemberAccessor GetAccessorUnlikely(Type type, string name)
        {
            var currentType = type.GetTypeInfo().BaseType;
            while (currentType != typeof(object) && currentType != null)
            {
                // Look for specific property map
                if (TryGetAccessor(currentType, name, out var accessor))
                {
                    return accessor;
                }

                accessor ??= _parent?.GetAccessor(currentType, name);

                if (accessor != null)
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

        public override void Register(Type type, string name, IMemberAccessor getter)
        {
            _map ??= new Dictionary<Type, Dictionary<string, IMemberAccessor>>();

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

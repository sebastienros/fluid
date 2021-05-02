using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Fluid
{
    public class DefaultMemberAccessStrategy : MemberAccessStrategy
    {
        private readonly object _synLock = new();

        private Dictionary<Type, Dictionary<string, IMemberAccessor>> _map = new Dictionary<Type, Dictionary<string, IMemberAccessor>>();

        public override IMemberAccessor GetAccessor(Type type, string name)
        {
            // Look for specific property map
            if (TryGetAccessor(type, name, out var accessor))
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

        public override void Register(Type type, IEnumerable<KeyValuePair<string, IMemberAccessor>> accessors)
        {
            if (accessors is null)
            {
                throw new ArgumentNullException(nameof(accessors));
            }

            // Create a copy of the current dictionary since types are added during the initialization of the app.

            lock (_synLock)
            {
                // Clone current dictionary
                var temp = new Dictionary<Type, Dictionary<string, IMemberAccessor>>(_map);

                // Clone inner dictionaries
                foreach (var typeEntry in temp)
                {
                    var entry = new Dictionary<string, IMemberAccessor>(typeEntry.Value);
                }

                if (!temp.TryGetValue(type, out var typeMap))
                {
                    typeMap = new Dictionary<string, IMemberAccessor>(IgnoreCasing
                        ? StringComparer.OrdinalIgnoreCase
                        : StringComparer.Ordinal);

                    temp[type] = typeMap;
                }

                foreach (var accessor in accessors)
                {
                    typeMap[accessor.Key] = accessor.Value;
                }

                _map = temp;
            }
        }
    }
}

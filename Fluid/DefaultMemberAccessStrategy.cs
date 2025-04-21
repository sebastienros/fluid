using System.Reflection;
using System.Runtime.CompilerServices;

namespace Fluid
{
    public class DefaultMemberAccessStrategy : MemberAccessStrategy
    {
        private readonly Lock _synLock = new();

        private readonly record struct Key(Type Type, string Name);

        private Dictionary<Key, IMemberAccessor> _map = new();

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
            return _map.TryGetValue(new Key(type, name), out accessor) || _map.TryGetValue(new Key(type, "*"), out accessor);
        }

        public override void Register(Type type, IEnumerable<KeyValuePair<string, IMemberAccessor>> accessors)
        {
            if (accessors is null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(accessors));
            }

            // Create a copy of the current dictionary since types are added during the initialization of the app.

            lock (_synLock)
            {
                // Clone current dictionary
                var temp = IgnoreCasing
                    ? new Dictionary<Key, IMemberAccessor>(_map, KeyIgnoreCaseComparer.Instance)
                    : new Dictionary<Key, IMemberAccessor>(_map);

                foreach (var accessor in accessors)
                {
                    temp[new Key(type, accessor.Key)] = accessor.Value;
                }

                _map = temp;
            }
        }

        private sealed class KeyIgnoreCaseComparer : IEqualityComparer<Key>
        {
            public static readonly KeyIgnoreCaseComparer Instance = new();

            private KeyIgnoreCaseComparer()
            {
            }

            public bool Equals(Key x, Key y)
            {
                return x.Type == y.Type && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(Key obj)
            {
#if NET6_0_OR_GREATER
                return HashCode.Combine(obj.Type, obj.Name.GetHashCode(StringComparison.OrdinalIgnoreCase));
#else
                return obj.Type.GetHashCode() ^ obj.Name.ToUpperInvariant().GetHashCode();
#endif
            }
        }
    }
}

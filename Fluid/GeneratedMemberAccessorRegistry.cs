namespace Fluid
{
    internal sealed class GeneratedMemberAccessorRegistration
    {
        public GeneratedMemberAccessorRegistration(MemberAccessor accessor, string[] memberNames)
        {
            Accessor = accessor;
            MemberNames = memberNames;
        }

        public MemberAccessor Accessor { get; }
        public string[] MemberNames { get; }

        public bool CanAccess(string name, StringComparer comparer)
        {
            foreach (var memberName in MemberNames)
            {
                if (comparer.Equals(name, memberName))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal static class GeneratedMemberAccessorRegistry
    {
        private sealed class RegistryState
        {
            public RegistryState(Dictionary<Type, GeneratedMemberAccessorRegistration[]> accessors)
            {
                Accessors = accessors;
            }

            public Dictionary<Type, GeneratedMemberAccessorRegistration[]> Accessors { get; }
        }

        private static volatile RegistryState _state = new([]);

        public static object CacheToken => _state;

        public static GeneratedMemberAccessorRegistration[] GetAccessors(Type runtimeType, out object cacheToken)
        {
            var state = _state;
            cacheToken = state;
            state.Accessors.TryGetValue(runtimeType, out var accessors);
            return accessors;
        }

        public static void Register(Type type, MemberAccessor accessor, string[] memberNames)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(accessor);
            ArgumentNullException.ThrowIfNull(memberNames);

            while (true)
            {
                var state = _state;
                var accessors = new Dictionary<Type, GeneratedMemberAccessorRegistration[]>(state.Accessors);
                var registration = new GeneratedMemberAccessorRegistration(accessor, memberNames.ToArray());

                accessors[type] = state.Accessors.TryGetValue(type, out var existing)
                    ? [.. existing, registration]
                    : [registration];

                var updated = new RegistryState(accessors);

                if (ReferenceEquals(Interlocked.CompareExchange(ref _state, updated, state), state))
                {
                    return;
                }
            }
        }
    }
}

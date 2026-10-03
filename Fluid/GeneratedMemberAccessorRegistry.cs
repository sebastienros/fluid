namespace Fluid
{
    internal static class GeneratedMemberAccessorRegistry
    {
        private static readonly Lock _syncLock = new();
        private static volatile Dictionary<Type, Dictionary<string, IMemberAccessor>> _accessors = new();

        public static Dictionary<string, IMemberAccessor> GetAccessors(Type type)
        {
            _accessors.TryGetValue(type, out var accessors);
            return accessors;
        }

        public static void Register(Type type, IMemberAccessor accessor, string[] memberNames)
        {
            if (type is null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(type));
            }

            if (accessor is null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(accessor));
            }

            if (memberNames is null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(memberNames));
            }

            lock (_syncLock)
            {
                var members = _accessors.TryGetValue(type, out var existing)
                    ? new Dictionary<string, IMemberAccessor>(existing, StringComparer.Ordinal)
                    : new Dictionary<string, IMemberAccessor>(StringComparer.Ordinal);

                foreach (var name in memberNames)
                {
                    members[name] = accessor;
                }

                _accessors = new Dictionary<Type, Dictionary<string, IMemberAccessor>>(_accessors)
                {
                    [type] = members
                };
            }
        }
    }
}

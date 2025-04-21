using System.Collections.Concurrent;

namespace Fluid
{
    public sealed class UnsafeMemberAccessStrategy : DefaultMemberAccessStrategy
    {
        private readonly ConcurrentDictionary<Type, object> _handledTypes = new();

        public static readonly UnsafeMemberAccessStrategy Instance = new();

        public override IMemberAccessor GetAccessor(Type type, string name)
        {
            var accessor = base.GetAccessor(type, name);

            if (accessor != null)
            {
                return accessor;
            }

            if (!_handledTypes.ContainsKey(type))
            {
                this.Register(type);
                _handledTypes.TryAdd(type, null);
            }

            return base.GetAccessor(type, name);
        }
    }
}

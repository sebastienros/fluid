using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Fluid
{
    public class MemberAccessStrategy : IMemberAccessStrategy
    {
        private ConcurrentDictionary<string, IMemberAccessor> _map = new ConcurrentDictionary<string, IMemberAccessor>();
        private readonly IMemberAccessStrategy _parent;

        public MemberAccessStrategy()
        {
        }

        public MemberAccessStrategy(IMemberAccessStrategy parent)
        {
            _parent = parent;
        }

        public IMemberAccessor GetAccessor(object obj, string name)
        {
            var type = obj.GetType();

            if (_map.Count > 0)
            {
                while (type != null)
                {
                    // Look for specific property map
                    if (_map.TryGetValue(Key(type, name), out var accessor))
                    {
                        return accessor;
                    }

                    // Look for a catch-all getter
                    if (_map.TryGetValue(Key(type, "*"), out accessor))
                    {
                        return accessor;
                    }

                    type = type.GetTypeInfo().BaseType;
                }

                // Register a null accessor to prevent any further lookups
                _map.TryAdd(Key(obj.GetType(), name), NullMemberAccessor.Instance);
            }

            if (_parent == null)
            {
                return NullMemberAccessor.Instance;
            }

            return _parent.GetAccessor(obj, name);
        }

        public void Register(Type type, string name, IMemberAccessor getter)
        {
            _map[Key(type, name)] = getter;
        }

        private string Key(Type type, string name) => $"{type.Name}.{name}";
    }
}

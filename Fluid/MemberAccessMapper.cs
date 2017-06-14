using System;
using System.Collections.Generic;

namespace Fluid
{
    public class MemberAccessStrategy : IMemberAccessStrategy
    {
        private Dictionary<string, IMemberAccessor> _map = new Dictionary<string, IMemberAccessor>();
        private readonly IMemberAccessStrategy _parent;

        public MemberAccessStrategy()
        {
        }

        public MemberAccessStrategy(IMemberAccessStrategy parent)
        {
            _parent = parent;
        }

        public object Get(object obj, string name)
        {
            // Look for specific property map
            if (_map.TryGetValue(Key(obj.GetType(), name), out var getter))
            {
                return getter.Get(obj, name);
            }

            // Look for a catch-all getter
            if (_map.TryGetValue(Key(obj.GetType(), "*"), out getter))
            {
                return getter.Get(obj, name);
            }

            return _parent?.Get(obj, name);
        }

        public void Register(Type type, string name, IMemberAccessor getter)
        {
            _map[Key(type, name)] = getter;
        }

        private string Key(Type type, string name) => $"{type.Name}.{name}";
    }
}

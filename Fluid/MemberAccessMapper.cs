using System;
using System.Collections.Generic;
using System.Reflection;

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

        public IMemberAccessor GetAccessor(object obj, string name)
        {
            var type = obj.GetType();

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

            var parentAccessor = _parent?.GetAccessor(obj, name);

            if (parentAccessor == null)
            {   
                // Register a null accessor to prevent any further lookups
                _map.Add(Key(obj.GetType(), name), NullMemberAccessor.Instance);
                return NullMemberAccessor.Instance;
            }

            return parentAccessor;
        }

        public void Register(Type type, string name, IMemberAccessor getter)
        {
            _map[Key(type, name)] = getter;
        }

        private string Key(Type type, string name) => $"{type.Name}.{name}";
    }
}

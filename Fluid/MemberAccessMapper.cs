using System;
using System.Collections.Generic;

namespace Fluid
{
    public class MemberAccessStrategy : IMemberAccessStrategy
    {
        private Dictionary<PropertyMapKey, IMemberAccessor> _map = new Dictionary<PropertyMapKey, IMemberAccessor>();
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
            var key = new PropertyMapKey(obj.GetType(), name);

            if (_map.TryGetValue(key, out var getter))
            {
                return getter.Get(obj);
            }

            return _parent?.Get(obj, name);
        }

        public void Register(Type type, string name, IMemberAccessor getter)
        {
            _map[new PropertyMapKey(type, name)] = getter;
        }
    }
    
    public struct PropertyMapKey : IEquatable<PropertyMapKey>
    {
        private Type _type;
        private string _member;

        public PropertyMapKey(Type type, string member)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (member == null)
            {
                throw new ArgumentNullException(nameof(member));
            }

            _type = type;
            _member = member;
        }

        public bool Equals(PropertyMapKey other)
        {
            return other._type == _type && other._member == _member;
        }

        public override int GetHashCode()
        {
            int result = 37;

            result *= 397;
            if (_type != null)
            {
                result += _type.GetHashCode();
            }

            result *= 397;
            if (_member != null)
            {
                result += _member.GetHashCode();
            }

            return result;
        }
    }
}

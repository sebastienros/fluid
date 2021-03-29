using System;
using System.Collections.Generic;

namespace Fluid
{
    public class UnsafeMemberAccessStrategy : MemberAccessStrategy
    {
        public static readonly UnsafeMemberAccessStrategy Instance = new UnsafeMemberAccessStrategy();

        private readonly MemberAccessStrategy baseMemberAccessStrategy = new DefaultMemberAccessStrategy();

        public override IMemberAccessor GetAccessor(Type type, string name)
        {
            var accessor = baseMemberAccessStrategy.GetAccessor(type, name);
            
            if (accessor != null)
            {
                return accessor;
            }

            baseMemberAccessStrategy.Register(type);
            return baseMemberAccessStrategy.GetAccessor(type, name);
        }

        public override void Register(Type type, IEnumerable<KeyValuePair<string, IMemberAccessor>> accessors)
        {
           baseMemberAccessStrategy.Register(type, accessors);
        }
    }
}

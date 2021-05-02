using System;

namespace Fluid
{
    public class UnsafeMemberAccessStrategy : DefaultMemberAccessStrategy
    {
        public static readonly UnsafeMemberAccessStrategy Instance = new UnsafeMemberAccessStrategy();

        public override IMemberAccessor GetAccessor(Type type, string name)
        {
            var accessor = base.GetAccessor(type, name);
            
            if (accessor != null)
            {
                return accessor;
            }

            this.Register(type);

            return base.GetAccessor(type, name);
        }
    }
}

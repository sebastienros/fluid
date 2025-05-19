namespace Fluid
{
    public abstract class MemberAccessStrategy
    {
        public abstract IMemberAccessor GetAccessor(Type type, string name, StringComparer stringComparer);

        public abstract void Register(Type type, string name, IMemberAccessor accessor);
    }
}

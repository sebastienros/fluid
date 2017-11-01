namespace Fluid
{
    public class NullMemberAccessor : IMemberAccessor
    {
        public static IMemberAccessor Instance = new NullMemberAccessor();

        private NullMemberAccessor()
        {

        }
        
        object IMemberAccessor.Get(object obj, string name)
        {
            return null;
        }
    }
}

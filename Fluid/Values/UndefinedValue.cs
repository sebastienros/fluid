namespace Fluid.Values
{
    public sealed class UndefinedValue : BaseNilValue
    {
        public static readonly UndefinedValue Instance = new(); // a variable that is not defined

        private UndefinedValue()
        {
        }
    }
}

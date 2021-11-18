namespace Fluid.Values
{
    public static class FluidValueExtensions
    {

        public static FluidValue Or(this FluidValue self, FluidValue other)
        {
            if (self.IsNil())
            {
                return other;
            }

            return self;
        }
    }
}

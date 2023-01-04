using Fluid.Values;

namespace Fluid.Compilation
{
    public class CompiledTemplateBase
    {
        public static FluidValue BuildArray(int start, int end)
        {
            // If end < start, we create an empty array
            var list = new FluidValue[Math.Max(0, end - start + 1)];

            for (var i = 0; i < list.Length; i++)
            {
                list[i] = NumberValue.Create(start + i);
            }

            return new ArrayValue(list);
        }
    }
}

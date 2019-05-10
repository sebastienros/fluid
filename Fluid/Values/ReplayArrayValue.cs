using System.Collections.Generic;

namespace Fluid.Values
{
    public class ReplayArrayValue: ArrayValue, IOriginalValue
    {
        public object OriginalValue { get; set; }

        public ReplayArrayValue(List<FluidValue> value, object originalValue) : base(value)
        {
            OriginalValue = originalValue;
        }

        public ReplayArrayValue(FluidValue[] value, object originalValue) : base(value)
        {
            OriginalValue = originalValue;
        }

        public ReplayArrayValue(IEnumerable<FluidValue> value, object originalValue) : base(value)
        {
            OriginalValue = originalValue;
        }
    }

    public interface IOriginalValue
    {
        object OriginalValue { get; set; }
    }
}

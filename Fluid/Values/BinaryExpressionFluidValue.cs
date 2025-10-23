using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    /// <summary>
    /// A FluidValue that wraps a binary expression result.
    /// It returns the comparison result when used in a boolean context (e.g., {% if %})
    /// and returns the left operand when rendered (e.g., {{ }}).
    /// </summary>
    public sealed class BinaryExpressionFluidValue : FluidValue
    {
        private readonly FluidValue _leftOperand;
        private readonly bool _comparisonResult;

        public BinaryExpressionFluidValue(FluidValue leftOperand, bool comparisonResult)
        {
            _leftOperand = leftOperand ?? NilValue.Instance;
            _comparisonResult = comparisonResult;
        }

        public override FluidValues Type => _leftOperand.Type;

        public override bool Equals(FluidValue other)
        {
            return _leftOperand.Equals(other);
        }

        public override bool ToBooleanValue()
        {
            // Return the comparison result for conditional logic
            return _comparisonResult;
        }

        public override decimal ToNumberValue()
        {
            return _leftOperand.ToNumberValue();
        }

        public override string ToStringValue()
        {
            // When converted to string (e.g., for filters), return the boolean result as string
            return _comparisonResult ? "true" : "false";
        }

        public override object ToObjectValue()
        {
            return _leftOperand.ToObjectValue();
        }

        public override ValueTask WriteToAsync(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            // Delegate rendering to the left operand
            return _leftOperand.WriteToAsync(writer, encoder, cultureInfo);
        }

        public override bool IsNil()
        {
            return _leftOperand.IsNil();
        }

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            return _leftOperand.GetValueAsync(name, context);
        }

        public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        {
            return _leftOperand.GetIndexAsync(index, context);
        }

        public override bool Contains(FluidValue value)
        {
            return _leftOperand.Contains(value);
        }

        public override IEnumerable<FluidValue> Enumerate(TemplateContext context)
        {
            return _leftOperand.Enumerate(context);
        }
    }
}

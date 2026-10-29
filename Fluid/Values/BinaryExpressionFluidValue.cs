using System.Text.Encodings.Web;

namespace Fluid.Values;

/// <summary>
/// A FluidValue that wraps a binary expression result.
/// It returns the comparison result when used in a boolean context (e.g., {% if %})
/// and returns the left operand when rendered (e.g., {{ }}).
/// </summary>
public sealed class BinaryExpressionFluidValue : FluidValue
{
    private readonly FluidValue _leftOperand;
    private readonly bool _comparisonResult;

    public static BinaryExpressionFluidValue Create(FluidValue leftOperand, bool comparisonResult)
        => new BinaryExpressionFluidValue(leftOperand, comparisonResult);

    private BinaryExpressionFluidValue(FluidValue leftOperand, bool comparisonResult)
    {
        _leftOperand = leftOperand ?? NilValue.Instance;
        _comparisonResult = comparisonResult;
    }

    public override FluidValues Type => _leftOperand.Type;

    public override bool Equals(FluidValue other)
    {
        return _leftOperand.Equals(other);
    }

    public override bool ToBooleanValue(TemplateContext context)
    {
        // Return the comparison result for conditional logic
        return _comparisonResult;
    }

    public override decimal ToNumberValue(TemplateContext context)
    {
        return _leftOperand.ToNumberValue(context);
    }

    public override string ToStringValue(TemplateContext context)
    {
        // When converted to string (e.g., for filters), return the boolean result as string
        return _comparisonResult ? "true" : "false";
    }

    public override object ToObjectValue(TemplateContext context)
    {
        return _leftOperand.ToObjectValue(context);
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        // Delegate rendering to the left operand
        return _leftOperand.WriteToAsync(output, encoder, context);
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

    public override ValueTask<bool> ContainsAsync(FluidValue value, TemplateContext context)
    {
        return _leftOperand.ContainsAsync(value, context);
    }

    public override IAsyncEnumerable<FluidValue> EnumerateAsync(TemplateContext context)
    {
        return _leftOperand.EnumerateAsync(context);
    }
}

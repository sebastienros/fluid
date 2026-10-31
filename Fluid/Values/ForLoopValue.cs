using System.Text.Encodings.Web;

namespace Fluid.Values;

public sealed class ForLoopValue : FluidValue
{
    public static ForLoopValue Create() => new ForLoopValue();

    private ForLoopValue()
    {
    }

    public int Length { get; set; }
    public int Index { get; set; }
    public int Index0 { get; set; }
    public int RIndex { get; set; }
    public int RIndex0 { get; set; }
    public bool First { get; set; }
    public bool Last { get; set; }
    public string Identifier { get; set; }
    public string Source { get; set; }

    public ForLoopValue ParentLoop { get; set; }

    /// <summary>
    /// Gets or sets whether this forloop is from a render tag.
    /// Render tag forloops should not be accessible as parentloop from nested for loops.
    /// </summary>
    public bool IsRenderLoop { get; set; }

    public int Count => Length;

    public override FluidValues Type => FluidValues.Dictionary;

    public override bool Equals(FluidValue other)
    {
        return false;
    }

    public override bool ToBooleanValue(TemplateContext context)
    {
        return false;
    }

    public override decimal ToNumberValue(TemplateContext context)
    {
        return Length;
    }

    public override object ToObjectValue(TemplateContext context)
    {
        return null;
    }

    public override string ToStringValue(TemplateContext context)
    {
        return "forloop";
    }

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
    {
        return name switch
        {
            "length" => NumberValue.Create(Length),
            "index" => NumberValue.Create(Index),
            "index0" => NumberValue.Create(Index0),
            "rindex" => NumberValue.Create(RIndex),
            "rindex0" => NumberValue.Create(RIndex0),
            "first" => BooleanValue.Create(First),
            "last" => BooleanValue.Create(Last),
            "parentloop" => ParentLoop is null ? NilValue.Instance : ParentLoop,
            "name" => !string.IsNullOrEmpty(Identifier) && !string.IsNullOrEmpty(Source)
                ? StringValue.Create(Identifier + "-" + Source)
                : NilValue.Instance,
            _ => NilValue.Instance,
        };
    }

    // Like Liquid's drops, forloop['index'] reads the same properties as forloop.index.
    public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
    {
        return GetValueAsync(index.ToStringValue(context), context);
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        return default;
    }
}

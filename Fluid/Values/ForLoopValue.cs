using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class ForLoopValue : FluidValue
    {
        public int Length { get; set; }
        public int Index { get; set; }
        public int Index0 { get; set; }
        public int RIndex { get; set; }
        public int RIndex0 { get; set; }
        public bool First { get; set; }
        public bool Last { get; set; }

        public int Count => Length;

        public override FluidValues Type => FluidValues.Dictionary;

        public override bool Equals(FluidValue other)
        {
            return false;
        }

        public override bool ToBooleanValue()
        {
            return false;
        }

        public override bool ToBooleanValue(TemplateContext context)
        {
            return false;
        }

        public override decimal ToNumberValue()
        {
            return Length;
        }

        public override decimal ToNumberValue(TemplateContext context)
        {
            return Length;
        }

        public override object ToObjectValue()
        {
            return null;
        }

        public override object ToObjectValue(TemplateContext context)
        {
            return null;
        }

        public override string ToStringValue()
        {
            return "forloop";
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
                _ => NilValue.Instance,
            };
        }

        public override ValueTask WriteToAsync(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            return default;
        }
    }
}

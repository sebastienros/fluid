using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

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

        public override decimal ToNumberValue()
        {
            return Length;
        }

        public override object ToObjectValue()
        {
            return null;
        }

        public override string ToStringValue()
        {
            return "forloop";
        }

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            return name switch
            {
                "length" => new ValueTask<FluidValue>(NumberValue.Create(Length)),
                "index" => new ValueTask<FluidValue>(NumberValue.Create(Index)),
                "index0" => new ValueTask<FluidValue>(NumberValue.Create(Index0)),
                "rindex" => new ValueTask<FluidValue>(NumberValue.Create(RIndex)),
                "rindex0" => new ValueTask<FluidValue>(NumberValue.Create(RIndex0)),
                "first" => new ValueTask<FluidValue>(BooleanValue.Create(First)),
                "last" => new ValueTask<FluidValue>(BooleanValue.Create(Last)),
                _ => new ValueTask<FluidValue>(NilValue.Instance),
            };
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
        }
    }
}

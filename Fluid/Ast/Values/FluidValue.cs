using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast.Values
{
    public abstract class FluidValue
    {
        public static FluidValue Nil = new StringValue("nil");
        
        public abstract void WriteTo(TextWriter writer, TextEncoder encoder);

        public abstract FluidValue Add(FluidValue other);
        public abstract FluidValue Equals(FluidValue other);

        public abstract bool ToBoolean();
        public abstract double ToNumber();
    }
}

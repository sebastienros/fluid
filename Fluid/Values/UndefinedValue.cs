using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    internal sealed class UndefinedValue : FluidValue
    {
        private readonly TemplateContext _context;
        private readonly string _path;
        private bool _notified;

        public UndefinedValue(string path, TemplateContext context)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Path => _path;

        private void NotifyUsage()
        {
            if (_notified)
            {
                return;
            }

            _notified = true;
            _context.NotifyUndefinedUsage(_path);
        }

        internal UndefinedValue AppendProperty(string property)
        {
            if (string.IsNullOrEmpty(property))
            {
                return this;
            }

            return new UndefinedValue($"{_path}.{property}", _context);
        }

        internal UndefinedValue AppendIndexer(string index)
        {
            if (string.IsNullOrEmpty(index))
            {
                return this;
            }

            return new UndefinedValue($"{_path}[{index}]", _context);
        }

        public override bool Equals(FluidValue other)
        {
            NotifyUsage();
            return other == NilValue.Instance || other.IsNil();
        }

        public override bool ToBooleanValue()
        {
            NotifyUsage();
            return false;
        }

        public override decimal ToNumberValue()
        {
            NotifyUsage();
            return 0;
        }

        public override string ToStringValue()
        {
            NotifyUsage();
            return string.Empty;
        }

        public override object ToObjectValue()
        {
            NotifyUsage();
            return null;
        }

        public override ValueTask WriteToAsync(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            NotifyUsage();
            return default;
        }

        [Obsolete("WriteTo is obsolete, prefer the WriteToAsync method.")]
        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            NotifyUsage();
        }

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            return new ValueTask<FluidValue>(AppendProperty(name));
        }

        public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        {
            var value = index.ToStringValue();
            return new ValueTask<FluidValue>(AppendIndexer(value));
        }

        public override ValueTask<FluidValue> InvokeAsync(FunctionArguments arguments, TemplateContext context)
        {
            NotifyUsage();
            return new ValueTask<FluidValue>(NilValue.Instance);
        }

        public override FluidValues Type => FluidValues.Nil;

        public override bool IsNil()
        {
            NotifyUsage();
            return true;
        }

        public override IEnumerable<FluidValue> Enumerate(TemplateContext context)
        {
            NotifyUsage();
            return Array.Empty<FluidValue>();
        }
    }
}

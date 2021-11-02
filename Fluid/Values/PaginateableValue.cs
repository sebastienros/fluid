using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public abstract class PaginateableValue : FluidValue
    {
        public override FluidValues Type => FluidValues.Array;

        protected abstract PaginatedData Paginate(Int32 pageSize);

        public abstract string GetUrl(int page);

        public abstract Int32 CurrentPage { get; }

        private PaginatedData _paginatedData;

        private Int32 _pageSize = 10;

        public PaginatedData GetPaginatedData()
        {
            if (_paginatedData == null)
            {
                _paginatedData = Paginate(PageSize);
            }
            return _paginatedData;
        }

        protected virtual Int32 MaxPageSize => 50;

        protected virtual Int32 MinPageSize => 5;

        protected virtual int DefaultPageSize => 10;

        public Int32 PageSize
        {
            get { return _pageSize; }
            internal set
            {
                if (value > MaxPageSize || value < MinPageSize) value = DefaultPageSize;
                if (_pageSize != value)
                {
                    _paginatedData = null;
                    _pageSize = value;
                }
            }
        }

        protected override FluidValue GetValue(string name, TemplateContext context)
        {
            var data = this.GetPaginatedData();
            switch (name)
            {
                case "total":
                    return NumberValue.Create(data.Total);
                case "size":
                    return NumberValue.Create(data.Items.Count);
                case "first":
                    if (data.Items.Count > 0) return data.Items[0];
                    break;
                case "last":
                    if (data.Items.Count > 0) return data.Items[data.Items.Count - 1];
                    break;
            }
            return base.GetValue(name, context);
        }

        public override bool Equals(FluidValue other)
        {
            if (other == null || other.IsNil()) return false;
            return other is PaginateableValue value && value._paginatedData == _paginatedData;
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override decimal ToNumberValue()
        {
            return GetPaginatedData().Total;
        }

        public override bool Contains(FluidValue value)
        {
            return GetPaginatedData().Items.Contains(value);
        }

        public override IEnumerable<FluidValue> Enumerate()
        {
            return GetPaginatedData().Items;
        }

        public override object ToObjectValue()
        {
            return GetPaginatedData().Items;
        }

        public override string ToStringValue()
        {
            return string.Join(string.Empty, GetPaginatedData().Items.Select(x => x.ToStringValue()));
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);

            foreach (var v in GetPaginatedData().Items) writer.Write(v.ToStringValue());
        }
    }
}

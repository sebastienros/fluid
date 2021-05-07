using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Values
{
    public abstract class PaginationValue : FluidValue
    {
        private readonly Cache _cache;

        protected PaginationValue()
        {
            _cache = new Cache(this);
        }

        public override FluidValues Type => FluidValues.Array;
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; internal set; } = 20;
        public int MaxPageSize { get; set; } = 50;

        internal string BuildUrl(int page)
        {
            return GetUrl(page);
        }

        protected abstract string GetUrl(int page);
        protected abstract Task<QueryResult> QueryAsync(int page, int pageSize);

        internal async Task<long> GetItemCount()
        {
            var ret = await _cache.GetOrCreateAsync();
            return ret.ItemCount;
        }

        public override IEnumerable<FluidValue> Enumerate()
        {
            return _cache.GetOrCreate();
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (encoder == null)
            {
                throw new ArgumentNullException(nameof(encoder));
            }

            encoder.Encode(writer, ToString());
        }

        public override async ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            var queryResult = await _cache.GetOrCreateAsync();
            switch (name)
            {
                case "size":
                    return NumberValue.Create(queryResult.ItemCount);
            }

            return await base.GetValueAsync(name, context);
        }

        public override async ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        {
            var list = await _cache.GetOrCreateAsync();
            var idx = Convert.ToInt32(index.ToNumberValue());
            if (idx >= 0 && idx < list.Count)
            {
                return list[idx];
            }

            return NilValue.Instance;
        }

        public override bool ToBooleanValue()
        {
            return _cache.GetOrCreate().ItemCount > 0;
        }

        public override decimal ToNumberValue()
        {
            return _cache.GetOrCreate().ItemCount;
        }

        public override string ToStringValue()
        {
            var items = _cache.GetOrCreate();
            return string.Join(string.Empty, items.Select(i => i.ToStringValue()));
        }


        public override bool Contains(FluidValue value)
        {
            var items = _cache.GetOrCreate();
            return items.Contains(value);
        }

        public override object ToObjectValue()
        {
            return this;
        }

        public override bool Equals(FluidValue other)
        {
            if (other == null)
            {
                return false;
            }

            return GetHashCode() == other.GetHashCode();
        }

        private class Cache : Dictionary<string, QueryResult>
        {
            private readonly PaginationValue _value;

            public Cache(PaginationValue value)
            {
                _value = value;
            }

            public QueryResult GetOrCreate()
            {
                return GetOrCreateAsync().GetAwaiter().GetResult();
            }

            public async Task<QueryResult> GetOrCreateAsync()
            {
                if (_value.PageSize > _value.MaxPageSize)
                {
                    _value.PageSize = _value.MaxPageSize;
                }
                else if (_value.PageSize < 0)
                {
                    _value.PageSize = 20;
                }

                if (_value.CurrentPage < 0)
                {
                    _value.CurrentPage = 1;
                }

                var key = string.Join("#", _value.CurrentPage, _value.PageSize);
                if (!TryGetValue(key, out var ret))
                {
                    this[key] = ret = await _value.QueryAsync(_value.CurrentPage, _value.PageSize);
                }

                return ret;
            }
        }

        protected class QueryResult : List<FluidValue>
        {
            public long ItemCount { get; set; }
        }
    }
}
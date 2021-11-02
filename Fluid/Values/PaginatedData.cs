using System;
using System.Collections.Generic;
using System.Text;

namespace Fluid.Values
{
    public class PaginatedData
    {
        public int Total { get; }

        public IReadOnlyList<FluidValue> Items { get; }

        public PaginatedData(IReadOnlyList<FluidValue> items, int total)
        {
            Total = total;
            Items = items;
        }
    }
}

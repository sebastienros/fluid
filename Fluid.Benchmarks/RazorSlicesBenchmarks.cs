using RazorSlices;
using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;

namespace Fluid.Benchmarks
{
    public class RazorSlicesBenchmarks : BaseBenchmarks
    {
        private readonly HtmlEncoder _encoder = HtmlEncoder.Default;

        public RazorSlicesBenchmarks()
        {
        }

        public override object Parse()
        {
            throw new NotSupportedException();
        }

        public override object ParseBig()
        {
            throw new NotSupportedException();
        }

        public override string Render()
        {
            var result = Slices.Product.Create(Products).RenderAsync(_encoder);

            if (result.IsCompletedSuccessfully)
            {
                return result.Result;
            }

            return result.GetAwaiter().GetResult();
        }

        public override string ParseAndRender()
        {
            throw new NotSupportedException();
        }
    }
}

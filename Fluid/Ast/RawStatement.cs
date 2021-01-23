﻿using Parlot;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class RawStatement : Statement
    {
        private readonly TextSpan _text;

        public RawStatement(in TextSpan text)
        {
            _text = text;
        }

        public ref readonly TextSpan Text => ref _text;

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            return Normal;
        }
    }
}
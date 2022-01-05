﻿using Fluid.Ast;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Parser
{
    public sealed class FluidTemplate : IFluidTemplate
    {
        private readonly List<Statement> _statements;

        public FluidTemplate(params Statement[] statements)
        {
            _statements = new List<Statement>(statements ?? Array.Empty<Statement>());
        }

        public FluidTemplate(List<Statement> statements)
        {
            _statements = statements ?? throw new ArgumentNullException(nameof(statements));
        }

        internal IReadOnlyList<Statement> Statements => _statements;

        public ValueTask RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            if (writer == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(writer));
            }

            if (encoder == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(encoder));
            }

            if (context == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(context));
            }

            var i = 0;
            foreach (var statement in _statements.AsSpan())
            {
                var task = statement.WriteToAsync(writer, encoder, context);
                if (!task.IsCompletedSuccessfully)
                {
                    return Awaited(
                        task,
                        writer,
                        encoder,
                        context,
                        _statements,
                        startIndex: i + 1);
                }

                i++;
            }

            return new ValueTask();
        }

        private static async ValueTask Awaited(
            ValueTask<Completion> task,
            TextWriter writer,
            TextEncoder encoder,
            TemplateContext context,
            IReadOnlyList<Statement> statements,
            int startIndex)
        {
            await task;
            for (var i = startIndex; i < statements.Count; i++)
            {
                await statements[i].WriteToAsync(writer, encoder, context);
            }
        }
    }
}

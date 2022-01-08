﻿using Fluid.Values;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    internal sealed class IncludeStatement : Statement
    {
        private const string ViewExtension = ".liquid";

        private readonly FluidParser _parser;
        private volatile CachedTemplate _cachedTemplate;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private readonly Expression _path;
        private readonly Expression _with;
        private readonly Expression _for;
        private readonly string _alias;
        private readonly List<AssignStatement> _assignStatements;

        public IncludeStatement(
            FluidParser parser,
            Expression path,
            Expression with = null,
            Expression @for = null,
            string alias = null,
            List<AssignStatement> assignStatements = null)
        {
            _parser = parser;
            _path = path;
            _with = with;
            _for = @for;
            _alias = alias;
            _assignStatements = assignStatements;
        }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var relativePath = (await _path.EvaluateAsync(context)).ToStringValue();

            if (!relativePath.EndsWith(ViewExtension, StringComparison.OrdinalIgnoreCase))
            {
                relativePath += ViewExtension;
            }

            if (_cachedTemplate == null || !string.Equals(_cachedTemplate.Name, Path.GetFileNameWithoutExtension(relativePath), StringComparison.Ordinal))
            {
                await _semaphore.WaitAsync();

                try
                {
                    if (_cachedTemplate == null || !string.Equals(_cachedTemplate.Name, Path.GetFileNameWithoutExtension(relativePath), StringComparison.Ordinal))
                    {

                        var fileProvider = context.Options.FileProvider;

                        var fileInfo = fileProvider.GetFileInfo(relativePath);

                        if (fileInfo == null || !fileInfo.Exists)
                        {
                            throw new FileNotFoundException(relativePath);
                        }

                        var content = "";

                        using (var stream = fileInfo.CreateReadStream())
                        using (var streamReader = new StreamReader(stream))
                        {
                            content = await streamReader.ReadToEndAsync();
                        }

                        if (!_parser.TryParse(content, out var template, out var errors))
                        {
                            throw new ParseException(errors);
                        }

                        var identifier = Path.GetFileNameWithoutExtension(relativePath);

                        _cachedTemplate = new CachedTemplate(template, identifier);
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            try
            {
                context.EnterChildScope();

                if (_with != null)
                {
                    var with = await _with.EvaluateAsync(context);

                    context.SetValue(_alias ?? _cachedTemplate.Name, with);

                    await _cachedTemplate.Template.RenderAsync(writer, encoder, context);
                }
                else if (_assignStatements != null)
                {
                    var length = _assignStatements.Count;
                    for (var i = 0; i < length; i++)
                    {
                        await _assignStatements[i].WriteToAsync(writer, encoder, context);
                    }

                    await _cachedTemplate.Template.RenderAsync(writer, encoder, context);
                }
                else if (_for != null)
                {
                    try
                    {
                        var forloop = new ForLoopValue();

                        var list = (await (await _for.EvaluateAsync(context)).EnumerateAsync(context)).ToList();

                        var length = forloop.Length = list.Count;

                        context.SetValue("forloop", forloop);

                        for (var i = 0; i < length; i++)
                        {
                            context.IncrementSteps();

                            var item = list[i];

                            context.SetValue(_alias ?? _cachedTemplate.Name, item);

                            // Set helper variables
                            forloop.Index = i + 1;
                            forloop.Index0 = i;
                            forloop.RIndex = length - i - 1;
                            forloop.RIndex0 = length - i;
                            forloop.First = i == 0;
                            forloop.Last = i == length - 1;

                            await _cachedTemplate.Template.RenderAsync(writer, encoder, context);

                            // Restore the forloop property after every statement in case it replaced it,
                            // for instance if it contains a nested for loop
                            context.SetValue("forloop", forloop);
                        }
                    }
                    finally
                    {
                        context.LocalScope.Delete("forloop");
                    }
                }
                else
                {
                    // no with, for or assignments, e.g. {% include 'products' %}
                    await _cachedTemplate.Template.RenderAsync(writer, encoder, context);
                }
            }
            finally
            {
                context.ReleaseScope();
            }

            return Completion.Normal;
        }

        private record class CachedTemplate(IFluidTemplate Template, string Name);

    }
}

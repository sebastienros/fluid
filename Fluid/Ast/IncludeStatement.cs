using Fluid.Values;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class IncludeStatement : Statement
    {
        public const string ViewExtension = ".liquid";
        private readonly FluidParser _parser;
        private volatile CachedTemplate _cachedTemplate;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        public IncludeStatement(FluidParser parser, Expression path, Expression with = null, Expression @for = null, string alias = null, IList<AssignStatement> assignStatements = null)
        {
            _parser = parser;
            Path = path;
            With = with;
            For = @for;
            Alias = alias;
            AssignStatements = assignStatements;
        }

        public Expression Path { get; }
        public IList<AssignStatement> AssignStatements { get; }
        public Expression With { get; }
        public Expression For { get; }
        public string Alias { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var relativePath = (await Path.EvaluateAsync(context)).ToStringValue();

            if (!relativePath.EndsWith(ViewExtension, StringComparison.OrdinalIgnoreCase))
            {
                relativePath += ViewExtension;
            }

            if (_cachedTemplate == null || !string.Equals(_cachedTemplate.Name, System.IO.Path.GetFileNameWithoutExtension(relativePath), StringComparison.Ordinal))
            {
                await _semaphore.WaitAsync();

                try
                {
                    if (_cachedTemplate == null || !string.Equals(_cachedTemplate.Name, System.IO.Path.GetFileNameWithoutExtension(relativePath), StringComparison.Ordinal))
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

                        var identifier = System.IO.Path.GetFileNameWithoutExtension(relativePath);

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
                
                if (With != null)
                {
                    var with = await With.EvaluateAsync(context);

                    context.SetValue(Alias ?? _cachedTemplate.Name, with);

                    await _cachedTemplate.Template.RenderAsync(writer, encoder, context);
                }
                else if (AssignStatements != null)
                {
                    var length = AssignStatements.Count;
                    for (var i = 0; i < length; i++)
                    {
                        await AssignStatements[i].WriteToAsync(writer, encoder, context);
                    }

                    await _cachedTemplate.Template.RenderAsync(writer, encoder, context);
                }
                else if (For != null)
                {
                    try
                    {
                        var forloop = new ForLoopValue();

                        var list = (await For.EvaluateAsync(context)).Enumerate(context).ToList();

                        var length = forloop.Length = list.Count;

                        context.SetValue("forloop", forloop);

                        for (var i = 0; i < length; i++)
                        {
                            context.IncrementSteps();

                            var item = list[i];

                            context.SetValue(Alias ?? _cachedTemplate.Name, item);

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

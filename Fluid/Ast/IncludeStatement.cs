using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;

namespace Fluid.Ast
{
    public class IncludeStatement : Statement
    {
        private static readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());

        /// <summary>
        /// Override <see cref="IFluidParserFactory"/> used by a <see cref="TemplateContext"/>
        /// by setting an ambient value with a key matching the value of the
        /// <see cref="FluidParserFactoryKey"/>.
        /// </summary>
        public const string FluidParserFactoryKey = "FluidParserFactory";
        /// <summary>
        /// Override <see cref="Func{IFluidTemplate}"/> used by a <see cref="TemplateContext"/>
        /// by setting an ambient value with a key matching the value of the
        /// <see cref="FluidTemplateFactoryKey"/>.
        /// </summary>
        public const string FluidTemplateFactoryKey = "FluidTemplateFactory";
        public const string ViewExtension = ".liquid";

        public IncludeStatement(Expression path, Expression with = null, IList<AssignStatement> assignStatements = null)
        {
            Path = path;
            With = with;
            AssignStatements = assignStatements;
        }

        public Expression Path { get; }

        public IList<AssignStatement> AssignStatements { get; }

        public Expression With { get; }

        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var relativePath = (await Path.EvaluateAsync(context)).ToStringValue();
            if (!relativePath.EndsWith(ViewExtension))
            {
                relativePath += ViewExtension;
            }

            var fileProvider = context.FileProvider ?? TemplateContext.GlobalFileProvider;
            var fileInfo = fileProvider.GetFileInfo(relativePath);

            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException(relativePath);
            }

            using (var stream = fileInfo.CreateReadStream())
            using (var streamReader = new StreamReader(stream))
            {
                var childScope = context.EnterChildScope();
                string partialTemplate = await streamReader.ReadToEndAsync();
                var template = ParseTemplate(relativePath, fileProvider, context);
                if (With != null)
                {
                    var identifier = System.IO.Path.GetFileNameWithoutExtension(relativePath);
                    var with = await With.EvaluateAsync(context);
                    childScope.SetValue(identifier, with);
                }

                if (AssignStatements != null)
                {
                    foreach (var assignStatement in AssignStatements)
                    {
                        await assignStatement.WriteToAsync(writer, encoder, context);
                    }
                }

                await template.RenderAsync(writer, encoder, context);

                childScope.ReleaseScope();
            }

            return Completion.Normal;
        }

        private static IFluidTemplate ParseTemplate(string path, IFileProvider fileProvider, TemplateContext context)
        {
            return _memoryCache.GetOrCreate(path, entry =>
            {
                var fileInfo = fileProvider.GetFileInfo(path);
                var statements = new List<Statement>();
                entry.SlidingExpiration = TimeSpan.FromHours(1);
                entry.ExpirationTokens.Add(fileProvider.Watch(path));

                using (var stream = fileInfo.CreateReadStream())
                {
                    using (var sr = new StreamReader(stream))
                    {
                        if (FluidTemplate.TryParse(sr.ReadToEnd(), out var template, out var errors))
                        {
                            statements.AddRange(template.Statements);
                            template.Statements = statements;
                            return template;
                        }
                        else
                        {
                            throw new Exception(String.Join(Environment.NewLine, errors));
                        }
                    }
                }
            });
        }
    }
}

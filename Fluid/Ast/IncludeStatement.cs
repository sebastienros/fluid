using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Caching.Memory;

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

        public IncludeStatement(Expression path)
        {
            Path = path;
        }

        public Expression Path { get; }

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

            var isComment = context.GetValue("IsComment").ToBooleanValue();
            if (!isComment)
            {
                var childScope = context.EnterChildScope();
                var template = ParseTemplate(relativePath, fileProvider, context);
                await template.RenderAsync(writer, encoder, context);
                childScope.ReleaseScope();
            }

            context.SetValue("IsComment", false);

            return Completion.Normal;
        }

        private static IFluidTemplate ParseTemplate(string path, IFileProvider fileProvider, TemplateContext context)
        {
            return _memoryCache.GetOrCreate(path, entry =>
            {
                var fileInfo = fileProvider.GetFileInfo(path);
                entry.SlidingExpiration = TimeSpan.FromHours(1);
                entry.ExpirationTokens.Add(fileProvider.Watch(path));

                using (var stream = fileInfo.CreateReadStream())
                {
                    using (var sr = new StreamReader(stream))
                    {
                        var parser = CreateParser(context);
                        if (parser.TryParse(sr.ReadToEnd(), out var statements, out var errors))
                        {
                            return CreateTemplate(context, statements);
                        }
                        else
                        {
                            throw new Exception(String.Join(Environment.NewLine, errors));
                        }
                    }
                }
            });
        }

        private static IFluidParser CreateParser(TemplateContext context)
        {
            if (context.AmbientValues.TryGetValue(FluidParserFactoryKey, out var factory))
            {
                return ((IFluidParserFactory)factory).CreateParser();
            }
            else
            {
                return FluidTemplate.Factory.CreateParser();
            }
        }

        private static IFluidTemplate CreateTemplate(TemplateContext context, IList<Statement> statements)
        {
            IFluidTemplate template;
            if (context.AmbientValues.TryGetValue(FluidTemplateFactoryKey, out var factory))
            {
                template = ((Func<IFluidTemplate>)factory)();
            }
            else
            {
                template = new FluidTemplate();
            }
            template.Statements = statements;

            return template;
        }

        private class FluidViewTemplate : BaseFluidTemplate<FluidViewTemplate>
        {

        }
    }
}

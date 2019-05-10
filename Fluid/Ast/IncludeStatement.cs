using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class IncludeStatement : Statement
    {
        //Selz: Support to reuse the parent scope instead of open scope everytime
        //This is the white list of the template name which should reuse parent scope
        private static readonly string[] ReuseScopeTemplateName = {"blocks-styles.liquid"};

        public string TemplateName { get; }
        //Selz: Flag to indicate whether scope need to be opened
        public bool OpenScope { get; }

        public const string ViewExtension = ".liquid";

        public IncludeStatement(string templateName)
        {
            TemplateName = templateName;
            OpenScope = !ReuseScopeTemplateName.Contains(templateName);
        }


        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            string templateName = TemplateName;

            var fileProvider = context.FileProvider ?? TemplateContext.GlobalFileProvider;
            var fileInfo = fileProvider.GetFileInfo(templateName);

            if (fileInfo == null || !fileInfo.Exists)
            {
                throw new FileNotFoundException(templateName);
            }

            using (var stream = fileInfo.CreateReadStream())
            using (var streamReader = new StreamReader(stream))
            {
                // Selz: Only open scope when the flag is false
                if (OpenScope)
                {
                    context.EnterChildScope();
                }

                string partialTemplate = await streamReader.ReadToEndAsync();
                var parser = CreateParser(context);
                if (parser.TryParse(partialTemplate, true, out var statements, out var errors))
                {
                    var template = CreateTemplate(context, statements);

                    await template.RenderAsync(writer, encoder, context);
                }
                else
                {
                    throw new Exception(String.Join(Environment.NewLine, errors));
                }

                // Selz: Release scope if it is opened
                if (OpenScope)
                {
                    context.ReleaseScope();
                }
            }

            return Completion.Normal;
        }

        private static IFluidParser CreateParser(TemplateContext context)
        {
            return context.ParserFactory != null
                ? context.ParserFactory.CreateParser()
                : FluidTemplate.Factory.CreateParser()
                ;
        }

        private static IFluidTemplate CreateTemplate(TemplateContext context, List<Statement> statements)
        {
            IFluidTemplate template = context.TemplateFactory != null 
                ? context.TemplateFactory()
                : new FluidTemplate()
                ;

            template.Statements = statements;
            return template;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.IO.Compression;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Fluid;
using Fluid.Ast;

namespace FluidMvcViewEngine
{
    public class FluidRendering : IFluidRendering
    {
        static FluidRendering()
        {
            TemplateContext.GlobalMemberAccessStrategy.Register<ViewDataDictionary>();
            TemplateContext.GlobalMemberAccessStrategy.Register<ModelStateDictionary>();
        }

        private ConcurrentDictionary<string, IList<Statement>> _fluidCache = new ConcurrentDictionary<string, IList<Statement>>();

        public Task<string> Render(FileInfo fluidFile, object model, ViewDataDictionary viewData, ModelStateDictionary modelState)
        {
            var statements = _fluidCache.GetOrAdd(fluidFile.FullName, filename =>
            {
                var source= File.ReadAllText(filename);
                           
                if (FluidTemplate.TryParse(source, out var temp, out var errors))
                {
                    return temp.Statements;
                }

                throw new Exception(String.Join("\r\n", errors));
            });

            var template = new FluidTemplate(statements);

            var context = new TemplateContext();
            context.LocalScope.SetValue("Model", model);
            context.LocalScope.SetValue("ViewData", viewData);
            context.LocalScope.SetValue("ModelState", modelState);
                        
            return template.RenderAsync(context);
        }
    }
}

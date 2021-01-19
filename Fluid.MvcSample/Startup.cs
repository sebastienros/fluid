using Fluid.Ast;
using Fluid.MvcSample.Models;
using Fluid.MvcViewEngine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fluid.MvcSample
{
    public class Startup
    {
        static Startup()
        {
            TemplateContext.GlobalMemberAccessStrategy.Register<Person>();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<FluidViewEngineOptions>(x => x.Parser = p => 
                p.RegisterEmptyBlock("mytag", static async (s, w, e, c) =>
                {
                    await w.WriteAsync("Hello from MyTag");

                    return Completion.Normal;
                }
            ));

            services.AddMvc().AddFluid();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(routes =>
            {
                routes.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}

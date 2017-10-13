Imports Microsoft.Extensions.DependencyInjection
Imports FluidMvcViewEngine
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.Extensions.Logging

Public Class Startup
    Shared Sub New()
        TemplateContext.GlobalMemberAccessStrategy.Register(Of Person)()
    End Sub

    Public Sub ConfigureServices(services As IServiceCollection)
        services.AddMvc().AddFluid()
    End Sub

    Public Sub Configure(app As IApplicationBuilder, env As IHostingEnvironment, loggerFactory As ILoggerFactory)
        If env.IsDevelopment() Then
            app.UseDeveloperExceptionPage()
        End If

        app.UseMvc(Sub(routes)
                       routes.MapRoute(name:="default",
                                        template:="{controller=Home}/{action=Index}/{id?}")
                   End Sub)
    End Sub
End Class

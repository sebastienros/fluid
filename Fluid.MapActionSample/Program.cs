using Fluid.MapActionViewEngine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.ConfigureServices(services =>
        {
            services.AddFluid();
        });

        webBuilder.Configure(app =>
        {
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                IResult ViewAsync()
                {
                    var todo = new Todo(1, "Go back to work!", false);
                    return new ActionViewResult("index", todo);
                };

                endpoints.MapGet("/", (Func<IResult>)ViewAsync);

            });

        });
    })
    .Build();

await host.StartAsync();

await host.WaitForShutdownAsync();

record Todo(int Id, string Name, bool IsComplete);
using Fluid.MvcViewEngine;
using Fluid.Tests.Mocks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests.MvcViewEngine
{
    public class FunctionalServerTests
    {
        [Fact]
        public async Task KestrelWithSynchronousIoDisabled_ShouldRenderView()
        {
            var expected = new string('b', 4096);
            var mockFileProvider = new MockFileProvider();
            mockFileProvider.Add("Home/Index.liquid", "{{ Model }}");

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseKestrel(options => options.AllowSynchronousIO = false);
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            builder.Services.PostConfigure<FluidMvcViewOptions>(options =>
            {
                options.TemplateOptions.OutputBufferSize = 16;
                options.ViewsFileProvider = mockFileProvider;
                options.PartialsFileProvider = mockFileProvider;
            });

            builder.Services
                .AddControllersWithViews()
                .AddApplicationPart(typeof(FunctionalServerTests).Assembly)
                .AddFluid();

            var app = builder.Build();
            app.UseRouting();
            app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

            await app.StartAsync();

            try
            {
                var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
                var serverAddress = Assert.Single(addresses);

                using var client = new HttpClient { BaseAddress = new Uri(serverAddress) };
                var response = await client.GetAsync("/Home/Index");
                var responseBody = await response.Content.ReadAsStringAsync();

                Assert.True(response.IsSuccessStatusCode, $"Response status code was {(int) response.StatusCode}. Body: {responseBody}");
                Assert.Equal(expected, responseBody);
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }

    }

    public class HomeController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View("Index", new string('b', 4096));
        }
    }
}

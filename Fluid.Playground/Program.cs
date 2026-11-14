using Fluid.Playground;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddSingleton<PlaygroundEngine>();
builder.Services.AddSingleton<PlaygroundHighlighter>();

await builder.Build().RunAsync();

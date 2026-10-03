using Fluid;
using Fluid.MvcSample;
using Fluid.MvcViewEngine;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.Configure<FluidMvcViewOptions>(options =>
{
    options.Parser = new CustomFluidViewParser(new FluidParserOptions());
});

builder.Services.AddControllersWithViews();
builder.Services.AddMvc().AddFluid();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

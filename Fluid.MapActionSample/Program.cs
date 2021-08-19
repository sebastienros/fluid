var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddFluid();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapGet("/", () =>
{
    return LiquidResults.View("index", new Todo(1, "Go back to work!", false));
});

app.Run();

record Todo(int Id, string Name, bool IsComplete);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddFluid();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapGet("/", () => Results.Extensions.View("Index", new Todo(1, "Go back to work!", false)));

await app.RunAsync();

record Todo(int Id, string Name, bool IsComplete);

using BarbershopApi.Data;
using Microsoft.EntityFrameworkCore;

const string VitePolicy = "VitePolicy";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy(VitePolicy, policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var connectionString = builder.Configuration.GetConnectionString("BarbershopDb")
    ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "App_Data", "barbershop.db")}";

builder.Services.AddDbContext<BarbershopDbContext>(options => options.UseSqlite(connectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<BarbershopDbContext>().Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(VitePolicy);

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;

using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Infrastructure.Persistence;
using ArtCommission.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register DbContext (SQL Server or InMemory fallback)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connStr))
    {
        options.UseSqlServer(connStr);
    }
    else
    {
        options.UseInMemoryDatabase("ArtCommissionDb");
    }
});

// Register Application Services
builder.Services.AddScoped<ICommissionService, CommissionService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "openapi/{documentName}.json";
    });

    app.UseSwaggerUI();

    app.MapScalarApiReference(options =>
    {
        options.WithTitle("ArtCommission API Reference")
               .WithTheme(ScalarTheme.Purple);
        options.EndpointPathPrefix = "/scalar/{documentName}";
        options.OpenApiRoutePattern = "/openapi/v1.json";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

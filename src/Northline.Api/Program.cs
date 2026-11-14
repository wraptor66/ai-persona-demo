using Microsoft.EntityFrameworkCore;
using Northline.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<NorthlineDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Northline"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services, app.Logger);

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

await app.RunAsync();

/// <summary>Exposed for integration testing.</summary>
public partial class Program { }

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Northline.Api.Data;
using Northline.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<NorthlineDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Northline"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddScoped<ILowStockService, LowStockService>();

// KAN-4 AC-4: JWT bearer authentication. Settings come from Authentication:Schemes:Bearer
// (Authority, ValidAudiences, ValidIssuer). Endpoints opt in with [Authorize]; the rest stay anonymous.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services, app.Logger);

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

await app.RunAsync();

/// <summary>Exposed for integration testing.</summary>
public partial class Program { }

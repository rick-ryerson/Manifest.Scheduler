using Manifest.Scheduler.Infrastructure;
using Manifest.Scheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Infrastructure (EF Core, tenant resolution, repositories, services) ────
builder.Services.AddInfrastructure(builder.Configuration);

// ── API ───────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Manifest Scheduler API",
        Version = "v1",
        Description = "GalacticSenate UDM — Party, Person, and Organization management"
    });
});

// ProblemDetails support for standardised error responses (RFC 7807)
builder.Services.AddProblemDetails();

var app = builder.Build();

// ── Database migration ────────────────────────────────────────────────────
// Auto-apply pending EF Core migrations on startup.
// This is safe for containerised deployments; in production consider a
// dedicated migration job or a startup health-check gate instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// ── Middleware ────────────────────────────────────────────────────────────
// Swagger is enabled in all environments so the containerised API is
// explorable at http://localhost:8080/swagger without needing to set
// ASPNETCORE_ENVIRONMENT=Development.
app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Manifest Scheduler v1"));

app.UseHttpsRedirection();
app.UseExceptionHandler();   // global handler — converts unhandled exceptions to ProblemDetails
app.MapControllers();

app.Run();

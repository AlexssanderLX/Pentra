using Microsoft.AspNetCore.Mvc;
using Pentra.Application;
using Pentra.Infrastructure;
using Pentra.Infrastructure.Persistence;
using Pentra.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// MVC with global antiforgery validation on every unsafe (POST/PUT/DELETE) request.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // With nullable reference types on, non-nullable string properties would be
    // treated as implicitly [Required]. Optional fields (notes, descriptions) use
    // non-nullable strings, so disable that; explicit [Required] still applies.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// HTTPS redirection is intentionally not forced: the container is served over
// plain HTTP on :8080 behind the operator's own TLS/reverse proxy if desired.

app.UseSecurityHeaders();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Apply migrations and seed reference data on startup. Retries while the runner
// creates and relaxes the shared database file (in the Docker deployment).
await DatabaseInitializer.InitializeWithRetryAsync(app.Services);

app.Run();

// Exposed so WebApplicationFactory-based integration tests can reference the entry point.
public partial class Program;

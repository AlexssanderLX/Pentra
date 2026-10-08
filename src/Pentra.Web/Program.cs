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

// Apply migrations and seed reference data (phases + tool catalog) on startup.
await DatabaseInitializer.InitializeAsync(app.Services);

app.Run();

// Exposed so WebApplicationFactory-based integration tests can reference the entry point.
public partial class Program;

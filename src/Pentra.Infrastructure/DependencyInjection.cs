using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pentra.Application.Abstractions;
using Pentra.Domain.Abstractions;
using Pentra.Infrastructure.Persistence;
using Pentra.Infrastructure.Time;

namespace Pentra.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pentra")
                               ?? "Data Source=pentra.db";

        services.AddDbContext<PentraDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IPentraDbContext>(sp => sp.GetRequiredService<PentraDbContext>());
        services.AddSingleton<ISystemClock, SystemClock>();

        return services;
    }
}

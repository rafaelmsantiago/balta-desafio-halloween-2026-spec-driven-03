using Encurtador.Application.Links;
using Encurtador.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Encurtador.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("Links");

        // Único ponto de troca de provider (research.md D6).
        switch (provider)
        {
            case "Sqlite":
                services.AddDbContext<LinksDbContext>(o => o.UseSqlite(connectionString));
                break;
            default:
                throw new InvalidOperationException(
                    $"Database:Provider '{provider}' não é suportado. Valores aceitos: Sqlite.");
        }

        services.AddScoped<ILinkRepository, EfLinkRepository>();
        return services;
    }

    public static async Task ApplyMigrationsAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LinksDbContext>();
        await db.Database.MigrateAsync();
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Encurtador.IntegrationTests.Support;

/// <summary>Host real da API sobre um arquivo SQLite temporário próprio.</summary>
public class LinksApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly bool ownsDatabase;

    public LinksApiFactory()
        : this(null)
    {
    }

    internal LinksApiFactory(string? databasePath)
    {
        ownsDatabase = databasePath is null;
        DatabasePath = databasePath ?? NewDatabasePath();
    }

    public string DatabasePath { get; }

    public MutableTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero));

    public static string NewDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"encurtador-{Guid.NewGuid():N}.db");

    public static void DeleteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { path, $"{path}-shm", $"{path}-wal" })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    public HttpClient CreateNoRedirectClient() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Links", $"Data Source={DatabasePath}");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (ownsDatabase)
        {
            DeleteDatabase(DatabasePath);
        }

        GC.SuppressFinalize(this);
    }
}

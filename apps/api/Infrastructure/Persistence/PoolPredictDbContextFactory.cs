using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System.Data.Common;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PoolPredictDbContextFactory : IDesignTimeDbContextFactory<PoolPredictDbContext>
{
    public PoolPredictDbContext CreateDbContext(string[] args)
    {
        var basePath = ResolveConfigurationBasePath();
        var environment = ResolveEnvironment(basePath);
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var connectionString = configuration.GetConnectionString("MariaDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:MariaDb is required for EF design-time commands. " +
                "Set ConnectionStrings__MariaDb, create appsettings.Production.json, or pass --ConnectionStrings:MariaDb.");
        }

        ValidateConnectionString(connectionString);

        var options = new DbContextOptionsBuilder<PoolPredictDbContext>()
            .UseMySql(
                connectionString,
                ServerVersion.Create(new Version(12, 0, 0), ServerType.MariaDb),
                mySqlOptions => mySqlOptions.MigrationsHistoryTable(PoolPredictDatabaseOptions.MigrationsHistoryTable))
            .Options;

        return new PoolPredictDbContext(options);
    }

    private static string ResolveConfigurationBasePath()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        if (HasAppSettings(currentDirectory))
        {
            return currentDirectory;
        }

        var apiDirectory = Path.Combine(currentDirectory, "apps", "api");
        if (HasAppSettings(apiDirectory))
        {
            return apiDirectory;
        }

        return currentDirectory;
    }

    private static string ResolveEnvironment(string basePath)
    {
        var configuredEnvironment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        if (!string.IsNullOrWhiteSpace(configuredEnvironment))
        {
            return configuredEnvironment;
        }

        if (File.Exists(Path.Combine(basePath, "appsettings.Production.json")))
        {
            return "Production";
        }

        if (File.Exists(Path.Combine(basePath, "appsettings.Development.json")))
        {
            return "Development";
        }

        return "Production";
    }

    private static bool HasAppSettings(string directory) =>
        Directory.Exists(directory)
        && Directory.EnumerateFiles(directory, "appsettings*.json").Any();

    private static void ValidateConnectionString(string connectionString)
    {
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        if (!builder.TryGetValue("Database", out var database)
            || string.IsNullOrWhiteSpace(database?.ToString()))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:MariaDb must include a non-empty Database value.");
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PoolPredictDbContextFactory : IDesignTimeDbContextFactory<PoolPredictDbContext>
{
    public PoolPredictDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__MariaDb")
            ?? "Server=127.0.0.1;Port=3308;Database=pool_predict;User=root02;Password=123;";

        var options = new DbContextOptionsBuilder<PoolPredictDbContext>()
            .UseMySql(
                connectionString,
                ServerVersion.Create(new Version(12, 0, 0), ServerType.MariaDb))
            .Options;

        return new PoolPredictDbContext(options);
    }
}

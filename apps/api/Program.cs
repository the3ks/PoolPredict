using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Infrastructure.Persistence;
using PoolPredict.Api.Modules.Identity;
using PoolPredict.Api.Modules.Pools;
using PoolPredict.Api.Modules.Predictions;
using PoolPredict.Api.Modules.Tournaments;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton<IEventProvider, MockEventProvider>();
var mariaDbConnectionString = builder.Configuration.GetConnectionString("MariaDb");
if (string.IsNullOrWhiteSpace(mariaDbConnectionString))
{
    builder.Services.AddSingleton<ITournamentPersistence, NoOpTournamentPersistence>();
}
else
{
    builder.Services.AddDbContextFactory<PoolPredictDbContext>(options =>
        options.UseMySql(
            mariaDbConnectionString,
            ServerVersion.Create(new Version(12, 0, 0), ServerType.MariaDb)));
    builder.Services.AddSingleton<ITournamentPersistence, EfTournamentPersistence>();
}

builder.Services.AddSingleton<TournamentCatalog>();
builder.Services.AddSingleton<IdentityStore>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<PoolStore>();
builder.Services.AddSingleton<PredictionStore>();

builder.Services
    .AddAuthentication(JwtBearerAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, JwtBearerAuthenticationHandler>(
        JwtBearerAuthenticationHandler.SchemeName,
        options => { });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithOrigins("http://localhost:3000");
    });
});

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(mariaDbConnectionString))
{
    using var scope = app.Services.CreateScope();
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PoolPredictDbContext>>();
    using var db = dbFactory.CreateDbContext();
    db.Database.Migrate();
    _ = app.Services.GetRequiredService<TournamentCatalog>();
    _ = app.Services.GetRequiredService<IdentityStore>();
    _ = app.Services.GetRequiredService<PoolStore>();
    _ = app.Services.GetRequiredService<PredictionStore>();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "PoolPredict API",
    utcNow = DateTimeOffset.UtcNow
}));

app.MapTournamentEndpoints();
app.MapIdentityEndpoints();
app.MapPoolEndpoints();
app.MapPredictionEndpoints();

app.Run();

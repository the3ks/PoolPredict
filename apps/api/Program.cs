using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Infrastructure.Persistence;
using PoolPredict.Api.Modules.Admin;
using PoolPredict.Api.Modules.Email;
using PoolPredict.Api.Modules.Identity;
using PoolPredict.Api.Modules.Markets;
using PoolPredict.Api.Modules.Pools;
using PoolPredict.Api.Modules.Predictions;
using PoolPredict.Api.Modules.Settlement;
using PoolPredict.Api.Modules.Tournaments;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.Configure<EventProviderOptions>(builder.Configuration.GetSection("EventProvider"));
builder.Services.Configure<MarketOptions>(builder.Configuration.GetSection("Markets"));
builder.Services.AddSingleton<MockEventProvider>();
builder.Services.AddHttpClient<FootballDataProvider>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<EventProviderOptions>>().Value.FootballData;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
});
builder.Services.AddHttpClient<VirtualProviderEventProvider>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<EventProviderOptions>>().Value.VirtualProvider;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
});
builder.Services.AddSingleton<EventProviderFactory>();
var mariaDbConnectionString = builder.Configuration.GetConnectionString("MariaDb");
if (string.IsNullOrWhiteSpace(mariaDbConnectionString))
{
    throw new InvalidOperationException("ConnectionStrings:MariaDb is required. Apply EF migrations manually before starting the API.");
}

builder.Services.AddDbContextFactory<PoolPredictDbContext>(options =>
    options.UseMySql(
        mariaDbConnectionString,
        ServerVersion.Create(new Version(12, 0, 0), ServerType.MariaDb),
        mySqlOptions => mySqlOptions.MigrationsHistoryTable(PoolPredictDatabaseOptions.MigrationsHistoryTable)));
builder.Services.AddSingleton<ITournamentPersistence, EfTournamentPersistence>();

builder.Services.AddSingleton<TournamentCatalog>();
builder.Services.AddSingleton<TournamentSyncJob>();
builder.Services.AddSingleton<EmailSettingsStore>();
builder.Services.AddSingleton<SmtpEmailSender>();
builder.Services.AddSingleton<IdentityStore>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<PayoutConfigurationStore>();
builder.Services.AddSingleton<PoolStore>();
builder.Services.AddSingleton<PredictionStore>();
builder.Services.AddSingleton<SettlementCalculator>();
builder.Services.AddSingleton<SettlementService>();

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

_ = app.Services.GetRequiredService<TournamentCatalog>();
_ = app.Services.GetRequiredService<IdentityStore>();
_ = app.Services.GetRequiredService<PoolStore>();
_ = app.Services.GetRequiredService<PredictionStore>();

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
app.MapAdminEndpoints();
app.MapMarketEndpoints();
app.MapPoolEndpoints();
app.MapPredictionEndpoints();
app.MapSettlementEndpoints();

app.Run();

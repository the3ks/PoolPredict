using PoolPredict.Api.Modules.Pools;
using PoolPredict.Api.Modules.Predictions;
using PoolPredict.Api.Modules.Tournaments;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton<TournamentCatalog>();
builder.Services.AddSingleton<PoolStore>();
builder.Services.AddSingleton<PredictionStore>();

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

app.UseCors();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "PoolPredict API",
    utcNow = DateTimeOffset.UtcNow
}));

app.MapTournamentEndpoints();
app.MapPoolEndpoints();
app.MapPredictionEndpoints();

app.Run();

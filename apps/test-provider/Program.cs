using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<VirtualProviderStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "Virtual Sports Data Provider",
    utcNow = DateTimeOffset.UtcNow
}))
    .WithTags("Provider");

app.MapGet("/api/tournaments", async (VirtualProviderStore store, CancellationToken cancellationToken) =>
        Results.Ok(await store.GetTournamentsAsync(cancellationToken)))
    .WithTags("Provider");

app.MapGet("/api/tournaments/{externalTournamentId}/participants", async (
        string externalTournamentId,
        VirtualProviderStore store,
        CancellationToken cancellationToken) =>
    {
        var participants = await store.GetParticipantsAsync(externalTournamentId, cancellationToken);
        return participants.Length == 0 ? Results.NotFound() : Results.Ok(participants);
    })
    .WithTags("Provider");

app.MapGet("/api/tournaments/{externalTournamentId}/events", async (
        string externalTournamentId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        VirtualProviderStore store,
        CancellationToken cancellationToken) =>
    {
        var events = await store.GetEventsAsync(externalTournamentId, from, to, cancellationToken);
        return events.Length == 0 ? Results.NotFound() : Results.Ok(events);
    })
    .WithTags("Provider");

app.MapGet("/api/test/events", async (VirtualProviderStore store, CancellationToken cancellationToken) =>
        Results.Ok(await store.GetAllEventsAsync(cancellationToken)))
    .WithTags("Test control");

app.MapGet("/api/test/events/{externalEventId}", async (
        string externalEventId,
        VirtualProviderStore store,
        CancellationToken cancellationToken) =>
    {
        var matchEvent = await store.GetEventAsync(externalEventId, cancellationToken);
        return matchEvent is null ? Results.NotFound() : Results.Ok(matchEvent);
    })
    .WithTags("Test control");

app.MapPut("/api/test/events/{externalEventId}", async (
        string externalEventId,
        UpdateEventRequest request,
        VirtualProviderStore store,
        CancellationToken cancellationToken) =>
    {
        var matchEvent = await store.UpdateEventAsync(externalEventId, request, cancellationToken);
        return matchEvent is null ? Results.NotFound() : Results.Ok(matchEvent);
    })
    .WithTags("Test control");

app.MapPost("/api/test/events/{externalEventId}/start", async (
        string externalEventId,
        VirtualProviderStore store,
        CancellationToken cancellationToken) =>
    {
        var matchEvent = await store.StartEventAsync(externalEventId, cancellationToken);
        return matchEvent is null ? Results.NotFound() : Results.Ok(matchEvent);
    })
    .WithTags("Test control");

app.MapPost("/api/test/events/{externalEventId}/finish", async (
        string externalEventId,
        FinishEventRequest request,
        VirtualProviderStore store,
        CancellationToken cancellationToken) =>
    {
        var matchEvent = await store.FinishEventAsync(externalEventId, request, cancellationToken);
        return matchEvent is null ? Results.NotFound() : Results.Ok(matchEvent);
    })
    .WithTags("Test control");

app.MapPost("/api/test/events/{externalEventId}/postpone", async (
        string externalEventId,
        PostponeEventRequest request,
        VirtualProviderStore store,
        CancellationToken cancellationToken) =>
    {
        var matchEvent = await store.PostponeEventAsync(externalEventId, request, cancellationToken);
        return matchEvent is null ? Results.NotFound() : Results.Ok(matchEvent);
    })
    .WithTags("Test control");

app.MapPost("/api/test/events/{externalEventId}/cancel", async (
        string externalEventId,
        VirtualProviderStore store,
        CancellationToken cancellationToken) =>
    {
        var matchEvent = await store.CancelEventAsync(externalEventId, cancellationToken);
        return matchEvent is null ? Results.NotFound() : Results.Ok(matchEvent);
    })
    .WithTags("Test control");

app.MapPost("/api/test/reset", async (VirtualProviderStore store, CancellationToken cancellationToken) =>
        Results.Ok(await store.ResetAsync(cancellationToken)))
    .WithTags("Test control");

app.Run();

public sealed class VirtualProviderStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _statePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public VirtualProviderStore(IHostEnvironment environment)
    {
        var dataPath = Path.Combine(environment.ContentRootPath, "data");
        Directory.CreateDirectory(dataPath);
        _statePath = Path.Combine(dataPath, "virtual-provider-state.json");
    }

    public async Task<TournamentResponse[]> GetTournamentsAsync(CancellationToken cancellationToken)
    {
        var state = await LoadAsync(cancellationToken);
        return state.Tournaments.ToArray();
    }

    public async Task<ParticipantResponse[]> GetParticipantsAsync(string tournamentExternalId, CancellationToken cancellationToken)
    {
        var state = await LoadAsync(cancellationToken);
        return state.Participants
            .Where(participant => string.Equals(participant.TournamentExternalId, tournamentExternalId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public async Task<EventResponse[]> GetEventsAsync(
        string tournamentExternalId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var state = await LoadAsync(cancellationToken);
        return state.Events
            .Where(matchEvent => string.Equals(matchEvent.TournamentExternalId, tournamentExternalId, StringComparison.OrdinalIgnoreCase))
            .Where(matchEvent => from is null || matchEvent.StartsAt >= from)
            .Where(matchEvent => to is null || matchEvent.StartsAt <= to)
            .OrderBy(matchEvent => matchEvent.StartsAt)
            .ToArray();
    }

    public async Task<EventResponse[]> GetAllEventsAsync(CancellationToken cancellationToken)
    {
        var state = await LoadAsync(cancellationToken);
        return state.Events.OrderBy(matchEvent => matchEvent.StartsAt).ToArray();
    }

    public async Task<EventResponse?> GetEventAsync(string externalEventId, CancellationToken cancellationToken)
    {
        var state = await LoadAsync(cancellationToken);
        return state.Events.SingleOrDefault(matchEvent =>
            string.Equals(matchEvent.ExternalId, externalEventId, StringComparison.OrdinalIgnoreCase));
    }

    public Task<EventResponse?> UpdateEventAsync(string externalEventId, UpdateEventRequest request, CancellationToken cancellationToken) =>
        MutateEventAsync(externalEventId, matchEvent =>
        {
            matchEvent.StartsAt = request.StartsAt;
            matchEvent.Status = request.Status;
            matchEvent.FirstHalfHomeScore = request.FirstHalfHomeScore;
            matchEvent.FirstHalfAwayScore = request.FirstHalfAwayScore;
            matchEvent.FullTimeHomeScore = request.FullTimeHomeScore;
            matchEvent.FullTimeAwayScore = request.FullTimeAwayScore;
        }, cancellationToken);

    public Task<EventResponse?> StartEventAsync(string externalEventId, CancellationToken cancellationToken) =>
        MutateEventAsync(externalEventId, matchEvent =>
        {
            matchEvent.Status = EventStatus.Live;
        }, cancellationToken);

    public Task<EventResponse?> FinishEventAsync(string externalEventId, FinishEventRequest request, CancellationToken cancellationToken) =>
        MutateEventAsync(externalEventId, matchEvent =>
        {
            matchEvent.Status = EventStatus.Finished;
            matchEvent.FirstHalfHomeScore = request.FirstHalfHomeScore;
            matchEvent.FirstHalfAwayScore = request.FirstHalfAwayScore;
            matchEvent.FullTimeHomeScore = request.FullTimeHomeScore;
            matchEvent.FullTimeAwayScore = request.FullTimeAwayScore;
        }, cancellationToken);

    public Task<EventResponse?> PostponeEventAsync(string externalEventId, PostponeEventRequest request, CancellationToken cancellationToken) =>
        MutateEventAsync(externalEventId, matchEvent =>
        {
            matchEvent.Status = EventStatus.Postponed;
            if (request.StartsAt is not null)
            {
                matchEvent.StartsAt = request.StartsAt.Value;
            }
        }, cancellationToken);

    public Task<EventResponse?> CancelEventAsync(string externalEventId, CancellationToken cancellationToken) =>
        MutateEventAsync(externalEventId, matchEvent =>
        {
            matchEvent.Status = EventStatus.Cancelled;
        }, cancellationToken);

    public async Task<ProviderState> ResetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = CreateSeedState();
            await SaveCoreAsync(state, cancellationToken);
            return state;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<EventResponse?> MutateEventAsync(
        string externalEventId,
        Action<EventResponse> mutate,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadCoreAsync(cancellationToken);
            var matchEvent = state.Events.SingleOrDefault(candidate =>
                string.Equals(candidate.ExternalId, externalEventId, StringComparison.OrdinalIgnoreCase));
            if (matchEvent is null)
            {
                return null;
            }

            mutate(matchEvent);
            await SaveCoreAsync(state, cancellationToken);
            return matchEvent;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ProviderState> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await LoadCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ProviderState> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_statePath))
        {
            var seedState = CreateSeedState();
            await SaveCoreAsync(seedState, cancellationToken);
            return seedState;
        }

        await using var stream = File.OpenRead(_statePath);
        return await JsonSerializer.DeserializeAsync<ProviderState>(stream, SerializerOptions, cancellationToken)
            ?? CreateSeedState();
    }

    private async Task SaveCoreAsync(ProviderState state, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_statePath);
        await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
    }

    private static ProviderState CreateSeedState()
    {
        const string tournamentId = "virtual-wc-2026";
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var startsAt = new DateTimeOffset(today.ToDateTime(new TimeOnly(16, 0), DateTimeKind.Utc));
        var teams = new[]
        {
            new ParticipantResponse("virtual-wc-2026-team-arg", tournamentId, "Argentina", "ARG", "Argentina"),
            new ParticipantResponse("virtual-wc-2026-team-bra", tournamentId, "Brazil", "BRA", "Brazil"),
            new ParticipantResponse("virtual-wc-2026-team-can", tournamentId, "Canada", "CAN", "Canada"),
            new ParticipantResponse("virtual-wc-2026-team-eng", tournamentId, "England", "ENG", "England"),
            new ParticipantResponse("virtual-wc-2026-team-fra", tournamentId, "France", "FRA", "France"),
            new ParticipantResponse("virtual-wc-2026-team-ger", tournamentId, "Germany", "GER", "Germany"),
            new ParticipantResponse("virtual-wc-2026-team-ita", tournamentId, "Italy", "ITA", "Italy"),
            new ParticipantResponse("virtual-wc-2026-team-jpn", tournamentId, "Japan", "JPN", "Japan"),
            new ParticipantResponse("virtual-wc-2026-team-kor", tournamentId, "Korea Republic", "KOR", "Korea Republic"),
            new ParticipantResponse("virtual-wc-2026-team-mar", tournamentId, "Morocco", "MAR", "Morocco"),
            new ParticipantResponse("virtual-wc-2026-team-mex", tournamentId, "Mexico", "MEX", "Mexico"),
            new ParticipantResponse("virtual-wc-2026-team-ned", tournamentId, "Netherlands", "NED", "Netherlands"),
            new ParticipantResponse("virtual-wc-2026-team-por", tournamentId, "Portugal", "POR", "Portugal"),
            new ParticipantResponse("virtual-wc-2026-team-esp", tournamentId, "Spain", "ESP", "Spain"),
            new ParticipantResponse("virtual-wc-2026-team-usa", tournamentId, "United States", "USA", "United States"),
            new ParticipantResponse("virtual-wc-2026-team-uru", tournamentId, "Uruguay", "URU", "Uruguay")
        };

        var events = new List<EventResponse>();
        for (var index = 0; index < 24; index++)
        {
            var home = teams[(index * 2) % teams.Length];
            var away = teams[((index * 2) + 1) % teams.Length];
            var dayOffset = index / 3;
            var slotOffset = index % 3;
            events.Add(new EventResponse(
                $"virtual-wc-2026-event-{index + 1:000}",
                tournamentId,
                home.ExternalId,
                away.ExternalId,
                startsAt.AddDays(dayOffset).AddHours(slotOffset * 3),
                EventStatus.Scheduled));
        }

        return new ProviderState(
            [new TournamentResponse(tournamentId, "Virtual WC 2026", "Football", today, today.AddDays(30))],
            teams.ToList(),
            events);
    }
}

public sealed record ProviderState(
    List<TournamentResponse> Tournaments,
    List<ParticipantResponse> Participants,
    List<EventResponse> Events);

public sealed record TournamentResponse(
    string ExternalId,
    string Name,
    string Sport,
    DateOnly StartsOn,
    DateOnly EndsOn);

public sealed record ParticipantResponse(
    string ExternalId,
    string TournamentExternalId,
    string Name,
    string Code,
    string Country);

public sealed class EventResponse(
    string externalId,
    string tournamentExternalId,
    string homeParticipantExternalId,
    string awayParticipantExternalId,
    DateTimeOffset startsAt,
    EventStatus status)
{
    public string ExternalId { get; set; } = externalId;

    public string TournamentExternalId { get; set; } = tournamentExternalId;

    public string HomeParticipantExternalId { get; set; } = homeParticipantExternalId;

    public string AwayParticipantExternalId { get; set; } = awayParticipantExternalId;

    public DateTimeOffset StartsAt { get; set; } = startsAt;

    public EventStatus Status { get; set; } = status;

    public int? FirstHalfHomeScore { get; set; }

    public int? FirstHalfAwayScore { get; set; }

    public int? FullTimeHomeScore { get; set; }

    public int? FullTimeAwayScore { get; set; }
}

public sealed record UpdateEventRequest(
    DateTimeOffset StartsAt,
    EventStatus Status,
    int? FirstHalfHomeScore,
    int? FirstHalfAwayScore,
    int? FullTimeHomeScore,
    int? FullTimeAwayScore);

public sealed record FinishEventRequest(
    int FullTimeHomeScore,
    int FullTimeAwayScore,
    int? FirstHalfHomeScore,
    int? FirstHalfAwayScore);

public sealed record PostponeEventRequest(DateTimeOffset? StartsAt);

public enum EventStatus
{
    Scheduled,
    Live,
    Finished,
    Postponed,
    Cancelled
}

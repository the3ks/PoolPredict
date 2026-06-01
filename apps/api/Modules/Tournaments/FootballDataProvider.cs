using Microsoft.Extensions.Options;
using PoolPredict.Api.Domain.Tournaments;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoolPredict.Api.Modules.Tournaments;

public sealed class FootballDataProvider(HttpClient httpClient, IOptions<EventProviderOptions> options) : IEventProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly FootballDataOptions _options = options.Value.FootballData;

    public Task SyncTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IEnumerable<TournamentDto>> GetTournamentsAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var tournament = new TournamentDto(
            _options.CompetitionCode,
            _options.TournamentName,
            _options.Sport,
            _options.StartsOn,
            _options.EndsOn);

        return Task.FromResult<IEnumerable<TournamentDto>>([tournament]);
    }

    public async Task<IEnumerable<ParticipantDto>> GetParticipantsAsync(string tournamentExternalId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var response = await GetJsonAsync<FootballDataTeamsResponse>(
            $"competitions/{_options.CompetitionCode}/teams?season={_options.Season}",
            cancellationToken);

        return response.Teams.Where(team => team.Id is not null).Select(team => new ParticipantDto(
            team.Id!.Value.ToString(),
            tournamentExternalId,
            team.Name,
            string.IsNullOrWhiteSpace(team.Tla) ? team.ShortName : team.Tla,
            team.Area?.Name ?? team.Name));
    }

    public async Task<IEnumerable<EventDto>> GetEventsAsync(string tournamentExternalId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var response = await GetJsonAsync<FootballDataMatchesResponse>(
            $"competitions/{_options.CompetitionCode}/matches?season={_options.Season}",
            cancellationToken);

        return response.Matches
            .Where(match => match.HomeTeam?.Id is not null && match.AwayTeam?.Id is not null)
            .Select(match => new EventDto(
                match.Id.ToString(),
                tournamentExternalId,
                match.HomeTeam!.Id!.Value.ToString(),
                match.AwayTeam!.Id!.Value.ToString(),
                match.UtcDate ?? _options.StartsOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                MapStatus(match.Status)));
    }

    public async Task<IEnumerable<EventDto>> GetLiveEventsAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var response = await GetJsonAsync<FootballDataMatchesResponse>(
            $"competitions/{_options.CompetitionCode}/matches?season={_options.Season}&status=LIVE",
            cancellationToken);

        return response.Matches
            .Where(match => match.HomeTeam?.Id is not null && match.AwayTeam?.Id is not null)
            .Select(match => new EventDto(
                match.Id.ToString(),
                _options.CompetitionCode,
                match.HomeTeam!.Id!.Value.ToString(),
                match.AwayTeam!.Id!.Value.ToString(),
                match.UtcDate ?? DateTimeOffset.UtcNow,
                MapStatus(match.Status)));
    }

    public async Task<EventDto?> GetEventAsync(string externalId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var response = await GetJsonAsync<FootballDataMatchResponse>($"matches/{externalId}", cancellationToken);
        var match = response.Match;
        if (match?.HomeTeam?.Id is null || match.AwayTeam?.Id is null)
        {
            return null;
        }

        return new EventDto(
            match.Id.ToString(),
            _options.CompetitionCode,
            match.HomeTeam.Id.Value.ToString(),
            match.AwayTeam.Id.Value.ToString(),
            match.UtcDate ?? DateTimeOffset.UtcNow,
            MapStatus(match.Status));
    }

    private async Task<T> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Auth-Token", _options.ApiToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Football-data response could not be parsed.");
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            throw new InvalidOperationException("FootballData ApiToken is required when EventProvider:Provider is FootballData.");
        }
    }

    private static EventStatus MapStatus(string? status) =>
        status?.ToUpperInvariant() switch
        {
            "IN_PLAY" or "PAUSED" or "LIVE" => EventStatus.Live,
            "FINISHED" => EventStatus.Finished,
            "POSTPONED" => EventStatus.Postponed,
            "CANCELLED" or "CANCELED" => EventStatus.Cancelled,
            _ => EventStatus.Scheduled
        };

    private sealed record FootballDataTeamsResponse(FootballDataTeam[] Teams);

    private sealed record FootballDataMatchesResponse(FootballDataMatch[] Matches);

    private sealed record FootballDataMatchResponse([property: JsonPropertyName("match")] FootballDataMatch? Match);

    private sealed record FootballDataArea(string? Name);

    private sealed record FootballDataTeam(int? Id, string Name, string ShortName, string Tla, FootballDataArea? Area);

    private sealed record FootballDataMatch(int Id, DateTimeOffset? UtcDate, string? Status, FootballDataTeam? HomeTeam, FootballDataTeam? AwayTeam);
}

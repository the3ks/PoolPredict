using PoolPredict.Api.Domain.Tournaments;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoolPredict.Api.Modules.Tournaments;

public sealed class VirtualProviderEventProvider(HttpClient httpClient) : IEventProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public Task SyncTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<IEnumerable<TournamentDto>> GetTournamentsAsync(CancellationToken cancellationToken = default) =>
        await GetJsonAsync<TournamentDto[]>("/api/tournaments", cancellationToken);

    public async Task<IEnumerable<ParticipantDto>> GetParticipantsAsync(string tournamentExternalId, CancellationToken cancellationToken = default) =>
        await GetJsonAsync<ParticipantDto[]>($"/api/tournaments/{Uri.EscapeDataString(tournamentExternalId)}/participants", cancellationToken);

    public async Task<IEnumerable<EventDto>> GetEventsAsync(string tournamentExternalId, CancellationToken cancellationToken = default) =>
        await GetJsonAsync<EventDto[]>($"/api/tournaments/{Uri.EscapeDataString(tournamentExternalId)}/events", cancellationToken);

    public async Task<IEnumerable<EventDto>> GetLiveEventsAsync(CancellationToken cancellationToken = default)
    {
        var tournaments = await GetTournamentsAsync(cancellationToken);
        var events = new List<EventDto>();
        foreach (var tournament in tournaments)
        {
            events.AddRange(await GetEventsAsync(tournament.ExternalId, cancellationToken));
        }

        return events.Where(matchEvent => matchEvent.Status == EventStatus.Live);
    }

    public async Task<EventDto?> GetEventAsync(string externalId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/test/events/{Uri.EscapeDataString(externalId)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EventDto>(SerializerOptions, cancellationToken);
    }

    private async Task<T> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Virtual provider response could not be parsed.");
    }
}

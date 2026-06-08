using Microsoft.EntityFrameworkCore;
using PoolPredict.Api.Domain.Common;
using PoolPredict.Api.Domain.Markets;
using PoolPredict.Api.Domain.Pools;
using PoolPredict.Api.Infrastructure.Persistence;

namespace PoolPredict.Api.Modules.Markets;

public sealed class PayoutConfigurationStore
{
    private readonly List<PayoutConfigurationResponse> _configurations = [];
    private readonly IDbContextFactory<PoolPredictDbContext> _dbContextFactory;
    private readonly object _gate = new();

    public PayoutConfigurationStore(IDbContextFactory<PoolPredictDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        LoadOrSeed();
    }

    public PayoutConfigurationResponse GetActiveConfiguration()
    {
        lock (_gate)
        {
            return _configurations.Single(configuration => configuration.IsActive);
        }
    }

    public IReadOnlyCollection<PayoutMarketRuleResponse> GetRules(MarketProfile profile)
    {
        var active = GetActiveConfiguration();
        return active.Rules
            .Where(rule => rule.Profile == profile && rule.IsEnabled)
            .ToArray();
    }

    public IReadOnlyCollection<PayoutConfigurationResponse> GetConfigurations()
    {
        lock (_gate)
        {
            return _configurations.ToArray();
        }
    }

    private void LoadOrSeed()
    {
        using var db = _dbContextFactory.CreateDbContext();
        var persistedConfigurations = db.PayoutConfigurations.AsNoTracking().ToArray();
        if (persistedConfigurations.Length == 0)
        {
            var defaults = CreateDefaultConfiguration();
            db.PayoutConfigurations.Add(new PersistedPayoutConfiguration
            {
                Id = defaults.Id,
                Version = defaults.Version,
                Name = defaults.Name,
                IsActive = defaults.IsActive,
                CreatedAt = DateTimeOffset.UtcNow
            });
            db.PayoutMarketRules.AddRange(defaults.Rules.Select(rule => new PersistedPayoutMarketRule
            {
                Id = rule.Id,
                PayoutConfigurationId = defaults.Id,
                Profile = rule.Profile,
                MarketType = rule.MarketType,
                Period = rule.Period,
                LineValue = rule.LineValue,
                PayoutMultiplier = rule.PayoutMultiplier,
                IsEnabled = rule.IsEnabled
            }));
            db.SaveChanges();
            _configurations.Add(defaults);
            return;
        }

        EnsureCurrentDefaultRules(db);

        var persistedRules = db.PayoutMarketRules.AsNoTracking().ToArray();
        _configurations.AddRange(persistedConfigurations.Select(configuration => new PayoutConfigurationResponse(
            configuration.Id,
            configuration.Version,
            configuration.Name,
            configuration.IsActive,
            persistedRules
                .Where(rule => rule.PayoutConfigurationId == configuration.Id)
                .Select(rule => new PayoutMarketRuleResponse(
                    rule.Id,
                    rule.Profile,
                    rule.MarketType,
                    rule.Period,
                    rule.LineValue,
                    rule.PayoutMultiplier,
                    rule.IsEnabled))
                .ToArray())));
    }

    private static PayoutConfigurationResponse CreateDefaultConfiguration()
    {
        var configurationId = Ids.NewId();
        return new PayoutConfigurationResponse(
            configurationId,
            1,
            "MVP global defaults",
            true,
            CreateDefaultRules().ToArray());
    }

    private static IEnumerable<PayoutMarketRuleResponse> CreateDefaultRules()
    {
        foreach (var period in new[] { MarketPeriod.FullTime })
        {
            yield return Rule(MarketProfile.Casual, MarketType.OneXTwo, period, null, 2.5m);
            yield return Rule(MarketProfile.Casual, MarketType.OverUnder, period, 2.5m, 2.0m);
            yield return Rule(MarketProfile.Casual, MarketType.OddEven, period, null, 2.0m);
            yield return Rule(MarketProfile.Casual, MarketType.CorrectScore, period, null, 5.0m);
        }

        foreach (var profile in new[] { MarketProfile.Standard, MarketProfile.Expert })
        {
            foreach (var period in new[] { MarketPeriod.FullTime, MarketPeriod.FirstHalf })
            {
                if (period == MarketPeriod.FullTime)
                {
                    yield return Rule(profile, MarketType.OneXTwo, period, null, 2.5m);
                }

                yield return Rule(profile, MarketType.Handicap, period, 0.5m, 2.0m);
                yield return Rule(profile, MarketType.OverUnder, period, 2.5m, 2.0m);
                yield return Rule(profile, MarketType.OddEven, period, null, 2.0m);
                yield return Rule(profile, MarketType.CorrectScore, period, null, 5.0m);
            }
        }
    }

    private static PayoutMarketRuleResponse Rule(
        MarketProfile profile,
        MarketType marketType,
        MarketPeriod period,
        decimal? lineValue,
        decimal payoutMultiplier) =>
        new(Ids.NewId(), profile, marketType, period, lineValue, payoutMultiplier, true);

    private static void EnsureCurrentDefaultRules(PoolPredictDbContext db)
    {
        var activeConfiguration = db.PayoutConfigurations.SingleOrDefault(configuration => configuration.IsActive);
        if (activeConfiguration is null)
        {
            return;
        }

        var defaultRules = CreateDefaultRules().ToArray();
        var existingRules = db.PayoutMarketRules
            .Where(rule => rule.PayoutConfigurationId == activeConfiguration.Id)
            .ToArray();
        var existingRuleLookup = existingRules.ToDictionary(
            rule => (rule.Profile, rule.MarketType, rule.Period));

        var missingRules = defaultRules
            .Where(rule => !existingRuleLookup.ContainsKey((rule.Profile, rule.MarketType, rule.Period)))
            .Select(rule => new PersistedPayoutMarketRule
            {
                Id = rule.Id,
                PayoutConfigurationId = activeConfiguration.Id,
                Profile = rule.Profile,
                MarketType = rule.MarketType,
                Period = rule.Period,
                LineValue = rule.LineValue,
                PayoutMultiplier = rule.PayoutMultiplier,
                IsEnabled = rule.IsEnabled
            })
            .ToArray();

        var hasUpdates = false;
        foreach (var defaultRule in defaultRules)
        {
            if (!existingRuleLookup.TryGetValue((defaultRule.Profile, defaultRule.MarketType, defaultRule.Period), out var existingRule))
            {
                continue;
            }

            if (existingRule.LineValue != defaultRule.LineValue)
            {
                existingRule.LineValue = defaultRule.LineValue;
                hasUpdates = true;
            }

            if (existingRule.PayoutMultiplier != defaultRule.PayoutMultiplier)
            {
                existingRule.PayoutMultiplier = defaultRule.PayoutMultiplier;
                hasUpdates = true;
            }

            if (existingRule.IsEnabled != defaultRule.IsEnabled)
            {
                existingRule.IsEnabled = defaultRule.IsEnabled;
                hasUpdates = true;
            }
        }

        if (missingRules.Length > 0)
        {
            db.PayoutMarketRules.AddRange(missingRules);
            hasUpdates = true;
        }

        if (hasUpdates)
        {
            db.SaveChanges();
        }
    }
}

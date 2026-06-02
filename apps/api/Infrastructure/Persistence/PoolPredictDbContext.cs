using Microsoft.EntityFrameworkCore;

namespace PoolPredict.Api.Infrastructure.Persistence;

public sealed class PoolPredictDbContext(DbContextOptions<PoolPredictDbContext> options) : DbContext(options)
{
    public DbSet<PersistedTournament> Tournaments => Set<PersistedTournament>();

    public DbSet<PersistedParticipant> Participants => Set<PersistedParticipant>();

    public DbSet<PersistedEvent> Events => Set<PersistedEvent>();

    public DbSet<PersistedEventResult> EventResults => Set<PersistedEventResult>();

    public DbSet<PersistedUser> Users => Set<PersistedUser>();

    public DbSet<PersistedUserExternalLogin> UserExternalLogins => Set<PersistedUserExternalLogin>();

    public DbSet<PersistedIdentityToken> IdentityTokens => Set<PersistedIdentityToken>();

    public DbSet<PersistedEmailSettings> EmailSettings => Set<PersistedEmailSettings>();

    public DbSet<PersistedPool> Pools => Set<PersistedPool>();

    public DbSet<PersistedPoolMember> PoolMembers => Set<PersistedPoolMember>();

    public DbSet<PersistedPoolInvite> PoolInvites => Set<PersistedPoolInvite>();

    public DbSet<PersistedPoolJoinRequest> PoolJoinRequests => Set<PersistedPoolJoinRequest>();

    public DbSet<PersistedMarket> Markets => Set<PersistedMarket>();

    public DbSet<PersistedPrediction> Predictions => Set<PersistedPrediction>();

    public DbSet<PersistedPointLedgerEntry> PointLedger => Set<PersistedPointLedgerEntry>();

    public DbSet<PersistedPayoutConfiguration> PayoutConfigurations => Set<PersistedPayoutConfiguration>();

    public DbSet<PersistedPayoutMarketRule> PayoutMarketRules => Set<PersistedPayoutMarketRule>();

    public DbSet<PersistedSettlementRun> SettlementRuns => Set<PersistedSettlementRun>();

    public DbSet<PersistedSettlementLog> SettlementLogs => Set<PersistedSettlementLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PersistedTournament>(entity =>
        {
            entity.ToTable("tournaments");
            entity.HasKey(tournament => tournament.Id);
            entity.HasIndex(tournament => new { tournament.Provider, tournament.ExternalId }).IsUnique();
            entity.Property(tournament => tournament.Id).HasColumnName("id");
            entity.Property(tournament => tournament.ExternalId).HasColumnName("external_id").HasMaxLength(128);
            entity.Property(tournament => tournament.Provider).HasColumnName("provider").HasMaxLength(80);
            entity.Property(tournament => tournament.IsTestData).HasColumnName("is_test_data");
            entity.Property(tournament => tournament.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(tournament => tournament.Sport).HasColumnName("sport").HasMaxLength(80);
            entity.Property(tournament => tournament.StartsOn).HasColumnName("starts_on");
            entity.Property(tournament => tournament.EndsOn).HasColumnName("ends_on");
        });

        modelBuilder.Entity<PersistedParticipant>(entity =>
        {
            entity.ToTable("participants");
            entity.HasKey(participant => participant.Id);
            entity.HasIndex(participant => new { participant.TournamentId, participant.Provider, participant.ExternalId }).IsUnique();
            entity.Property(participant => participant.Id).HasColumnName("id");
            entity.Property(participant => participant.TournamentId).HasColumnName("tournament_id");
            entity.Property(participant => participant.ExternalId).HasColumnName("external_id").HasMaxLength(128);
            entity.Property(participant => participant.Provider).HasColumnName("provider").HasMaxLength(80);
            entity.Property(participant => participant.IsTestData).HasColumnName("is_test_data");
            entity.Property(participant => participant.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(participant => participant.Code).HasColumnName("code").HasMaxLength(16);
            entity.Property(participant => participant.Country).HasColumnName("country").HasMaxLength(120);
        });

        modelBuilder.Entity<PersistedEvent>(entity =>
        {
            entity.ToTable("events");
            entity.HasKey(matchEvent => matchEvent.Id);
            entity.HasIndex(matchEvent => new { matchEvent.TournamentId, matchEvent.Provider, matchEvent.ExternalId }).IsUnique();
            entity.HasIndex(matchEvent => matchEvent.StartsAt);
            entity.Property(matchEvent => matchEvent.Id).HasColumnName("id");
            entity.Property(matchEvent => matchEvent.TournamentId).HasColumnName("tournament_id");
            entity.Property(matchEvent => matchEvent.HomeParticipantId).HasColumnName("home_participant_id");
            entity.Property(matchEvent => matchEvent.AwayParticipantId).HasColumnName("away_participant_id");
            entity.Property(matchEvent => matchEvent.ExternalId).HasColumnName("external_id").HasMaxLength(128);
            entity.Property(matchEvent => matchEvent.Provider).HasColumnName("provider").HasMaxLength(80);
            entity.Property(matchEvent => matchEvent.IsTestData).HasColumnName("is_test_data");
            entity.Property(matchEvent => matchEvent.ManagementMode).HasColumnName("management_mode").HasConversion<string>().HasMaxLength(40);
            entity.Property(matchEvent => matchEvent.HomeParticipant).HasColumnName("home_participant").HasMaxLength(200);
            entity.Property(matchEvent => matchEvent.AwayParticipant).HasColumnName("away_participant").HasMaxLength(200);
            entity.Property(matchEvent => matchEvent.StartsAt).HasColumnName("starts_at");
            entity.Property(matchEvent => matchEvent.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        });

        modelBuilder.Entity<PersistedEventResult>(entity =>
        {
            entity.ToTable("event_results");
            entity.HasKey(result => result.Id);
            entity.HasIndex(result => result.EventId).IsUnique();
            entity.Property(result => result.Id).HasColumnName("id");
            entity.Property(result => result.EventId).HasColumnName("event_id");
            entity.Property(result => result.FullTimeHomeScore).HasColumnName("full_time_home_score");
            entity.Property(result => result.FullTimeAwayScore).HasColumnName("full_time_away_score");
            entity.Property(result => result.FirstHalfHomeScore).HasColumnName("first_half_home_score");
            entity.Property(result => result.FirstHalfAwayScore).HasColumnName("first_half_away_score");
            entity.Property(result => result.RecordedAt).HasColumnName("recorded_at");
        });

        modelBuilder.Entity<PersistedUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
            entity.Property(user => user.Id).HasColumnName("id");
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(320);
            entity.Property(user => user.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320);
            entity.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(200);
            entity.Property(user => user.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(40);
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash");
            entity.Property(user => user.CreatedAt).HasColumnName("created_at");
            entity.Property(user => user.EmailVerifiedAt).HasColumnName("email_verified_at");
            entity.Property(user => user.MustChangePassword).HasColumnName("must_change_password");
            entity.Property(user => user.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(user => user.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PersistedUserExternalLogin>(entity =>
        {
            entity.ToTable("user_external_logins");
            entity.HasKey(login => login.Id);
            entity.HasIndex(login => new { login.Provider, login.ProviderUserId }).IsUnique();
            entity.Property(login => login.Id).HasColumnName("id");
            entity.Property(login => login.UserId).HasColumnName("user_id");
            entity.Property(login => login.Provider).HasColumnName("provider").HasMaxLength(80);
            entity.Property(login => login.ProviderUserId).HasColumnName("provider_user_id").HasMaxLength(200);
        });

        modelBuilder.Entity<PersistedIdentityToken>(entity =>
        {
            entity.ToTable("identity_tokens");
            entity.HasKey(token => token.Id);
            entity.HasIndex(token => new { token.Purpose, token.TokenHash }).IsUnique();
            entity.HasIndex(token => new { token.UserId, token.Purpose, token.ConsumedAt });
            entity.Property(token => token.Id).HasColumnName("id");
            entity.Property(token => token.UserId).HasColumnName("user_id");
            entity.Property(token => token.Purpose).HasColumnName("purpose").HasMaxLength(80);
            entity.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(128);
            entity.Property(token => token.ExpiresAt).HasColumnName("expires_at");
            entity.Property(token => token.CreatedAt).HasColumnName("created_at");
            entity.Property(token => token.ConsumedAt).HasColumnName("consumed_at");
        });

        modelBuilder.Entity<PersistedEmailSettings>(entity =>
        {
            entity.ToTable("email_settings");
            entity.HasKey(settings => settings.Id);
            entity.Property(settings => settings.Id).HasColumnName("id");
            entity.Property(settings => settings.Provider).HasColumnName("provider").HasMaxLength(80);
            entity.Property(settings => settings.Host).HasColumnName("host").HasMaxLength(200);
            entity.Property(settings => settings.Port).HasColumnName("port");
            entity.Property(settings => settings.Username).HasColumnName("username").HasMaxLength(200);
            entity.Property(settings => settings.Password).HasColumnName("password");
            entity.Property(settings => settings.FromEmail).HasColumnName("from_email").HasMaxLength(320);
            entity.Property(settings => settings.FromName).HasColumnName("from_name").HasMaxLength(200);
            entity.Property(settings => settings.UseStartTls).HasColumnName("use_start_tls");
            entity.Property(settings => settings.IsEnabled).HasColumnName("is_enabled");
            entity.Property(settings => settings.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PersistedPool>(entity =>
        {
            entity.ToTable("pools");
            entity.HasKey(pool => pool.Id);
            entity.HasIndex(pool => pool.OwnerUserId);
            entity.Property(pool => pool.Id).HasColumnName("id");
            entity.Property(pool => pool.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(pool => pool.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(pool => pool.TournamentId).HasColumnName("tournament_id");
            entity.Property(pool => pool.Profile).HasColumnName("profile").HasConversion<string>().HasMaxLength(40);
            entity.Property(pool => pool.StartingBalance).HasColumnName("starting_balance");
        });

        modelBuilder.Entity<PersistedPoolMember>(entity =>
        {
            entity.ToTable("pool_members");
            entity.HasKey(member => member.Id);
            entity.HasIndex(member => new { member.PoolId, member.UserId }).IsUnique();
            entity.Property(member => member.Id).HasColumnName("id");
            entity.Property(member => member.PoolId).HasColumnName("pool_id");
            entity.Property(member => member.UserId).HasColumnName("user_id");
            entity.Property(member => member.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(40);
            entity.Property(member => member.JoinedAt).HasColumnName("joined_at");
        });

        modelBuilder.Entity<PersistedPoolInvite>(entity =>
        {
            entity.ToTable("pool_invites");
            entity.HasKey(invite => invite.Id);
            entity.HasIndex(invite => invite.Code).IsUnique();
            entity.Property(invite => invite.Id).HasColumnName("id");
            entity.Property(invite => invite.PoolId).HasColumnName("pool_id");
            entity.Property(invite => invite.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(invite => invite.Code).HasColumnName("code").HasMaxLength(64);
            entity.Property(invite => invite.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<PersistedPoolJoinRequest>(entity =>
        {
            entity.ToTable("pool_join_requests");
            entity.HasKey(request => request.Id);
            entity.HasIndex(request => new { request.PoolId, request.UserId }).IsUnique();
            entity.Property(request => request.Id).HasColumnName("id");
            entity.Property(request => request.PoolId).HasColumnName("pool_id");
            entity.Property(request => request.UserId).HasColumnName("user_id");
            entity.Property(request => request.RequestedAt).HasColumnName("requested_at");
            entity.Property(request => request.Status).HasColumnName("status").HasMaxLength(40);
        });

        modelBuilder.Entity<PersistedMarket>(entity =>
        {
            entity.ToTable("markets");
            entity.HasKey(market => market.Id);
            entity.HasIndex(market => market.PoolId);
            entity.HasIndex(market => market.EventId);
            entity.Property(market => market.Id).HasColumnName("id");
            entity.Property(market => market.PoolId).HasColumnName("pool_id");
            entity.Property(market => market.EventId).HasColumnName("event_id");
            entity.Property(market => market.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(40);
            entity.Property(market => market.Period).HasColumnName("period").HasConversion<string>().HasMaxLength(40);
            entity.Property(market => market.LineValue).HasColumnName("line_value").HasPrecision(10, 2);
            entity.Property(market => market.PayoutMultiplier).HasColumnName("payout_multiplier").HasPrecision(10, 2);
            entity.Property(market => market.PayoutConfigurationVersion).HasColumnName("payout_configuration_version");
            entity.Property(market => market.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        });

        modelBuilder.Entity<PersistedPrediction>(entity =>
        {
            entity.ToTable("predictions");
            entity.HasKey(prediction => prediction.Id);
            entity.HasIndex(prediction => prediction.PoolId);
            entity.HasIndex(prediction => prediction.MemberId);
            entity.Property(prediction => prediction.Id).HasColumnName("id");
            entity.Property(prediction => prediction.PoolId).HasColumnName("pool_id");
            entity.Property(prediction => prediction.MemberId).HasColumnName("member_id");
            entity.Property(prediction => prediction.MarketId).HasColumnName("market_id");
            entity.Property(prediction => prediction.SelectedOption).HasColumnName("selected_option").HasMaxLength(200);
            entity.Property(prediction => prediction.Stake).HasColumnName("stake");
            entity.Property(prediction => prediction.MarketType).HasColumnName("market_type").HasConversion<string>().HasMaxLength(40);
            entity.Property(prediction => prediction.MarketPeriod).HasColumnName("market_period").HasConversion<string>().HasMaxLength(40);
            entity.Property(prediction => prediction.LineValueSnapshot).HasColumnName("line_value_snapshot").HasPrecision(10, 2);
            entity.Property(prediction => prediction.PayoutMultiplierSnapshot).HasColumnName("payout_multiplier_snapshot").HasPrecision(10, 2);
            entity.Property(prediction => prediction.PayoutConfigurationVersionSnapshot).HasColumnName("payout_configuration_version_snapshot");
            entity.Property(prediction => prediction.SubmittedAt).HasColumnName("submitted_at");
        });

        modelBuilder.Entity<PersistedPointLedgerEntry>(entity =>
        {
            entity.ToTable("point_ledger");
            entity.HasKey(entry => entry.Id);
            entity.HasIndex(entry => new { entry.MemberId, entry.CreatedAt });
            entity.HasIndex(entry => entry.PoolId);
            entity.Property(entry => entry.Id).HasColumnName("id");
            entity.Property(entry => entry.PoolId).HasColumnName("pool_id");
            entity.Property(entry => entry.MemberId).HasColumnName("member_id");
            entity.Property(entry => entry.Points).HasColumnName("points");
            entity.Property(entry => entry.Reason).HasColumnName("reason").HasConversion<string>().HasMaxLength(60);
            entity.Property(entry => entry.PredictionId).HasColumnName("prediction_id");
            entity.Property(entry => entry.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<PersistedPayoutConfiguration>(entity =>
        {
            entity.ToTable("payout_configurations");
            entity.HasKey(configuration => configuration.Id);
            entity.HasIndex(configuration => configuration.Version).IsUnique();
            entity.Property(configuration => configuration.Id).HasColumnName("id");
            entity.Property(configuration => configuration.Version).HasColumnName("version");
            entity.Property(configuration => configuration.Name).HasColumnName("name").HasMaxLength(160);
            entity.Property(configuration => configuration.IsActive).HasColumnName("is_active");
            entity.Property(configuration => configuration.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<PersistedPayoutMarketRule>(entity =>
        {
            entity.ToTable("payout_configuration_market_rules");
            entity.HasKey(rule => rule.Id);
            entity.HasIndex(rule => new { rule.PayoutConfigurationId, rule.Profile, rule.MarketType, rule.Period }).IsUnique();
            entity.HasOne(rule => rule.PayoutConfiguration)
                .WithMany(configuration => configuration.Rules)
                .HasForeignKey(rule => rule.PayoutConfigurationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(rule => rule.Id).HasColumnName("id");
            entity.Property(rule => rule.PayoutConfigurationId).HasColumnName("payout_configuration_id");
            entity.Property(rule => rule.Profile).HasColumnName("profile").HasConversion<string>().HasMaxLength(40);
            entity.Property(rule => rule.MarketType).HasColumnName("market_type").HasConversion<string>().HasMaxLength(40);
            entity.Property(rule => rule.Period).HasColumnName("period").HasConversion<string>().HasMaxLength(40);
            entity.Property(rule => rule.LineValue).HasColumnName("line_value").HasPrecision(10, 2);
            entity.Property(rule => rule.PayoutMultiplier).HasColumnName("payout_multiplier").HasPrecision(10, 2);
            entity.Property(rule => rule.IsEnabled).HasColumnName("is_enabled");
        });

        modelBuilder.Entity<PersistedSettlementRun>(entity =>
        {
            entity.ToTable("settlement_runs");
            entity.HasKey(run => run.Id);
            entity.HasIndex(run => run.EventId);
            entity.Property(run => run.Id).HasColumnName("id");
            entity.Property(run => run.EventId).HasColumnName("event_id");
            entity.Property(run => run.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            entity.Property(run => run.StartedAt).HasColumnName("started_at");
            entity.Property(run => run.CompletedAt).HasColumnName("completed_at");
        });

        modelBuilder.Entity<PersistedSettlementLog>(entity =>
        {
            entity.ToTable("settlement_logs");
            entity.HasKey(log => log.Id);
            entity.HasIndex(log => log.SettlementRunId);
            entity.HasOne(log => log.SettlementRun)
                .WithMany(run => run.Logs)
                .HasForeignKey(log => log.SettlementRunId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(log => log.Id).HasColumnName("id");
            entity.Property(log => log.SettlementRunId).HasColumnName("settlement_run_id");
            entity.Property(log => log.PredictionId).HasColumnName("prediction_id");
            entity.Property(log => log.Level).HasColumnName("level").HasConversion<string>().HasMaxLength(40);
            entity.Property(log => log.Message).HasColumnName("message").HasMaxLength(500);
            entity.Property(log => log.CreatedAt).HasColumnName("created_at");
        });
    }
}

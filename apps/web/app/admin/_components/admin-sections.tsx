"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { Activity, CalendarClock, CheckCircle2, KeyRound, ListChecks, RefreshCw, Save, Search, Send, SlidersHorizontal, Trophy, Users } from "lucide-react";
import { IconLabel, Panel, StatGrid } from "../../components/ui";
import { apiUrl, readApiError } from "../../lib/api";
import { getStoredToken } from "../../lib/auth";
import { formatDisplayDateTime } from "../../lib/datetime";
import { appName } from "../../lib/config";

type ProviderSyncStatus = {
  provider: string;
  lastSyncedAt: string | null;
  lastResult: string;
  tournamentCount: number;
  participantCount: number;
  eventCount: number;
};

type ProviderList = {
  defaultProvider: string;
  providers: string[];
};

type AdminEvent = {
  id: string;
  tournamentName: string;
  homeParticipant: string;
  awayParticipant: string;
  startsAt: string;
  status: string;
  provider: string;
  isTestData: boolean;
  managementMode: string;
  firstHalfHomeScore: number | null;
  firstHalfAwayScore: number | null;
  fullTimeHomeScore: number | null;
  fullTimeAwayScore: number | null;
  resultRecordedAt: string | null;
};

type PayoutConfiguration = {
  id: string;
  version: number;
  name: string;
  isActive: boolean;
  rules: Array<{
    id: string;
    profile: string;
    marketType: string;
    period: string;
    lineValue: number | null;
    payoutMultiplier: number;
    isEnabled: boolean;
  }>;
};

type HandicapLineMarket = {
  id: string;
  poolId: string;
  poolName: string;
  eventId: string;
  marketPeriod: string;
  lineValue: number | null;
  payoutMultiplier: number;
  status: string;
};

type AdminUser = {
  id: string;
  email: string;
  displayName: string;
  role: string;
  isEmailVerified: boolean;
  mustChangePassword: boolean;
  lastLoginAt: string | null;
};

type EmailSettings = {
  provider: string;
  host: string;
  port: number;
  username: string;
  hasPassword: boolean;
  fromEmail: string;
  fromName: string;
  useStartTls: boolean;
  isEnabled: boolean;
};

export function ProviderSection() {
  const [status, setStatus] = useState<ProviderSyncStatus | null>(null);
  const [providerList, setProviderList] = useState<ProviderList | null>(null);
  const [selectedProvider, setSelectedProvider] = useState("");
  const [message, setMessage] = useState("Loading provider status...");
  const [isSyncing, setIsSyncing] = useState(false);

  useEffect(() => {
    loadProviders();
    loadStatus();
  }, []);

  async function loadProviders() {
    try {
      const response = await fetch(apiUrl("/api/tournaments/providers"));
      if (!response.ok) {
        setMessage(await readApiError(response, "Could not load providers."));
        return;
      }

      const result = (await response.json()) as ProviderList;
      setProviderList(result);
      setSelectedProvider(result.defaultProvider || result.providers[0] || "");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Could not load providers.");
    }
  }

  async function loadStatus() {
    try {
      const response = await fetch(apiUrl("/api/tournaments/provider/status"));
      if (!response.ok) {
        setMessage(await readApiError(response, "Could not load provider status."));
        return;
      }

      setStatus(await response.json());
      setMessage("Provider status loaded.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Could not load provider status.");
    }
  }

  async function syncProvider() {
    const token = getStoredToken();
    if (!token) {
      setMessage("Session is missing.");
      return;
    }
    if (!selectedProvider) {
      setMessage("Select a provider first.");
      return;
    }

    setIsSyncing(true);
    setMessage(`Syncing ${selectedProvider}...`);
    try {
      const params = new URLSearchParams({ provider: selectedProvider });
      const response = await fetch(apiUrl(`/api/tournaments/sync?${params.toString()}`), {
        method: "POST",
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        setMessage(await readApiError(response, "Provider sync failed."));
        return;
      }

      setStatus(await response.json());
      setMessage(`${selectedProvider} sync completed.`);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Provider sync failed.");
    } finally {
      setIsSyncing(false);
    }
  }

  return (
    <Panel title="Tournament provider">
      <div className="buttonRow">
        <label>Provider<select value={selectedProvider} onChange={(event) => setSelectedProvider(event.target.value)}>{providerList?.providers.map((provider) => <option key={provider} value={provider}>{provider}</option>)}</select></label>
        <button className="button" disabled={isSyncing || !selectedProvider} type="button" onClick={syncProvider}><IconLabel icon={RefreshCw}>Sync selected provider</IconLabel></button>
      </div>
      <p className="statusText">{message}</p>
      {status ? (
        <>
          <StatGrid
            items={[
              { label: "Tournaments", value: status.tournamentCount, icon: Trophy },
              { label: "Participants", value: status.participantCount, icon: Users },
              { label: "Events", value: status.eventCount, icon: Activity }
            ]}
          />
          <dl className="detailList">
            <div><dt>Last synced provider</dt><dd>{status.provider}</dd></div>
            <div><dt>Default provider</dt><dd>{providerList?.defaultProvider ?? "Unknown"}</dd></div>
            <div><dt>Last result</dt><dd>{status.lastResult}</dd></div>
            <div><dt>Last synced</dt><dd>{status.lastSyncedAt ? formatDisplayDateTime(status.lastSyncedAt) : "Not synced"}</dd></div>
          </dl>
        </>
      ) : null}
    </Panel>
  );
}

export function EventManagementSection() {
  const eventState = useAdminEvents("Loading events...");
  const selectedEvent = eventState.selectedEvent;
  const [managementMode, setManagementMode] = useState("Manual");
  const [eventStatus, setEventStatus] = useState("Finished");
  const [startsAt, setStartsAt] = useState("");
  const [fullTimeHomeScore, setFullTimeHomeScore] = useState("0");
  const [fullTimeAwayScore, setFullTimeAwayScore] = useState("0");
  const [firstHalfHomeScore, setFirstHalfHomeScore] = useState("0");
  const [firstHalfAwayScore, setFirstHalfAwayScore] = useState("0");
  const [isSavingEvent, setIsSavingEvent] = useState(false);
  const [fullTimeHandicapLine, setFullTimeHandicapLine] = useState("0.5");
  const [firstHalfHandicapLine, setFirstHalfHandicapLine] = useState("0.5");
  const [handicapLineMessage, setHandicapLineMessage] = useState("");
  const [isSavingHandicapLine, setIsSavingHandicapLine] = useState(false);

  useEffect(() => {
    if (!selectedEvent) {
      return;
    }

    setManagementMode(selectedEvent.managementMode);
    setEventStatus(selectedEvent.status);
    setStartsAt(toDateTimeLocal(selectedEvent.startsAt));
    setFullTimeHomeScore(scoreToText(selectedEvent.fullTimeHomeScore));
    setFullTimeAwayScore(scoreToText(selectedEvent.fullTimeAwayScore));
    setFirstHalfHomeScore(scoreToText(selectedEvent.firstHalfHomeScore));
    setFirstHalfAwayScore(scoreToText(selectedEvent.firstHalfAwayScore));
    setFullTimeHandicapLine("0.5");
    setFirstHalfHandicapLine("0.5");
    void loadHandicapLines(selectedEvent.id);
  }, [selectedEvent]);

  async function loadHandicapLines(eventId: string) {
    const token = getStoredToken();
    if (!token) {
      setHandicapLineMessage("Session is missing.");
      return;
    }

    setHandicapLineMessage("Loading handicap lines...");
    try {
      const response = await fetch(apiUrl(`/api/markets/events/${eventId}/handicap-lines`), {
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        setHandicapLineMessage(await readApiError(response, "Could not load handicap lines."));
        return;
      }

      const result = (await response.json()) as HandicapLineMarket[];
      const fullTime = result.find((market) => market.marketPeriod === "FullTime" && market.lineValue !== null);
      const firstHalf = result.find((market) => market.marketPeriod === "FirstHalf" && market.lineValue !== null);
      if (fullTime?.lineValue !== undefined && fullTime.lineValue !== null) {
        setFullTimeHandicapLine(String(fullTime.lineValue));
      }
      if (firstHalf?.lineValue !== undefined && firstHalf.lineValue !== null) {
        setFirstHalfHandicapLine(String(firstHalf.lineValue));
      }
      setHandicapLineMessage(result.length === 0 ? "No handicap markets generated for this event." : `${result.length} handicap markets loaded.`);
    } catch (error) {
      setHandicapLineMessage(error instanceof Error ? error.message : "Could not load handicap lines.");
    }
  }

  async function saveHandicapLines() {
    const token = getStoredToken();
    if (!token || !selectedEvent) {
      setHandicapLineMessage("Select an event first.");
      return;
    }

    const fullTimeLineValue = Number(fullTimeHandicapLine);
    const firstHalfLineValue = Number(firstHalfHandicapLine);
    if (!Number.isFinite(fullTimeLineValue) || !Number.isFinite(firstHalfLineValue)) {
      setHandicapLineMessage("Enter valid full-time and first-half handicap lines.");
      return;
    }

    setIsSavingHandicapLine(true);
    setHandicapLineMessage("Saving handicap lines...");
    try {
      for (const line of [
        { marketPeriod: "FullTime", lineValue: fullTimeLineValue },
        { marketPeriod: "FirstHalf", lineValue: firstHalfLineValue },
      ]) {
        const response = await fetch(apiUrl(`/api/markets/events/${selectedEvent.id}/handicap-lines`), {
          method: "PUT",
          headers: {
            Authorization: `Bearer ${token}`,
            "Content-Type": "application/json",
          },
          body: JSON.stringify(line),
        });

        if (!response.ok) {
          setHandicapLineMessage(await readApiError(response, "Could not save handicap lines."));
          return;
        }
      }

      setHandicapLineMessage("Handicap lines saved.");
    } catch (error) {
      setHandicapLineMessage(error instanceof Error ? error.message : "Could not save handicap lines.");
    } finally {
      setIsSavingHandicapLine(false);
    }
  }

  async function saveEvent(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const token = getStoredToken();
    if (!token || !selectedEvent) {
      eventState.setMessage("Select an event first.");
      return;
    }

    setIsSavingEvent(true);
    eventState.setMessage("Saving event...");
    try {
      const response = await fetch(apiUrl(`/api/tournaments/events/${selectedEvent.id}/manual`), {
        method: "PUT",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          startsAt: new Date(startsAt).toISOString(),
          status: eventStatus,
          fullTimeHomeScore: textToNullableNumber(fullTimeHomeScore),
          fullTimeAwayScore: textToNullableNumber(fullTimeAwayScore),
          firstHalfHomeScore: textToNullableNumber(firstHalfHomeScore),
          firstHalfAwayScore: textToNullableNumber(firstHalfAwayScore),
        }),
      });

      if (!response.ok) {
        eventState.setMessage(await readApiError(response, "Could not save event."));
        return;
      }

      if (managementMode !== "Manual") {
        await updateSelectedEventMode(selectedEvent.id, token, managementMode);
      }

      eventState.setMessage("Event saved.");
      await eventState.loadEvents();
    } catch (error) {
      eventState.setMessage(error instanceof Error ? error.message : "Could not save event.");
    } finally {
      setIsSavingEvent(false);
    }
  }

  async function updateModeOnly() {
    const token = getStoredToken();
    if (!token || !selectedEvent) {
      eventState.setMessage("Select an event first.");
      return;
    }

    setIsSavingEvent(true);
    eventState.setMessage("Updating event management mode...");
    try {
      await updateSelectedEventMode(selectedEvent.id, token, managementMode);
      eventState.setMessage(`Event is now ${managementMode.toLowerCase()} managed.`);
      await eventState.loadEvents();
    } catch (error) {
      eventState.setMessage(error instanceof Error ? error.message : "Could not update event management mode.");
    } finally {
      setIsSavingEvent(false);
    }
  }

  return (
    <Panel title="Event management">
      <EventFilters state={eventState} />
      <div className="adminEventGrid">
        <EventList state={eventState} />
        <form className="form adminEventEditor" onSubmit={saveEvent}>
          <SelectedEventSummary selectedEvent={selectedEvent} />
          <div className="adminEventPrimaryGrid">
            <label>Kickoff<input required type="datetime-local" value={startsAt} onChange={(event) => setStartsAt(event.target.value)} /></label>
            <label>Status<EventStatusSelect value={eventStatus} onChange={setEventStatus} /></label>
          </div>
          <ScoreInputs
            firstHalfAwayScore={firstHalfAwayScore}
            firstHalfHomeScore={firstHalfHomeScore}
            fullTimeAwayScore={fullTimeAwayScore}
            fullTimeHomeScore={fullTimeHomeScore}
            setFirstHalfAwayScore={setFirstHalfAwayScore}
            setFirstHalfHomeScore={setFirstHalfHomeScore}
            setFullTimeAwayScore={setFullTimeAwayScore}
            setFullTimeHomeScore={setFullTimeHomeScore}
          />
          <div className="adminEventModeRow">
            <label>Mode<ManagementModeSelect value={managementMode} onChange={setManagementMode} /></label>
          </div>
          <div className="buttonRow">
            <button className="button" disabled={!selectedEvent || isSavingEvent} type="submit"><IconLabel icon={Save}>Save event</IconLabel></button>
            <button className="button buttonSecondary" disabled={!selectedEvent || isSavingEvent} type="button" onClick={updateModeOnly}><IconLabel icon={SlidersHorizontal}>Mode only</IconLabel></button>
          </div>
          <section className="handicapLineEditor">
            <h3><IconLabel icon={SlidersHorizontal}>Handicap lines</IconLabel></h3>
            <p className="statusText">{handicapLineMessage || "Select an event to manage handicap lines."}</p>
            <div className="scoreGrid scoreGridCompact">
              <label>
                FT
                <input step="0.25" type="number" value={fullTimeHandicapLine} onChange={(event) => setFullTimeHandicapLine(event.target.value)} />
              </label>
              <label>
                HT
                <input step="0.25" type="number" value={firstHalfHandicapLine} onChange={(event) => setFirstHalfHandicapLine(event.target.value)} />
              </label>
            </div>
            <button className="button" disabled={!selectedEvent || isSavingHandicapLine} type="button" onClick={saveHandicapLines}><IconLabel icon={Save}>Confirm handicap lines</IconLabel></button>
          </section>
        </form>
      </div>
    </Panel>
  );
}

export function SettlementSection() {
  const eventState = useAdminEvents("Select an event to settle predictions.");
  const selectedEvent = eventState.selectedEvent;
  const [settlementMessage, setSettlementMessage] = useState("Select an event to settle predictions.");
  const [fullTimeHomeScore, setFullTimeHomeScore] = useState("0");
  const [fullTimeAwayScore, setFullTimeAwayScore] = useState("0");
  const [firstHalfHomeScore, setFirstHalfHomeScore] = useState("0");
  const [firstHalfAwayScore, setFirstHalfAwayScore] = useState("0");
  const [isCancelled, setIsCancelled] = useState(false);
  const [isSettling, setIsSettling] = useState(false);

  useEffect(() => {
    if (!selectedEvent) {
      return;
    }

    setFullTimeHomeScore(scoreToText(selectedEvent.fullTimeHomeScore));
    setFullTimeAwayScore(scoreToText(selectedEvent.fullTimeAwayScore));
    setFirstHalfHomeScore(scoreToText(selectedEvent.firstHalfHomeScore));
    setFirstHalfAwayScore(scoreToText(selectedEvent.firstHalfAwayScore));
    setIsCancelled(selectedEvent.status === "Cancelled");
    setSettlementMessage("Enter or confirm the result, then settle the selected event.");
  }, [selectedEvent]);

  async function settleEvent(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const token = getStoredToken();
    if (!token || !selectedEvent) {
      setSettlementMessage("Select an event first.");
      return;
    }

    setIsSettling(true);
    setSettlementMessage("Settling event...");
    try {
      const response = await fetch(apiUrl(`/api/settlement/events/${selectedEvent.id}/result`), {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          fullTimeHomeScore: isCancelled ? 0 : Number(fullTimeHomeScore || 0),
          fullTimeAwayScore: isCancelled ? 0 : Number(fullTimeAwayScore || 0),
          firstHalfHomeScore: isCancelled ? null : textToNullableNumber(firstHalfHomeScore),
          firstHalfAwayScore: isCancelled ? null : textToNullableNumber(firstHalfAwayScore),
          isCancelled,
        }),
      });

      if (!response.ok) {
        setSettlementMessage(await readApiError(response, "Settlement failed."));
        return;
      }

      const result = await response.json() as { settledPredictions: number; unchangedPredictions: number; ledgerEntriesCreated: number };
      setSettlementMessage(`Settled ${result.settledPredictions} predictions, unchanged ${result.unchangedPredictions}, created ${result.ledgerEntriesCreated} ledger entries.`);
      await eventState.loadEvents();
    } catch (error) {
      setSettlementMessage(error instanceof Error ? error.message : "Settlement failed.");
    } finally {
      setIsSettling(false);
    }
  }

  return (
    <Panel title="Manual settlement">
      <EventFilters state={eventState} />
      <div className="adminEventGrid">
        <EventList state={eventState} />
        <form className="form settlementForm" onSubmit={settleEvent}>
          <p className="statusText">{settlementMessage}</p>
          <SelectedEventSummary selectedEvent={selectedEvent} />
          <label className="checkboxRow">
            <input checked={isCancelled} type="checkbox" onChange={(event) => setIsCancelled(event.target.checked)} />
            Cancelled event
          </label>
          <ScoreInputs
            disabled={isCancelled}
            firstHalfAwayScore={firstHalfAwayScore}
            firstHalfHomeScore={firstHalfHomeScore}
            fullTimeAwayScore={fullTimeAwayScore}
            fullTimeHomeScore={fullTimeHomeScore}
            setFirstHalfAwayScore={setFirstHalfAwayScore}
            setFirstHalfHomeScore={setFirstHalfHomeScore}
            setFullTimeAwayScore={setFullTimeAwayScore}
            setFullTimeHomeScore={setFullTimeHomeScore}
          />
          <button className="button" disabled={!selectedEvent || isSettling} type="submit"><IconLabel icon={CheckCircle2}>Settle event</IconLabel></button>
        </form>
      </div>
    </Panel>
  );
}

export function PayoutSection() {
  const [payoutConfiguration, setPayoutConfiguration] = useState<PayoutConfiguration | null>(null);
  const [payoutMessage, setPayoutMessage] = useState("Loading payout defaults...");

  useEffect(() => {
    loadPayoutConfiguration();
  }, []);

  async function loadPayoutConfiguration() {
    const token = getStoredToken();
    if (!token) {
      setPayoutMessage("Session is missing.");
      return;
    }

    try {
      const response = await fetch(apiUrl("/api/markets/payout-configurations"), {
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        setPayoutMessage(await readApiError(response, "Could not load payout defaults."));
        return;
      }

      const configurations = (await response.json()) as PayoutConfiguration[];
      setPayoutConfiguration(configurations.find((configuration) => configuration.isActive) ?? configurations[0] ?? null);
      setPayoutMessage(configurations.length === 0 ? "No payout defaults configured." : "Payout defaults loaded.");
    } catch (error) {
      setPayoutMessage(error instanceof Error ? error.message : "Could not load payout defaults.");
    }
  }

  return (
    <Panel title="Payout defaults">
      <p className="statusText">{payoutMessage}</p>
      {payoutConfiguration ? (
        <>
          <StatGrid
            items={[
              { label: "Version", value: payoutConfiguration.version, icon: SlidersHorizontal },
              { label: "Rules", value: payoutConfiguration.rules.length, icon: ListChecks },
              { label: "Active", value: payoutConfiguration.isActive ? "Yes" : "No", icon: Activity }
            ]}
          />
          <dl className="detailList">
            <div><dt>Name</dt><dd>{payoutConfiguration.name}</dd></div>
          </dl>
          <div className="ruleList">
            {payoutConfiguration.rules.map((rule) => (
              <article className="ruleRow" key={rule.id}>
                <span><strong>{rule.profile}</strong><small>{rule.period}</small></span>
                <span><strong>{rule.marketType}</strong><small>{rule.lineValue ?? "No line"}</small></span>
                <span><strong>{rule.payoutMultiplier}x</strong><small>{rule.isEnabled ? "Enabled" : "Disabled"}</small></span>
              </article>
            ))}
          </div>
        </>
      ) : null}
    </Panel>
  );
}

export function UserManagementSection() {
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [userMessage, setUserMessage] = useState("Loading users...");
  const [userSearch, setUserSearch] = useState("");
  const [temporaryPassword, setTemporaryPassword] = useState("");
  const [isLoadingUsers, setIsLoadingUsers] = useState(false);

  useEffect(() => {
    loadUsers();
  }, []);

  async function loadUsers(event?: FormEvent<HTMLFormElement>) {
    event?.preventDefault();
    const token = getStoredToken();
    if (!token) {
      setUserMessage("Session is missing.");
      return;
    }

    setIsLoadingUsers(true);
    setUserMessage("Loading users...");
    try {
      const params = new URLSearchParams();
      if (userSearch.trim()) {
        params.set("search", userSearch.trim());
      }

      const response = await fetch(apiUrl(`/api/admin/users?${params.toString()}`), {
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        setUserMessage(await readApiError(response, "Could not load users."));
        return;
      }

      const result = (await response.json()) as AdminUser[];
      setUsers(result);
      setUserMessage(result.length === 0 ? "No users match the search." : `${result.length} users loaded.`);
    } catch (error) {
      setUserMessage(error instanceof Error ? error.message : "Could not load users.");
    } finally {
      setIsLoadingUsers(false);
    }
  }

  async function resetUserPassword(userId: string) {
    const token = getStoredToken();
    if (!token) {
      setUserMessage("Session is missing.");
      return;
    }

    setTemporaryPassword("");
    setUserMessage("Resetting password...");
    try {
      const response = await fetch(apiUrl(`/api/admin/users/${userId}/password-reset`), {
        method: "POST",
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        setUserMessage(await readApiError(response, "Could not reset password."));
        return;
      }

      const result = (await response.json()) as { message: string; temporaryPassword: string };
      setTemporaryPassword(result.temporaryPassword);
      setUserMessage(result.message);
      await loadUsers();
    } catch (error) {
      setUserMessage(error instanceof Error ? error.message : "Could not reset password.");
    }
  }

  async function verifyUserEmail(userId: string) {
    const token = getStoredToken();
    if (!token) {
      setUserMessage("Session is missing.");
      return;
    }

    setUserMessage("Marking user email as verified...");
    try {
      const response = await fetch(apiUrl(`/api/admin/users/${userId}/verify-email`), {
        method: "POST",
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        setUserMessage(await readApiError(response, "Could not verify user email."));
        return;
      }

      setUserMessage("User email marked as verified.");
      await loadUsers();
    } catch (error) {
      setUserMessage(error instanceof Error ? error.message : "Could not verify user email.");
    }
  }

  return (
    <Panel title="User management">
      <p className="statusText">{userMessage}</p>
      <form className="adminFilterBar" onSubmit={loadUsers}>
        <label>Search users<input placeholder="Email or display name" type="search" value={userSearch} onChange={(event) => setUserSearch(event.target.value)} /></label>
        <button className="button" disabled={isLoadingUsers} type="submit"><IconLabel icon={Search}>Search</IconLabel></button>
      </form>
      {temporaryPassword ? (
        <dl className="detailList">
          <div><dt>Temporary password</dt><dd>{temporaryPassword}</dd></div>
        </dl>
      ) : null}
      <div className="adminUserList">
        {users.map((user) => (
          <article className="adminUserRow" key={user.id}>
            <span><strong>{user.displayName}</strong><small>{user.email}</small></span>
            <span><strong>{user.role}</strong><small>{user.isEmailVerified ? "Verified" : "Unverified"}</small></span>
            <span><strong>{user.mustChangePassword ? "Must change" : "Current"}</strong><small>{user.lastLoginAt ? `Last login ${formatDisplayDateTime(user.lastLoginAt)}` : "No login recorded"}</small></span>
            <div className="adminUserActions">
              {!user.isEmailVerified ? (
                <button className="button compactButton" type="button" onClick={() => verifyUserEmail(user.id)}><IconLabel icon={CheckCircle2}>Verify</IconLabel></button>
              ) : null}
              <button className="button buttonSecondary compactButton" type="button" onClick={() => resetUserPassword(user.id)}><IconLabel icon={KeyRound}>Reset</IconLabel></button>
            </div>
          </article>
        ))}
      </div>
    </Panel>
  );
}

export function SystemSettingsSection() {
  const [emailSettings, setEmailSettings] = useState<EmailSettings | null>(null);
  const [emailMessage, setEmailMessage] = useState("Loading email settings...");
  const [emailProvider, setEmailProvider] = useState("AwsSesSmtp");
  const [emailHost, setEmailHost] = useState("");
  const [emailPort, setEmailPort] = useState(587);
  const [emailUsername, setEmailUsername] = useState("");
  const [emailPassword, setEmailPassword] = useState("");
  const [emailFromEmail, setEmailFromEmail] = useState("");
  const [emailFromName, setEmailFromName] = useState(appName);
  const [emailUseStartTls, setEmailUseStartTls] = useState(true);
  const [emailIsEnabled, setEmailIsEnabled] = useState(false);
  const [testEmail, setTestEmail] = useState("");
  const [isSavingEmail, setIsSavingEmail] = useState(false);

  useEffect(() => {
    loadEmailSettings();
  }, []);

  async function loadEmailSettings() {
    const token = getStoredToken();
    if (!token) {
      setEmailMessage("Session is missing.");
      return;
    }

    try {
      const response = await fetch(apiUrl("/api/admin/email-settings"), {
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        setEmailMessage(await readApiError(response, "Could not load email settings."));
        return;
      }

      const result = (await response.json()) as EmailSettings;
      setEmailSettings(result);
      setEmailProvider(result.provider);
      setEmailHost(result.host);
      setEmailPort(result.port);
      setEmailUsername(result.username);
      setEmailFromEmail(result.fromEmail);
      setEmailFromName(result.fromName);
      setEmailUseStartTls(result.useStartTls);
      setEmailIsEnabled(result.isEnabled);
      setEmailMessage(result.hasPassword ? "Email settings loaded. Existing password is saved." : "Email settings loaded.");
    } catch (error) {
      setEmailMessage(error instanceof Error ? error.message : "Could not load email settings.");
    }
  }

  async function saveEmailSettings(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const token = getStoredToken();
    if (!token) {
      setEmailMessage("Session is missing.");
      return;
    }

    setIsSavingEmail(true);
    setEmailMessage("Saving email settings...");
    try {
      const response = await fetch(apiUrl("/api/admin/email-settings"), {
        method: "PUT",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          provider: emailProvider,
          host: emailHost,
          port: emailPort,
          username: emailUsername,
          password: emailPassword || null,
          fromEmail: emailFromEmail,
          fromName: emailFromName,
          useStartTls: emailUseStartTls,
          isEnabled: emailIsEnabled,
        }),
      });

      if (!response.ok) {
        setEmailMessage(await readApiError(response, "Could not save email settings."));
        return;
      }

      const result = (await response.json()) as EmailSettings;
      setEmailSettings(result);
      setEmailPassword("");
      setEmailMessage("Email settings saved.");
    } catch (error) {
      setEmailMessage(error instanceof Error ? error.message : "Could not save email settings.");
    } finally {
      setIsSavingEmail(false);
    }
  }

  async function sendTestEmail() {
    const token = getStoredToken();
    if (!token) {
      setEmailMessage("Session is missing.");
      return;
    }

    setEmailMessage("Sending test email...");
    try {
      const response = await fetch(apiUrl("/api/admin/email-settings/test"), {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ toEmail: testEmail }),
      });

      if (!response.ok) {
        setEmailMessage(await readApiError(response, "Could not send test email."));
        return;
      }

      const result = (await response.json()) as { message: string };
      setEmailMessage(result.message);
    } catch (error) {
      setEmailMessage(error instanceof Error ? error.message : "Could not send test email.");
    }
  }

  return (
    <Panel title="SMTP settings">
      <p className="statusText">{emailMessage}</p>
      <form className="form" onSubmit={saveEmailSettings}>
        <div className="scoreGrid">
          <label>Provider<select value={emailProvider} onChange={(event) => setEmailProvider(event.target.value)}><option value="AwsSesSmtp">AWS SES SMTP</option><option value="Smtp">SMTP</option></select></label>
          <label>Host<input placeholder="email-smtp.us-east-1.amazonaws.com" required={emailIsEnabled} value={emailHost} onChange={(event) => setEmailHost(event.target.value)} /></label>
          <label>Port<input min={1} required type="number" value={emailPort} onChange={(event) => setEmailPort(Number(event.target.value))} /></label>
          <label>Username<input autoComplete="username" value={emailUsername} onChange={(event) => setEmailUsername(event.target.value)} /></label>
        </div>
        <div className="scoreGrid">
          <label>Password<input autoComplete="new-password" placeholder={emailSettings?.hasPassword ? "Leave blank to keep saved password" : ""} type="password" value={emailPassword} onChange={(event) => setEmailPassword(event.target.value)} /></label>
          <label>From email<input required={emailIsEnabled} type="email" value={emailFromEmail} onChange={(event) => setEmailFromEmail(event.target.value)} /></label>
          <label>From name<input value={emailFromName} onChange={(event) => setEmailFromName(event.target.value)} /></label>
          <label>Test email<input type="email" value={testEmail} onChange={(event) => setTestEmail(event.target.value)} /></label>
        </div>
        <div className="buttonRow">
          <label className="checkboxRow"><input checked={emailUseStartTls} type="checkbox" onChange={(event) => setEmailUseStartTls(event.target.checked)} />STARTTLS</label>
          <label className="checkboxRow"><input checked={emailIsEnabled} type="checkbox" onChange={(event) => setEmailIsEnabled(event.target.checked)} />Enabled</label>
        </div>
        <div className="buttonRow">
          <button className="button" disabled={isSavingEmail} type="submit"><IconLabel icon={Save}>Save SMTP</IconLabel></button>
          <button className="button buttonSecondary" disabled={!testEmail} type="button" onClick={sendTestEmail}><IconLabel icon={Send}>Test email</IconLabel></button>
        </div>
      </form>
    </Panel>
  );
}

type AdminEventState = ReturnType<typeof useAdminEvents>;

function useAdminEvents(initialMessage: string) {
  const [events, setEvents] = useState<AdminEvent[]>([]);
  const [selectedEventId, setSelectedEventId] = useState("");
  const [message, setMessage] = useState(initialMessage);
  const [providerFilter, setProviderFilter] = useState("");
  const [sourceFilter, setSourceFilter] = useState("All");
  const [modeFilter, setModeFilter] = useState("All");
  const [statusFilter, setStatusFilter] = useState("All");
  const [isLoadingEvents, setIsLoadingEvents] = useState(false);

  const selectedEvent = useMemo(
    () => events.find((event) => event.id === selectedEventId) ?? null,
    [events, selectedEventId]
  );
  const providerOptions = useMemo(
    () => Array.from(new Set(events.map((event) => event.provider))).sort(),
    [events]
  );

  useEffect(() => {
    loadEvents();
  }, []);

  async function loadEvents(event?: FormEvent<HTMLFormElement>) {
    event?.preventDefault();
    const token = getStoredToken();
    if (!token) {
      setMessage("Session is missing.");
      return;
    }

    setIsLoadingEvents(true);
    setMessage("Loading events...");
    try {
      const params = new URLSearchParams();
      if (providerFilter) {
        params.set("provider", providerFilter);
      }
      if (sourceFilter !== "All") {
        params.set("isTestData", sourceFilter === "Test" ? "true" : "false");
      }
      if (modeFilter !== "All") {
        params.set("managementMode", modeFilter);
      }
      if (statusFilter !== "All") {
        params.set("status", statusFilter);
      }

      const response = await fetch(apiUrl(`/api/tournaments/events/admin?${params.toString()}`), {
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        setMessage(await readApiError(response, "Could not load events."));
        return;
      }

      const result = ((await response.json()) as AdminEvent[])
        .filter(shouldShowAdminEvent)
        .sort(compareAdminEventsForDisplay);
      setEvents(result);
      setSelectedEventId((current) => result.some((item) => item.id === current) ? current : result[0]?.id ?? "");
      setMessage(result.length === 0 ? "No events match the filters." : `${result.length} events loaded.`);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Could not load events.");
    } finally {
      setIsLoadingEvents(false);
    }
  }

  return {
    events,
    isLoadingEvents,
    loadEvents,
    message,
    modeFilter,
    providerFilter,
    providerOptions,
    selectedEvent,
    selectedEventId,
    setMessage,
    setModeFilter,
    setProviderFilter,
    setSelectedEventId,
    setSourceFilter,
    setStatusFilter,
    sourceFilter,
    statusFilter,
  };
}

function EventFilters({ state }: { state: AdminEventState }) {
  return (
    <>
      <p className="statusText">{state.message}</p>
      <form className="adminFilterBar" onSubmit={state.loadEvents}>
        <label>Provider<select value={state.providerFilter} onChange={(event) => state.setProviderFilter(event.target.value)}><option value="">All providers</option>{state.providerOptions.map((provider) => <option key={provider} value={provider}>{provider}</option>)}</select></label>
        <label>Source<select value={state.sourceFilter} onChange={(event) => state.setSourceFilter(event.target.value)}><option value="All">All data</option><option value="Real">Real</option><option value="Test">Mock/test</option></select></label>
        <label>Mode<ManagementModeFilterSelect value={state.modeFilter} onChange={state.setModeFilter} /></label>
        <label>Status<EventStatusFilterSelect value={state.statusFilter} onChange={state.setStatusFilter} /></label>
        <button className="button" disabled={state.isLoadingEvents} type="submit"><IconLabel icon={Search}>Filter</IconLabel></button>
      </form>
    </>
  );
}

function EventList({ state }: { state: AdminEventState }) {
  return (
    <div className="adminEventList">
      {state.events.map((matchEvent) => (
        <button className={matchEvent.id === state.selectedEventId ? "adminEventRow active" : "adminEventRow"} key={matchEvent.id} type="button" onClick={() => state.setSelectedEventId(matchEvent.id)}>
          <span><strong>{matchEvent.homeParticipant} vs {matchEvent.awayParticipant}</strong><small>{matchEvent.tournamentName}</small></span>
          <span><strong>{formatDisplayDateTime(matchEvent.startsAt)}</strong><small><IconLabel icon={CalendarClock}>{matchEvent.status}</IconLabel></small></span>
          <span><strong>{matchEvent.provider}{matchEvent.isTestData ? " test" : ""}</strong><small>{matchEvent.managementMode}</small></span>
        </button>
      ))}
    </div>
  );
}

function SelectedEventSummary({ selectedEvent }: { selectedEvent: AdminEvent | null }) {
  return (
    <div className="selectedMarket">
      <Activity aria-hidden="true" size={18} />
      <span>
        <strong>{selectedEvent ? `${selectedEvent.homeParticipant} vs ${selectedEvent.awayParticipant}` : "Select an event"}</strong>
        <small>
          {selectedEvent ? (
            <>
              {selectedEvent.provider} {selectedEvent.isTestData ? "test" : "real"} | {selectedEvent.managementMode}
              <span className="eventIdText">{selectedEvent.id}</span>
            </>
          ) : "No event selected"}
        </small>
      </span>
    </div>
  );
}

function ScoreInputs({
  disabled = false,
  firstHalfAwayScore,
  firstHalfHomeScore,
  fullTimeAwayScore,
  fullTimeHomeScore,
  setFirstHalfAwayScore,
  setFirstHalfHomeScore,
  setFullTimeAwayScore,
  setFullTimeHomeScore
}: {
  disabled?: boolean;
  firstHalfAwayScore: string;
  firstHalfHomeScore: string;
  fullTimeAwayScore: string;
  fullTimeHomeScore: string;
  setFirstHalfAwayScore: (value: string) => void;
  setFirstHalfHomeScore: (value: string) => void;
  setFullTimeAwayScore: (value: string) => void;
  setFullTimeHomeScore: (value: string) => void;
}) {
  return (
    <div className="scoreGrid scoreGridCompact">
      <label>FT home<input disabled={disabled} min={0} type="number" value={fullTimeHomeScore} onChange={(event) => setFullTimeHomeScore(event.target.value)} /></label>
      <label>FT away<input disabled={disabled} min={0} type="number" value={fullTimeAwayScore} onChange={(event) => setFullTimeAwayScore(event.target.value)} /></label>
      <label>HT home<input disabled={disabled} min={0} type="number" value={firstHalfHomeScore} onChange={(event) => setFirstHalfHomeScore(event.target.value)} /></label>
      <label>HT away<input disabled={disabled} min={0} type="number" value={firstHalfAwayScore} onChange={(event) => setFirstHalfAwayScore(event.target.value)} /></label>
    </div>
  );
}

function EventStatusSelect({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return <select value={value} onChange={(event) => onChange(event.target.value)}>{eventStatuses.map((item) => <option key={item} value={item}>{item}</option>)}</select>;
}

function EventStatusFilterSelect({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return <select value={value} onChange={(event) => onChange(event.target.value)}><option value="All">All statuses</option>{eventStatuses.map((item) => <option key={item} value={item}>{item}</option>)}</select>;
}

function ManagementModeSelect({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return <select value={value} onChange={(event) => onChange(event.target.value)}><option value="Manual">Manual</option><option value="Provider">Provider</option></select>;
}

function ManagementModeFilterSelect({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return <select value={value} onChange={(event) => onChange(event.target.value)}><option value="All">All modes</option><option value="Provider">Provider</option><option value="Manual">Manual</option></select>;
}

async function updateSelectedEventMode(eventId: string, token: string, targetMode: string) {
  const response = await fetch(apiUrl(`/api/tournaments/events/${eventId}/management-mode`), {
    method: "PUT",
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ managementMode: targetMode }),
  });

  if (!response.ok) {
    throw new Error(await readApiError(response, "Could not update event management mode."));
  }
}

const eventStatuses = ["Scheduled", "Live", "Finished", "Postponed", "Cancelled", "Settled"];
const adminClosedEventStatuses = new Set(["Cancelled", "Canceled", "Settled"]);
const adminClosedEventDisplayWindowMs = 72 * 60 * 60 * 1000;

function shouldShowAdminEvent(matchEvent: AdminEvent) {
  if (!adminClosedEventStatuses.has(matchEvent.status)) {
    return true;
  }

  const startsAt = new Date(matchEvent.startsAt).getTime();
  return Date.now() - startsAt <= adminClosedEventDisplayWindowMs;
}

function compareAdminEventsForDisplay(first: AdminEvent, second: AdminEvent) {
  const firstClosedRank = adminClosedEventStatuses.has(first.status) ? 1 : 0;
  const secondClosedRank = adminClosedEventStatuses.has(second.status) ? 1 : 0;
  if (firstClosedRank !== secondClosedRank) {
    return firstClosedRank - secondClosedRank;
  }

  return new Date(first.startsAt).getTime() - new Date(second.startsAt).getTime();
}

function scoreToText(score: number | null) {
  return score === null ? "" : String(score);
}

function textToNullableNumber(value: string) {
  return value.trim() === "" ? null : Number(value);
}

function toDateTimeLocal(value: string) {
  const date = new Date(value);
  const offset = date.getTimezoneOffset() * 60000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}

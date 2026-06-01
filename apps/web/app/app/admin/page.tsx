"use client";

import { useEffect, useState } from "react";
import { Activity, ListChecks, RefreshCw, Shield, SlidersHorizontal, Trophy, Users } from "lucide-react";
import { IconLabel, PageHeader, Panel, StatGrid } from "../../components/ui";
import { apiUrl, readApiError } from "../../lib/api";
import { getStoredToken } from "../../lib/auth";

type ProviderSyncStatus = {
  provider: string;
  lastSyncedAt: string | null;
  lastResult: string;
  tournamentCount: number;
  participantCount: number;
  eventCount: number;
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

export default function AdminPage() {
  const [status, setStatus] = useState<ProviderSyncStatus | null>(null);
  const [payoutConfiguration, setPayoutConfiguration] = useState<PayoutConfiguration | null>(null);
  const [message, setMessage] = useState("Loading provider status...");
  const [payoutMessage, setPayoutMessage] = useState("Loading payout defaults...");
  const [isSyncing, setIsSyncing] = useState(false);

  useEffect(() => {
    loadStatus();
    loadPayoutConfiguration();
  }, []);

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

  async function syncProvider() {
    const token = getStoredToken();
    if (!token) {
      setMessage("Session is missing.");
      return;
    }

    setIsSyncing(true);
    setMessage("Syncing provider...");
    try {
      const response = await fetch(apiUrl("/api/tournaments/sync"), {
        method: "POST",
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        setMessage(await readApiError(response, "Provider sync failed."));
        return;
      }

      setStatus(await response.json());
      setMessage("Provider sync completed.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Provider sync failed.");
    } finally {
      setIsSyncing(false);
    }
  }

  return (
    <section className="pageStack">
      <PageHeader
        eyebrow="Admin"
        title="Platform admin"
        icon={Shield}
        actions={<button className="button" disabled={isSyncing} type="button" onClick={syncProvider}><IconLabel icon={RefreshCw}>Sync provider</IconLabel></button>}
      />
      <Panel title="Tournament provider">
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
              <div><dt>Provider</dt><dd>{status.provider}</dd></div>
              <div><dt>Last result</dt><dd>{status.lastResult}</dd></div>
              <div><dt>Last synced</dt><dd>{status.lastSyncedAt ? new Date(status.lastSyncedAt).toLocaleString() : "Not synced"}</dd></div>
            </dl>
          </>
        ) : null}
      </Panel>
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
                  <span>
                    <strong>{rule.profile}</strong>
                    <small>{rule.period}</small>
                  </span>
                  <span>
                    <strong>{rule.marketType}</strong>
                    <small>{rule.lineValue ?? "No line"}</small>
                  </span>
                  <span>
                    <strong>{rule.payoutMultiplier}x</strong>
                    <small>{rule.isEnabled ? "Enabled" : "Disabled"}</small>
                  </span>
                </article>
              ))}
            </div>
          </>
        ) : null}
      </Panel>
    </section>
  );
}

"use client";

import { useEffect, useState } from "react";
import { BadgeDollarSign, Eye, EyeOff, ShieldCheck, Trophy, Users, Waves } from "lucide-react";
import { IconLabel, PageHeader, Panel, StatGrid, StatusPill } from "../../components/ui";
import { apiUrl, readApiError } from "../../lib/api";
import { getStoredToken } from "../../lib/auth";

type AdminPool = {
  id: string;
  name: string;
  ownerUserId: string;
  ownerDisplayName: string;
  ownerEmail: string;
  tournamentId: string;
  tournamentName: string;
  provider: string;
  isTestData: boolean;
  profile: string;
  startingBalance: number;
  isHidden: boolean;
  memberCount: number;
  inviteCount: number;
};

export default function AdminPoolsPage() {
  const [pools, setPools] = useState<AdminPool[]>([]);
  const [status, setStatus] = useState("Loading pools...");

  useEffect(() => {
    loadPools();
  }, []);

  async function loadPools() {
    const token = getStoredToken();
    if (!token) {
      setStatus("Session is missing.");
      return;
    }

    try {
      const response = await fetch(apiUrl("/api/admin/pools"), {
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        setStatus(await readApiError(response, "Could not load pools."));
        return;
      }

      const result = (await response.json()) as AdminPool[];
      setPools(result);
      setStatus(result.length === 0 ? "No pools yet." : `${result.length} pool loaded.`);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Could not load pools.");
    }
  }

  async function setPoolVisibility(pool: AdminPool, isHidden: boolean) {
    const token = getStoredToken();
    if (!token) {
      setStatus("Session is missing.");
      return;
    }

    setStatus(`${isHidden ? "Hiding" : "Showing"} ${pool.name}...`);

    try {
      const response = await fetch(apiUrl(`/api/admin/pools/${pool.id}/visibility`), {
        method: "PUT",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ isHidden }),
      });

      if (!response.ok) {
        setStatus(await readApiError(response, "Could not update pool visibility."));
        return;
      }

      setPools((current) => current.map((item) => (item.id === pool.id ? { ...item, isHidden } : item)));
      setStatus(`${pool.name} is now ${isHidden ? "hidden" : "visible"}.`);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Could not update pool visibility.");
    }
  }

  return (
    <section className="pageStack">
      <PageHeader eyebrow="Admin" title="Pools" icon={Waves} />
      <StatusPill icon={ShieldCheck}>{status}</StatusPill>
      <StatGrid
        items={[
          { label: "Pools", value: pools.length, icon: Waves },
          { label: "Members", value: pools.reduce((sum, pool) => sum + pool.memberCount, 0), icon: Users },
          { label: "Invites", value: pools.reduce((sum, pool) => sum + pool.inviteCount, 0), icon: ShieldCheck },
          { label: "Test pools", value: pools.filter((pool) => pool.isTestData).length, icon: Trophy },
          { label: "Hidden", value: pools.filter((pool) => pool.isHidden).length, icon: EyeOff },
        ]}
      />
      <Panel title="All pools">
        <div className="adminPoolList">
          {pools.map((pool) => (
            <article className="adminPoolRow" key={pool.id}>
              <span>
                <strong>{pool.name}</strong>
                <small>{pool.profile} profile{pool.isHidden ? " hidden" : ""}</small>
              </span>
              <span>
                <strong>{pool.ownerDisplayName}</strong>
                <small>{pool.ownerEmail}</small>
              </span>
              <span>
                <strong>{pool.tournamentName}</strong>
                <small>{pool.provider}{pool.isTestData ? " test data" : ""}</small>
              </span>
              <span>
                <strong><IconLabel icon={Users}>{pool.memberCount} members</IconLabel></strong>
                <small>{pool.inviteCount} invites</small>
              </span>
              <span>
                <strong><IconLabel icon={BadgeDollarSign}>{pool.startingBalance}</IconLabel></strong>
                <small>Starting balance</small>
              </span>
              <button
                className="adminPoolVisibilityButton"
                type="button"
                onClick={() => setPoolVisibility(pool, !pool.isHidden)}
                title={pool.isHidden ? "Show pool" : "Hide pool"}
              >
                {pool.isHidden ? <Eye size={16} /> : <EyeOff size={16} />}
                {pool.isHidden ? "Show" : "Hide"}
              </button>
            </article>
          ))}
        </div>
      </Panel>
    </section>
  );
}

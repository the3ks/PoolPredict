"use client";

import { useEffect, useState } from "react";
import { BadgeDollarSign, ShieldCheck, Trophy, Users, Waves } from "lucide-react";
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
        ]}
      />
      <Panel title="All pools">
        <div className="adminPoolList">
          {pools.map((pool) => (
            <article className="adminPoolRow" key={pool.id}>
              <span>
                <strong>{pool.name}</strong>
                <small>{pool.profile} profile</small>
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
            </article>
          ))}
        </div>
      </Panel>
    </section>
  );
}

"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Hash, Plus, ShieldCheck, UserPlus, Users, Waves } from "lucide-react";
import { UserShell } from "../components/user-shell";
import { IconLabel, PageHeader, StatusPill } from "../components/ui";
import { apiUrl, readApiError } from "../lib/api";
import { getStoredToken } from "../lib/auth";
import { PoolSummary } from "../lib/types";

export default function PoolsPage() {
  const [pools, setPools] = useState<PoolSummary[]>([]);
  const [status, setStatus] = useState("Loading pools...");

  useEffect(() => {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    fetch(apiUrl("/api/pools"), {
      headers: { Authorization: `Bearer ${token}` },
    }).then(async (response) => {
      if (!response.ok) {
        setStatus(await readApiError(response, "Could not load pools."));
        return;
      }

      const result = (await response.json()) as PoolSummary[];
      setPools(result);
      setStatus(result.length === 0 ? "No pools yet." : `${result.length} pool loaded.`);
    }).catch((error) => setStatus(error instanceof Error ? error.message : "Could not load pools."));
  }, []);

  return (
    <UserShell>
    <section className="pageStack">
      <PageHeader
        eyebrow="Pools"
        title="Your pools"
        icon={Waves}
        actions={
          <>
            <Link className="button" href="/pools/new"><IconLabel icon={Plus}>Create pool</IconLabel></Link>
            <Link className="button buttonSecondary" href="/pools/join"><IconLabel icon={UserPlus}>Join pool</IconLabel></Link>
          </>
        }
      />
      <StatusPill icon={Users}>{status}</StatusPill>
      <div className="poolList">
        {pools.map((pool) => (
          <article className="poolCard" key={pool.id}>
            <Link className="poolCardLink" href={`/pools/${pool.id}`}>
              <span>
                <strong>{pool.name}</strong>
                <small>{pool.profile} profile</small>
              </span>
              <span>
                <strong>{pool.memberCount}</strong>
                <small><IconLabel icon={Users}>members</IconLabel></small>
              </span>
              <span>
                <strong>{pool.role}</strong>
                <small><IconLabel icon={pool.role === "Member" ? Hash : ShieldCheck}>{pool.inviteCount} invites</IconLabel></small>
              </span>
            </Link>
          </article>
        ))}
      </div>
    </section>
    </UserShell>
  );
}

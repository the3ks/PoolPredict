"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { apiUrl, readApiError } from "../../lib/api";
import { getStoredToken } from "../../lib/auth";
import { PoolSummary } from "../../lib/types";

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
    <section className="pageStack">
      <div className="pageHeader">
        <div>
          <p className="eyebrow">Pools</p>
          <h1>Your pools</h1>
        </div>
        <div className="buttonRow">
          <Link className="button" href="/app/pools/new">Create pool</Link>
          <Link className="button buttonSecondary" href="/app/pools/join">Join pool</Link>
        </div>
      </div>
      <span className="statusPill">{status}</span>
      <div className="poolList">
        {pools.map((pool) => (
          <article className="poolCard" key={pool.id}>
            <Link className="poolCardLink" href={`/app/pools/${pool.id}`}>
              <span>
                <strong>{pool.name}</strong>
                <small>{pool.profile} profile</small>
              </span>
              <span>
                <strong>{pool.memberCount}</strong>
                <small>members</small>
              </span>
              <span>
                <strong>{pool.role}</strong>
                <small>{pool.inviteCount} invites</small>
              </span>
            </Link>
          </article>
        ))}
      </div>
    </section>
  );
}

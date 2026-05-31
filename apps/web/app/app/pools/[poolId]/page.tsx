"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { apiUrl, readApiError } from "../../../lib/api";
import { getStoredToken } from "../../../lib/auth";
import { PoolSummary } from "../../../lib/types";

export default function PoolOverviewPage() {
  const params = useParams<{ poolId: string }>();
  const poolId = params.poolId;
  const [pool, setPool] = useState<PoolSummary | null>(null);
  const [name, setName] = useState("");
  const [startingBalance, setStartingBalance] = useState(1000);
  const [status, setStatus] = useState("Loading pool...");

  useEffect(() => {
    loadPool();
  }, [poolId]);

  async function loadPool() {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    const response = await fetch(apiUrl(`/api/pools/${poolId}`), {
      headers: { Authorization: `Bearer ${token}` },
    });

    if (!response.ok) {
      setStatus(await readApiError(response, "Could not load pool."));
      return;
    }

    const result = (await response.json()) as PoolSummary;
    setPool(result);
    setName(result.name);
    setStartingBalance(result.startingBalance);
    setStatus("Pool loaded.");
  }

  async function updatePool(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const token = getStoredToken();
    if (!token || !pool) {
      return;
    }

    const response = await fetch(apiUrl(`/api/pools/${pool.id}`), {
      method: "PUT",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ name, startingBalance }),
    });

    if (!response.ok) {
      setStatus(await readApiError(response, "Pool update failed."));
      return;
    }

    setStatus("Pool updated.");
    await loadPool();
  }

  return (
    <section className="pageStack">
      <div className="pageHeader">
        <div>
          <p className="eyebrow">Pool overview</p>
          <h1>{pool?.name ?? "Pool"}</h1>
        </div>
        {pool ? <Link className="button buttonSecondary" href={`/app/pools/${pool.id}/invites`}>Invites</Link> : null}
      </div>
      <span className="statusPill">{status}</span>
      {pool ? (
        <div className="poolGrid">
          <section className="panel">
            <h2>Summary</h2>
            <dl className="detailList">
              <div><dt>Role</dt><dd>{pool.role}</dd></div>
              <div><dt>Profile</dt><dd>{pool.profile}</dd></div>
              <div><dt>Members</dt><dd>{pool.memberCount}</dd></div>
              <div><dt>Invites</dt><dd>{pool.inviteCount}</dd></div>
              <div><dt>Starting balance</dt><dd>{pool.startingBalance}</dd></div>
            </dl>
          </section>
          <form className="form panel" onSubmit={updatePool}>
            <h2>Settings</h2>
            <label>
              Pool name
              <input required type="text" value={name} onChange={(event) => setName(event.target.value)} />
            </label>
            <label>
              Starting balance
              <input min={1} required type="number" value={startingBalance} onChange={(event) => setStartingBalance(Number(event.target.value))} />
            </label>
            <button className="button" disabled={pool.role === "Member"} type="submit">Save pool</button>
          </form>
        </div>
      ) : null}
    </section>
  );
}

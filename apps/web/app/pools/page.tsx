"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import {
  BadgeDollarSign,
  CircleDotDashed,
  Hash,
  Plus,
  Send,
  ShieldCheck,
  Trophy,
  UserPlus,
  Users,
} from "lucide-react";
import { UserShell } from "../components/user-shell";
import { IconLabel, PageHeader, Panel, StatusPill } from "../components/ui";
import { apiUrl, readApiError } from "../lib/api";
import { getStoredToken } from "../lib/auth";
import { DiscoverPool, PoolSummary } from "../lib/types";

export default function PoolsPage() {
  const [pools, setPools] = useState<PoolSummary[]>([]);
  const [otherPools, setOtherPools] = useState<DiscoverPool[]>([]);
  const [status, setStatus] = useState("Loading pools...");
  const [requestStatus, setRequestStatus] = useState("");

  useEffect(() => {
    loadPools();
  }, []);

  async function loadPools() {
    const token = getStoredToken();
    if (!token) {
      setStatus("Login is required to view pools.");
      return;
    }

    try {
      const [myPoolsResponse, otherPoolsResponse] = await Promise.all([
        fetch(apiUrl("/api/pools"), {
          headers: { Authorization: `Bearer ${token}` },
        }),
        fetch(apiUrl("/api/pools/discover"), {
          headers: { Authorization: `Bearer ${token}` },
        }),
      ]);

      if (!myPoolsResponse.ok) {
        setStatus(
          await readApiError(myPoolsResponse, "Could not load your pools."),
        );
        return;
      }

      if (!otherPoolsResponse.ok) {
        setStatus(
          await readApiError(otherPoolsResponse, "Could not load other pools."),
        );
        return;
      }

      const myPools = (await myPoolsResponse.json()) as PoolSummary[];
      const discoverPools = (await otherPoolsResponse.json()) as DiscoverPool[];
      setPools(myPools);
      setOtherPools(discoverPools);
      setStatus(
        `${myPools.length} joined or owned. ${discoverPools.length} available to request.`,
      );
    } catch (error) {
      setStatus(
        error instanceof Error ? error.message : "Could not load pools.",
      );
    }
  }

  async function requestJoin(pool: DiscoverPool) {
    const token = getStoredToken();
    if (!token) {
      setRequestStatus("Login is required to request joining.");
      return;
    }

    setRequestStatus(`Requesting access to ${pool.name}...`);
    try {
      const response = await fetch(
        apiUrl(`/api/pools/${pool.id}/join-requests`),
        {
          method: "POST",
          headers: { Authorization: `Bearer ${token}` },
        },
      );

      if (!response.ok) {
        setRequestStatus(
          await readApiError(response, "Could not request to join."),
        );
        return;
      }

      setRequestStatus(`Join request sent for ${pool.name}.`);
      await loadPools();
    } catch (error) {
      setRequestStatus(
        error instanceof Error ? error.message : "Could not request to join.",
      );
    }
  }

  return (
    <UserShell>
      <section className="pageStack">
        <PageHeader
          eyebrow="Joined and available"
          title="Lobby"
          icon={CircleDotDashed}
          actions={
            <>
              <Link className="button" href="/pools/new">
                <IconLabel icon={Plus}>Create pool</IconLabel>
              </Link>
              <Link className="button buttonSecondary" href="/pools/join">
                <IconLabel icon={UserPlus}>Join pool</IconLabel>
              </Link>
            </>
          }
        />
        <StatusPill icon={Users}>{status}</StatusPill>
        {requestStatus ? (
          <StatusPill icon={Send}>{requestStatus}</StatusPill>
        ) : null}
        <Panel title="My Pools">
          <div className="poolList">
            {pools.length === 0 ? (
              <p className="mutedText">
                You have not joined or created any pools yet.
              </p>
            ) : null}
            {pools.map((pool) => (
              <article className="poolCard" key={pool.id}>
                <Link className="poolCardLink" href={`/pools/${pool.id}`}>
                  <span>
                    <strong>{pool.name}</strong>
                    <small>{pool.profile} profile</small>
                  </span>
                  <span>
                    <strong>{pool.memberCount}</strong>
                    <small>
                      <IconLabel icon={Users}>members</IconLabel>
                    </small>
                  </span>
                  <span>
                    <strong>{pool.role}</strong>
                    <small>
                      <IconLabel
                        icon={pool.role === "Member" ? Hash : ShieldCheck}
                      >
                        {pool.inviteCount} invites
                      </IconLabel>
                    </small>
                  </span>
                </Link>
              </article>
            ))}
          </div>
        </Panel>
        <Panel title="Community Pools">
          <div className="poolList">
            {otherPools.length === 0 ? (
              <p className="mutedText">
                No other pools are available right now.
              </p>
            ) : null}
            {otherPools.map((pool) => (
              <article className="poolCard" key={pool.id}>
                <div className="poolCardLink discoverPoolCardLink">
                  <span>
                    <strong>{pool.name}</strong>
                    <small>{pool.profile} profile</small>
                  </span>
                  <span>
                    <strong>{pool.tournamentName}</strong>
                    <small>
                      <IconLabel icon={Trophy}>
                        {pool.provider}
                        {pool.isTestData ? " test data" : ""}
                      </IconLabel>
                    </small>
                  </span>
                  <span>
                    <strong>{pool.memberCount}</strong>
                    <small>
                      <IconLabel icon={Users}>members</IconLabel>
                    </small>
                  </span>
                  <span>
                    <strong>{pool.startingBalance}</strong>
                    <small>
                      <IconLabel icon={BadgeDollarSign}>
                        starting balance
                      </IconLabel>
                    </small>
                  </span>
                  <button
                    className="button buttonSecondary"
                    disabled={pool.hasPendingJoinRequest}
                    type="button"
                    onClick={() => requestJoin(pool)}
                  >
                    <IconLabel icon={Send}>
                      {pool.hasPendingJoinRequest
                        ? "Requested"
                        : "Request to join"}
                    </IconLabel>
                  </button>
                </div>
              </article>
            ))}
          </div>
        </Panel>
      </section>
    </UserShell>
  );
}

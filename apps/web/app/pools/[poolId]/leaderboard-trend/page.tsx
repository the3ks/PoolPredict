"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { ArrowLeft, LineChart, Trophy } from "lucide-react";
import { LeaderboardTimelineChart } from "../../../components/leaderboard-timeline-chart";
import { UserShell } from "../../../components/user-shell";
import { IconLabel, PageHeader, Panel, StatusPill } from "../../../components/ui";
import { apiUrl, readApiError } from "../../../lib/api";
import { getStoredToken } from "../../../lib/auth";
import type { LeaderboardTimeline, PoolSummary } from "../../../lib/types";

export default function LeaderboardTrendPage() {
  const params = useParams<{ poolId: string }>();
  const poolId = params.poolId;
  const [pool, setPool] = useState<PoolSummary | null>(null);
  const [timeline, setTimeline] = useState<LeaderboardTimeline | null>(null);
  const [status, setStatus] = useState("Loading leaderboard trend...");

  useEffect(() => {
    void loadPage();
  }, [poolId]);

  async function loadPage() {
    const token = getStoredToken();
    if (!token) {
      setStatus("Session is missing.");
      return;
    }

    const poolResponse = await fetch(apiUrl(`/api/pools/${poolId}`), {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!poolResponse.ok) {
      setStatus(await readApiError(poolResponse, "Could not load pool."));
      return;
    }

    const poolResult = (await poolResponse.json()) as PoolSummary;
    setPool(poolResult);

    const timelineResponse = await fetch(
      apiUrl(`/api/predictions/pool/${poolResult.id}/leaderboard/timeline`),
      { headers: { Authorization: `Bearer ${token}` } },
    );
    if (!timelineResponse.ok) {
      setStatus(
        await readApiError(timelineResponse, "Could not load leaderboard trend."),
      );
      return;
    }

    setTimeline((await timelineResponse.json()) as LeaderboardTimeline);
    setStatus("Leaderboard trend loaded.");
  }

  return (
    <UserShell>
      <main className="pageStack">
        <PageHeader
          actions={
            <Link className="button buttonSecondary" href={`/pools/${poolId}`}>
              <IconLabel icon={ArrowLeft}>Pool</IconLabel>
            </Link>
          }
          eyebrow={pool?.name ?? "Leaderboard"}
          icon={LineChart}
          title="Leaderboard trend"
        />
        <StatusPill icon={Trophy}>{status}</StatusPill>
        <Panel>
          <LeaderboardTimelineChart timeline={timeline} />
        </Panel>
      </main>
    </UserShell>
  );
}

"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { ArrowLeft, History } from "lucide-react";
import { UserShell } from "../../../components/user-shell";
import { IconLabel, PageHeader, Panel, StatusPill } from "../../../components/ui";
import { apiUrl, readApiError } from "../../../lib/api";
import { getStoredToken } from "../../../lib/auth";
import { formatDisplayDateTime } from "../../../lib/datetime";
import { LeaderboardEntry, PoolSummary, Prediction } from "../../../lib/types";

export default function PoolPredictionsPage() {
  const params = useParams<{ poolId: string }>();
  const poolId = params.poolId;
  const [pool, setPool] = useState<PoolSummary | null>(null);
  const [predictions, setPredictions] = useState<Prediction[]>([]);
  const [leaderboard, setLeaderboard] = useState<LeaderboardEntry[]>([]);
  const [status, setStatus] = useState("Loading predictions...");

  useEffect(() => {
    loadPage();
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
    await Promise.all([loadPredictions(poolResult.id), loadLeaderboard(poolResult.id)]);
    setStatus("Prediction history loaded.");
  }

  async function loadPredictions(targetPoolId: string) {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    const response = await fetch(apiUrl(`/api/predictions/pool/${targetPoolId}`), {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (response.ok) {
      const result = (await response.json()) as Prediction[];
      setPredictions(result.sort((left, right) => new Date(right.submittedAt).getTime() - new Date(left.submittedAt).getTime()));
    }
  }

  async function loadLeaderboard(targetPoolId: string) {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    const response = await fetch(apiUrl(`/api/predictions/pool/${targetPoolId}/leaderboard`), {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (response.ok) {
      setLeaderboard((await response.json()) as LeaderboardEntry[]);
    }
  }

  return (
    <UserShell>
      <section className="pageStack">
        <PageHeader
          eyebrow="Pool predictions"
          title={pool?.name ?? "Predictions"}
          icon={History}
          actions={<Link className="button buttonSecondary" href={`/pools/${poolId}`}><IconLabel icon={ArrowLeft}>Back to pool</IconLabel></Link>}
        />
        <StatusPill icon={History}>{status}</StatusPill>

        <Panel title="My predictions">
          {predictions.length === 0 ? (
            <p className="mutedText">No predictions submitted yet.</p>
          ) : (
            <div className="predictionHistory">
              {predictions.map((prediction) => (
                <article className="historyRow" key={prediction.id}>
                  <span>
                    <strong>{formatMarketTypeLabel(prediction.marketType)}</strong>
                    <small>{formatEventName(prediction.eventName) ?? prediction.marketPeriod}</small>
                  </span>
                  <span>
                    <strong>{formatMarketOptionLabel(prediction.selectedOption)}</strong>
                    <small>
                      {prediction.marketPeriod} | {formatNumberDisplay(prediction.stake)} Điểm
                    </small>
                  </span>
                  <span>
                    <strong>{prediction.payoutMultiplierSnapshot}x</strong>
                    <small>{formatDisplayDateTime(prediction.submittedAt)}</small>
                  </span>
                  <span>
                    <strong>{prediction.outcome ?? "Unsettled"}</strong>
                    <small>{formatNetPoints(prediction.netPoints)}</small>
                  </span>
                </article>
              ))}
            </div>
          )}
        </Panel>

        <Panel title="Leaderboard">
          {leaderboard.length > 0 ? (
            <div className="leaderboardList">
              {leaderboard.map((entry, index) => (
                <article className={entry.memberId === pool?.memberId ? "leaderboardRow active" : "leaderboardRow"} key={entry.memberId}>
                  <span>
                    <strong className="leaderboardIdentity">
                      <span className="leaderboardName">
                        <span className="leaderboardRank">#{index + 1}</span>
                        <span className="leaderboardAvatarWrap">
                          {entry.avatarUrl ? (
                            // eslint-disable-next-line @next/next/no-img-element
                            <img alt="" className="leaderboardAvatar" src={entry.avatarUrl} />
                          ) : (
                            <span className="leaderboardAvatarFallback">
                              {entry.displayName.slice(0, 1).toUpperCase()}
                            </span>
                          )}
                        </span>
                        <span className="leaderboardLabel">{entry.displayName}</span>
                      </span>
                    </strong>
                  </span>
                  <span>
                    <strong>{formatNumberDisplay(entry.balance)}</strong>
                    <small>Balance</small>
                  </span>
                  <span>
                    <strong>{entry.winRate}%</strong>
                    <small>{entry.winCount}/{entry.settledPredictionCount} wins</small>
                  </span>
                  <span>
                    <strong>{entry.roi}%</strong>
                    <small>{entry.predictionCount} picks</small>
                  </span>
                </article>
              ))}
            </div>
          ) : (
            <p className="mutedText">No leaderboard rows yet.</p>
          )}
        </Panel>
      </section>
    </UserShell>
  );
}

function formatNetPoints(value: number | undefined) {
  if (value === undefined) {
    return "Not settled";
  }

  return value > 0
    ? `+${formatNumberDisplay(value)} net`
    : `${formatNumberDisplay(value)} net`;
}

function formatNumberDisplay(value: number) {
  if (Number.isInteger(value)) {
    return value.toLocaleString();
  }

  return value.toLocaleString(undefined, {
    maximumFractionDigits: 2,
    minimumFractionDigits: 0,
  });
}

function formatEventName(eventName: string | null | undefined) {
  return eventName?.replace(" vs ", " -vs- ");
}

function formatMarketTypeLabel(type: string) {
  const labels: Record<string, string> = {
    OneXTwo: "1X2",
    OverUnder: "Tài/Xỉu",
    OddEven: "Chẵn/Lẻ",
    CorrectScore: "Tỷ số",
  };

  return labels[type] ?? type;
}

function formatMarketOptionLabel(option: string) {
  if (option.startsWith("Over ")) {
    return option.replace("Over ", "Tài ");
  }

  if (option.startsWith("Under ")) {
    return option.replace("Under ", "Xỉu ");
  }

  const labels: Record<string, string> = {
    Odd: "Lẻ",
    Even: "Chẵn",
    Draw: "Hòa",
  };

  return labels[option] ?? option;
}

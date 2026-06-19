"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { ArrowLeft, ArrowUpDown, ChevronDown, ChevronLeft, ChevronRight, Dices, History, RefreshCw, X } from "lucide-react";
import { UserShell } from "../../../components/user-shell";
import { IconLabel, PageHeader, Panel, StatusPill } from "../../../components/ui";
import { apiUrl, readApiError } from "../../../lib/api";
import { getStoredToken } from "../../../lib/auth";
import { formatDisplayDateTime } from "../../../lib/datetime";
import {
  AutoPickPreview,
  AutoPickSubmission,
  LeaderboardEntry,
  PoolSummary,
  Prediction,
} from "../../../lib/types";

export default function PoolPredictionsPage() {
  const params = useParams<{ poolId: string }>();
  const poolId = params.poolId;
  const [pool, setPool] = useState<PoolSummary | null>(null);
  const [predictions, setPredictions] = useState<Prediction[]>([]);
  const [leaderboard, setLeaderboard] = useState<LeaderboardEntry[]>([]);
  const [status, setStatus] = useState("Loading predictions...");
  const [autoPickStake, setAutoPickStake] = useState(0);
  const [autoPickPreview, setAutoPickPreview] = useState<AutoPickPreview | null>(null);
  const [autoPickStatus, setAutoPickStatus] = useState("");
  const [isLoadingAutoPickPreview, setIsLoadingAutoPickPreview] = useState(false);
  const [isSubmittingAutoPick, setIsSubmittingAutoPick] = useState(false);
  const [isAutoPickExpanded, setIsAutoPickExpanded] = useState(false);
  const [cancellingPredictionId, setCancellingPredictionId] = useState("");
  const [settlementFilter, setSettlementFilter] = useState<
    "All" | "Settled" | "Unsettled"
  >("All");
  const [fromDate, setFromDate] = useState(() => getRelativeDateInputValue(-3));
  const [toDate, setToDate] = useState(() => getRelativeDateInputValue(3));
  const [predictionPageSize, setPredictionPageSize] = useState(20);
  const [predictionPage, setPredictionPage] = useState(1);
  const [eventSortOrder, setEventSortOrder] = useState<"desc" | "asc">("desc");

  useEffect(() => {
    loadPage();
  }, [poolId]);

  useEffect(() => {
    if (!pool || autoPickStake <= 0) {
      setAutoPickPreview(null);
      return;
    }

    void loadAutoPickPreview(pool.id, autoPickStake);
  }, [pool?.id, autoPickStake]);

  useEffect(() => {
    if (!pool) {
      return;
    }

    if (autoPickStake < pool.minStake || autoPickStake > pool.maxStake) {
      setAutoPickStake(pool.minStake);
    }
  }, [pool, autoPickStake]);

  useEffect(() => {
    setPredictionPage(1);
  }, [predictionPageSize, predictions.length, eventSortOrder]);

  useEffect(() => {
    setPredictionPage(1);
    if (!pool) {
      return;
    }

    void loadPredictions(pool.id);
  }, [pool?.id, settlementFilter, fromDate, toDate]);

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
    setAutoPickStake(poolResult.minStake);
    await loadLeaderboard(poolResult.id);
    setStatus("Prediction history loaded.");
  }

  async function loadPredictions(targetPoolId: string) {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    const params = new URLSearchParams();
    if (settlementFilter !== "All") {
      params.set("settlement", settlementFilter);
    }
    if (fromDate) {
      params.set("fromDate", fromDate);
    }
    if (toDate) {
      params.set("toDate", toDate);
    }

    const response = await fetch(apiUrl(`/api/predictions/pool/${targetPoolId}?${params.toString()}`), {
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

  async function loadAutoPickPreview(targetPoolId: string, stake: number) {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    setIsLoadingAutoPickPreview(true);
    const response = await fetch(
      apiUrl(`/api/predictions/pool/${targetPoolId}/auto-pick/preview`),
      {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ stake }),
      },
    );

    if (response.ok) {
      setAutoPickPreview((await response.json()) as AutoPickPreview);
      setAutoPickStatus("");
    } else {
      setAutoPickPreview(null);
      setAutoPickStatus(
        await readApiError(response, "Could not preview auto pick."),
      );
    }

    setIsLoadingAutoPickPreview(false);
  }

  async function submitAutoPick(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!pool || !autoPickPreview || !autoPickPreview.hasEnoughBalance) {
      return;
    }

    const token = getStoredToken();
    if (!token) {
      setAutoPickStatus("Session is missing.");
      return;
    }

    setIsSubmittingAutoPick(true);
    const response = await fetch(apiUrl(`/api/predictions/pool/${pool.id}/auto-pick`), {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ stake: autoPickStake }),
    });

    if (!response.ok) {
      setAutoPickStatus(await readApiError(response, "Auto pick failed."));
      setIsSubmittingAutoPick(false);
      return;
    }

    const result = (await response.json()) as AutoPickSubmission;
    setAutoPickStatus(
      `Auto pick created ${result.createdCount} prediction(s) and skipped ${result.skippedCount}.`,
    );
    await Promise.all([
      loadPredictions(pool.id),
      loadLeaderboard(pool.id),
      loadAutoPickPreview(pool.id, autoPickStake),
    ]);
    setStatus("Prediction history loaded.");
    setIsSubmittingAutoPick(false);
  }

  async function cancelPrediction(predictionId: string) {
    if (!pool) {
      return;
    }

    const token = getStoredToken();
    if (!token) {
      setStatus("Session is missing.");
      return;
    }

    setCancellingPredictionId(predictionId);
    setStatus("Cancelling prediction...");
    try {
      const response = await fetch(apiUrl(`/api/predictions/${predictionId}/cancel`), {
        method: "POST",
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        setStatus(await readApiError(response, "Could not cancel prediction."));
        return;
      }

      await Promise.all([
        loadPredictions(pool.id),
        loadLeaderboard(pool.id),
        loadAutoPickPreview(pool.id, autoPickStake),
      ]);
      setStatus("Prediction cancelled.");
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Could not cancel prediction.");
    } finally {
      setCancellingPredictionId("");
    }
  }

  const sortedPredictions = [...predictions].sort((left, right) => {
    const leftTime = left.eventStartsAt
      ? new Date(left.eventStartsAt).getTime()
      : new Date(left.submittedAt).getTime();
    const rightTime = right.eventStartsAt
      ? new Date(right.eventStartsAt).getTime()
      : new Date(right.submittedAt).getTime();

    return eventSortOrder === "asc" ? leftTime - rightTime : rightTime - leftTime;
  });

  const totalPredictionPages = Math.max(
    1,
    Math.ceil(sortedPredictions.length / predictionPageSize),
  );
  const currentPredictionPage = Math.min(predictionPage, totalPredictionPages);
  const pagedPredictions = sortedPredictions.slice(
    (currentPredictionPage - 1) * predictionPageSize,
    currentPredictionPage * predictionPageSize,
  );

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
          {pool ? (
            <form className="autoPickForm" onSubmit={submitAutoPick}>
              <div className="autoPickHeader">
                <div>
                  <strong>Auto pick all scheduled events - Gieo quẻ cầu duyên</strong>
                  <small>
                    One random market per eligible event. Events with any existing
                    prediction are skipped.
                  </small>
                </div>
                <button
                  className="autoPickToggle"
                  type="button"
                  onClick={() => setIsAutoPickExpanded((current) => !current)}
                >
                  <IconLabel icon={isAutoPickExpanded ? ChevronDown : ChevronRight}>
                    {isAutoPickExpanded ? "Hide auto pick" : "Show auto pick"}
                  </IconLabel>
                </button>
              </div>
              {isAutoPickExpanded ? (
                <>
                  <label>
                    Stake per event
                    <input
                      max={pool.maxStake}
                      min={pool.minStake}
                      required
                      type="number"
                      value={autoPickStake}
                      onChange={(previewEvent) =>
                        setAutoPickStake(Number(previewEvent.target.value))
                      }
                    />
                  </label>
                  {autoPickPreview ? (
                    <div className="autoPickSummary">
                      <span>
                        <strong>{autoPickPreview.eligibleEventCount}</strong>
                        <small>Eligible events</small>
                      </span>
                      <span>
                        <strong>{formatNumberDisplay(autoPickPreview.totalStake)}</strong>
                        <small>Total deduction</small>
                      </span>
                      <span>
                        <strong>{formatNumberDisplay(autoPickPreview.currentBalance)}</strong>
                        <small>Current balance</small>
                      </span>
                      <span>
                        <strong>{formatNumberDisplay(autoPickPreview.balanceAfterAutoPick)}</strong>
                        <small>Balance after</small>
                      </span>
                    </div>
                  ) : null}
                  {autoPickStatus ? <p className="statusText">{autoPickStatus}</p> : null}
                  {autoPickPreview && !autoPickPreview.hasEnoughBalance ? (
                    <p className="statusText">
                      Current balance is not enough for the full batch.
                    </p>
                  ) : null}
                  <div className="buttonRow">
                    <button
                      className="button compactButton"
                      disabled={
                        isSubmittingAutoPick ||
                        isLoadingAutoPickPreview ||
                        !autoPickPreview ||
                        !autoPickPreview.hasEnoughBalance ||
                        autoPickPreview.eligibleEventCount === 0
                      }
                      type="submit"
                    >
                      <IconLabel icon={Dices}>
                        {isSubmittingAutoPick ? "Submitting..." : "Auto pick all"}
                      </IconLabel>
                    </button>
                    <button
                      className="button buttonSecondary compactButton"
                      disabled={isLoadingAutoPickPreview}
                      type="button"
                      onClick={() => void loadAutoPickPreview(pool.id, autoPickStake)}
                    >
                      <IconLabel icon={RefreshCw}>Refresh preview</IconLabel>
                    </button>
                  </div>
                </>
              ) : null}
            </form>
          ) : null}
          <>
            <div className="adminFilterBar">
              <label>
                Settlement
                <select
                  value={settlementFilter}
                  onChange={(event) =>
                    setSettlementFilter(
                      event.target.value as "All" | "Settled" | "Unsettled",
                    )
                  }
                >
                  <option value="All">All</option>
                  <option value="Settled">Settled</option>
                  <option value="Unsettled">Unsettled</option>
                </select>
              </label>
              <label>
                From date
                <input
                  type="date"
                  value={fromDate}
                  onChange={(event) => setFromDate(event.target.value)}
                />
              </label>
              <label>
                To date
                <input
                  type="date"
                  value={toDate}
                  onChange={(event) => setToDate(event.target.value)}
                />
              </label>
            </div>
            {predictions.length === 0 ? (
              <p className="mutedText">No predictions match the current filters.</p>
            ) : null}
            {predictions.length > 0 ? (
              <>
                <div className="listToolbar">
                  <div className="listToolbarControls">
                    <label className="listPageSizeField">
                    Page size
                    <select
                      value={predictionPageSize}
                      onChange={(event) =>
                        setPredictionPageSize(Number(event.target.value))
                      }
                    >
                      <option value={20}>20</option>
                      <option value={50}>50</option>
                      <option value={100}>100</option>
                    </select>
                  </label>
                  <button
                    className="button buttonSecondary compactButton"
                    type="button"
                    onClick={() =>
                      setEventSortOrder((current) =>
                        current === "desc" ? "asc" : "desc",
                      )
                    }
                  >
                    <IconLabel icon={ArrowUpDown}>
                      Kickoff {eventSortOrder === "desc" ? "↓" : "↑"}
                    </IconLabel>
                  </button>
                </div>
                <div className="listPagination">
                  <button
                    className="button buttonSecondary compactButton"
                    disabled={currentPredictionPage <= 1}
                    type="button"
                    onClick={() =>
                      setPredictionPage((current) => Math.max(1, current - 1))
                    }
                  >
                    <IconLabel icon={ChevronLeft}>Prev</IconLabel>
                  </button>
                  <span>
                    Page {currentPredictionPage} / {totalPredictionPages}
                  </span>
                  <button
                    className="button buttonSecondary compactButton"
                    disabled={currentPredictionPage >= totalPredictionPages}
                    type="button"
                    onClick={() =>
                      setPredictionPage((current) =>
                        Math.min(totalPredictionPages, current + 1),
                      )
                    }
                  >
                    <IconLabel icon={ChevronRight}>Next</IconLabel>
                  </button>
                  </div>
                </div>
                <div className="predictionHistory">
                  {pagedPredictions.map((prediction) => (
                    <article className="historyRow" key={prediction.id}>
                      <span>
                        <strong>
                          {formatMarketTypeLabel(prediction.marketType)}
                          {prediction.eventStartsAt ? (
                            <span className="historyKickoffText">
                              {" - "}
                              {formatDisplayDateTime(prediction.eventStartsAt)}
                            </span>
                          ) : null}
                        </strong>
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
                      <div className="historyRowActions">
                        {prediction.canCancel ? (
                          <button
                            className="button buttonSecondary compactButton"
                            disabled={cancellingPredictionId === prediction.id}
                            type="button"
                            onClick={() => void cancelPrediction(prediction.id)}
                          >
                            <IconLabel icon={X}>
                              {cancellingPredictionId === prediction.id ? "Cancelling..." : "Cancel"}
                            </IconLabel>
                          </button>
                        ) : null}
                      </div>
                    </article>
                  ))}
                </div>
              </>
            ) : null}
          </>
        </Panel>

        <Panel title="Leaderboard">
          {leaderboard.length > 0 ? (
            <div className="leaderboardList">
              <div className="leaderboardHeader">
                <span>Player</span>
                <span>WinLoss</span>
                <span>WinRate</span>
                <span>ROI</span>
                <span>Events</span>
                <span>Event Avg Stake</span>
              </div>
              {leaderboard.map((entry, index) => (
                <article
                  className={[
                    "leaderboardRow",
                    entry.memberId === pool?.memberId ? "active" : "",
                    !entry.isStakeQualified && entry.leaderboardStatus !== "Excluded" ? "invalid" : "",
                    entry.leaderboardStatus === "Excluded" ? "excluded" : "",
                  ]
                    .filter(Boolean)
                    .join(" ")}
                  key={entry.memberId}
                >
                  <span>
                    <strong className="leaderboardIdentity">
                      <span className="leaderboardName">
                        <span className="leaderboardTopline">
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
                        </span>
                        <span className="leaderboardLabel">{entry.displayName}</span>
                        {entry.vipAdjustmentAmount > 0 ? (
                          <span className="vipBadge compact">
                            {formatVipLabel(entry.vipLevel)}
                          </span>
                        ) : null}
                        {entry.leaderboardStatus === "Excluded" ? (
                          <span className="leaderboardExcludedBadge">Excluded</span>
                        ) : null}
                      </span>
                    </strong>
                  </span>
                  <span>
                    <strong>{formatNumberDisplay(entry.winLoss)}</strong>
                  </span>
                  <span>
                    <strong>{entry.winRate}%</strong>
                    <small>{entry.winCount}/{entry.settledPredictionCount} wins</small>
                  </span>
                  <span>
                    <strong>{entry.roi}%</strong>
                    <small>{entry.predictionCount} picks</small>
                  </span>
                  <span>
                    <strong>{entry.settledEventRate}%</strong>
                    <small>{entry.settledEventCount}/{entry.totalEventCount} events</small>
                  </span>
                  <span>
                    <strong>{formatNumberDisplay(entry.settledEventAverageStake)}</strong>
                    <small>
                      {entry.isStakeQualified ? ">=" : "<"} {formatNumberDisplay(entry.minimumEventAverageStake)}
                    </small>
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

function formatVipLabel(vipLevel: number) {
  return `VIP${"⭐".repeat(Math.max(0, vipLevel))}`;
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

function getRelativeDateInputValue(daysFromToday: number) {
  const date = new Date();
  date.setHours(0, 0, 0, 0);
  date.setDate(date.getDate() + daysFromToday);
  return toDateInputValue(date);
}

function toDateInputValue(date: Date) {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${year}-${month}-${day}`;
}

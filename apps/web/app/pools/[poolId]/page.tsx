"use client";

import Link from "next/link";
import {
  Dispatch,
  FormEvent,
  SetStateAction,
  useEffect,
  useState,
} from "react";
import { useParams } from "next/navigation";
import {
  CalendarClock,
  ChevronDown,
  ChevronRight,
  Goal,
  History,
  KeyRound,
  Lock,
  RefreshCw,
  Save,
  Send,
  Settings,
  ShieldCheck,
  UserCheck,
  UserX,
  Users,
  Waves,
} from "lucide-react";
import { UserShell } from "../../components/user-shell";
import {
  IconLabel,
  PageHeader,
  Panel,
  StatGrid,
  StatusPill,
} from "../../components/ui";
import { apiUrl, readApiError } from "../../lib/api";
import { getStoredToken } from "../../lib/auth";
import { formatParticipantName } from "../../lib/participant-flags";
import {
  LeaderboardEntry,
  Market,
  PoolJoinRequest,
  PoolSummary,
  TournamentEvent,
} from "../../lib/types";

type RecentPrediction = {
  eventName: string;
  marketType: string;
  marketPeriod: string;
  selectedOption: string;
  stake: number;
  payoutMultiplier: number;
  submittedAt: string;
};

type MarketPredictionSummary = {
  marketId: string;
  selectedOption: string;
  users: string[];
};

export default function PoolOverviewPage() {
  const params = useParams<{ poolId: string }>();
  const poolId = params.poolId;
  const [pool, setPool] = useState<PoolSummary | null>(null);
  const [events, setEvents] = useState<TournamentEvent[]>([]);
  const [markets, setMarkets] = useState<Market[]>([]);
  const [marketPredictionSummaries, setMarketPredictionSummaries] = useState<
    MarketPredictionSummary[]
  >([]);
  const [leaderboard, setLeaderboard] = useState<LeaderboardEntry[]>([]);
  const [joinRequests, setJoinRequests] = useState<PoolJoinRequest[]>([]);
  const [selectedMarketId, setSelectedMarketId] = useState("");
  const [selectedOption, setSelectedOption] = useState("");
  const [stake, setStake] = useState(100);
  const [name, setName] = useState("");
  const [startingBalance, setStartingBalance] = useState(1000);
  const [predictionsLocked, setPredictionsLocked] = useState(false);
  const [status, setStatus] = useState("Loading pool...");
  const [joinRequestStatus, setJoinRequestStatus] = useState("");
  const [isLoadingJoinRequests, setIsLoadingJoinRequests] = useState(false);
  const [showManagementRow, setShowManagementRow] = useState(false);
  const [showRecentClosedMarkets, setShowRecentClosedMarkets] = useState(true);
  const [recentPrediction, setRecentPrediction] =
    useState<RecentPrediction | null>(null);

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
    setPredictionsLocked(result.predictionsLocked);
    setStatus("Pool loaded.");
    await Promise.all([
      loadEvents(result.tournamentId),
      loadMarkets(result.id),
      loadMarketPredictionSummaries(result.id),
      loadLeaderboard(result.id),
      canManagePool(result) ? loadJoinRequests(result.id) : Promise.resolve(),
    ]);
  }

  async function loadEvents(tournamentId: string) {
    const response = await fetch(
      apiUrl(`/api/tournaments/${tournamentId}/events`),
    );
    if (response.ok) {
      setEvents((await response.json()) as TournamentEvent[]);
    }
  }

  async function loadMarkets(targetPoolId: string) {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    const response = await fetch(apiUrl(`/api/pools/${targetPoolId}/markets`), {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!response.ok) {
      setStatus(await readApiError(response, "Could not load markets."));
      return;
    }

    const result = (await response.json()) as Market[];
    setMarkets(result);
    setSelectedMarketId((current) => current || result[0]?.id || "");
  }

  async function loadMarketPredictionSummaries(targetPoolId: string) {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    const response = await fetch(
      apiUrl(`/api/predictions/pool/${targetPoolId}/market-summaries`),
      {
        headers: { Authorization: `Bearer ${token}` },
      },
    );
    if (response.ok) {
      setMarketPredictionSummaries(
        (await response.json()) as MarketPredictionSummary[],
      );
    }
  }

  async function loadLeaderboard(targetPoolId: string) {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    const response = await fetch(
      apiUrl(`/api/predictions/pool/${targetPoolId}/leaderboard`),
      {
        headers: { Authorization: `Bearer ${token}` },
      },
    );
    if (response.ok) {
      setLeaderboard((await response.json()) as LeaderboardEntry[]);
    }
  }

  async function loadJoinRequests(targetPoolId: string) {
    const token = getStoredToken();
    if (!token) {
      setJoinRequestStatus("Session is missing.");
      return;
    }

    setIsLoadingJoinRequests(true);
    setJoinRequestStatus("Loading join requests...");
    try {
      const response = await fetch(
        apiUrl(`/api/pools/${targetPoolId}/join-requests`),
        {
          headers: { Authorization: `Bearer ${token}` },
        },
      );
      if (!response.ok) {
        setJoinRequestStatus(
          await readApiError(response, "Could not load join requests."),
        );
        return;
      }

      const result = ((await response.json()) as PoolJoinRequest[]).filter(
        (request) => request.status === "Pending",
      );
      setJoinRequests(result);
      setShowManagementRow(result.length > 0);
      setJoinRequestStatus(
        result.length === 0
          ? "No pending join requests."
          : `${result.length} pending join request${result.length === 1 ? "" : "s"}.`,
      );
    } catch (error) {
      setJoinRequestStatus(
        error instanceof Error
          ? error.message
          : "Could not load join requests.",
      );
    } finally {
      setIsLoadingJoinRequests(false);
    }
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
      body: JSON.stringify({ name, startingBalance, predictionsLocked }),
    });

    if (!response.ok) {
      setStatus(await readApiError(response, "Pool update failed."));
      return;
    }

    setStatus("Pool updated.");
    await loadPool();
  }

  async function submitPrediction(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const token = getStoredToken();
    if (!token || !pool || !selectedMarketId) {
      return;
    }

    const market = markets.find((item) => item.id === selectedMarketId);
    const option =
      market?.type === "CorrectScore" ? selectedOption.trim() : selectedOption;
    if (!option) {
      setStatus("Select a prediction option.");
      return;
    }
    const matchEvent = market
      ? events.find((item) => item.id === market.eventId)
      : null;

    const response = await fetch(apiUrl("/api/predictions"), {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        poolId: pool.id,
        marketId: selectedMarketId,
        selectedOption: option,
        stake,
      }),
    });

    if (!response.ok) {
      setStatus(await readApiError(response, "Prediction submission failed."));
      return;
    }

    setStatus("Prediction submitted.");
    if (market) {
      setRecentPrediction({
        eventName: matchEvent ? formatMatchName(matchEvent) : "Selected event",
        marketType: market.type,
        marketPeriod: market.period,
        selectedOption: option,
        stake,
        payoutMultiplier: market.payoutMultiplier,
        submittedAt: new Date().toISOString(),
      });
    }
    setSelectedOption("");
    await Promise.all([
      loadLeaderboard(pool.id),
      loadMarketPredictionSummaries(pool.id),
    ]);
  }

  async function decideJoinRequest(
    requestId: string,
    decision: "approve" | "deny",
  ) {
    const token = getStoredToken();
    if (!token || !pool) {
      return;
    }

    setJoinRequestStatus(
      decision === "approve" ? "Approving request..." : "Denying request...",
    );
    const response = await fetch(
      apiUrl(`/api/pools/${pool.id}/join-requests/${requestId}/${decision}`),
      {
        method: "POST",
        headers: { Authorization: `Bearer ${token}` },
      },
    );

    if (!response.ok) {
      setJoinRequestStatus(
        await readApiError(response, "Could not update join request."),
      );
      return;
    }

    setJoinRequestStatus(
      decision === "approve" ? "Request approved." : "Request denied.",
    );
    await Promise.all([loadJoinRequests(pool.id), loadPool()]);
  }

  const selectedMarket =
    markets.find((market) => market.id === selectedMarketId) ?? null;
  const selectedEvent = selectedMarket
    ? events.find((event) => event.id === selectedMarket.eventId)
    : null;
  const selectedMarketAvailability = selectedMarket
    ? getMarketAvailability(
        selectedMarket,
        selectedEvent ?? undefined,
        pool?.predictionsLocked ?? false,
      )
    : { isAvailable: false, reason: "No market selected" };
  const marketGroups = events
    .map((matchEvent) => {
      return {
        event: matchEvent,
        markets: markets
          .filter((market) => market.eventId === matchEvent.id)
          .sort(compareMarketsForDisplay),
      };
    })
    .filter((group) => group.markets.length > 0);
  const upcomingMarketGroups = marketGroups.filter((group) =>
    shouldShowUpcomingMarketGroup(group.event),
  );
  const recentClosedMarketGroups = marketGroups.filter((group) =>
    shouldShowRecentClosedMarketGroup(group.event),
  );

  return (
    <UserShell>
      <section className="pageStack">
        <PageHeader
          eyebrow="Pool overview"
          title={pool?.name ?? "Pool"}
          icon={Waves}
          actions={
            pool && canManageInvites(pool) ? (
              <Link
                className="button buttonSecondary"
                href={`/pools/${pool.id}/invites`}
              >
                <IconLabel icon={KeyRound}>Invites</IconLabel>
              </Link>
            ) : null
          }
        />
        <StatusPill icon={ShieldCheck}>{status}</StatusPill>
        {pool ? (
          <div className="poolOverviewGrid">
            <Panel className="poolSummaryPanel" title="Summary">
              <StatGrid
                items={[
                  { label: "Members", value: pool.memberCount, icon: Users },
                  { label: "Role", value: pool.role, icon: ShieldCheck },
                  {
                    label: "Prediction status",
                    value: pool.predictionsLocked ? "Locked" : "Open",
                    icon: Lock,
                  },
                  { label: "Profile", value: pool.profile, icon: Waves },
                ]}
              />
            </Panel>
            <Panel className="poolLeaderboardPanel" title="Leaderboard">
              <div className="poolLeaderboardList">
                {leaderboard.length === 0 ? (
                  <p className="mutedText">No leaderboard entries yet.</p>
                ) : (
                  leaderboard.map((entry, index) => (
                    <article
                      className={[
                        "poolLeaderboardRow",
                        entry.memberId === pool.memberId ? "active" : "",
                      ]
                        .filter(Boolean)
                        .join(" ")}
                      key={entry.memberId}
                    >
                      <span>
                        <strong>
                          #{index + 1} {entry.displayName}
                        </strong>
                        <small>{entry.role}</small>
                      </span>
                      <span>
                        <strong>{entry.balance}</strong>
                        <small>Balance</small>
                      </span>
                      <span>
                        <strong>{entry.winRate}%</strong>
                        <small>Win rate</small>
                      </span>
                    </article>
                  ))
                )}
              </div>
            </Panel>
          </div>
        ) : null}
        {pool && canManagePool(pool) ? (
          <section className="poolManagementSection">
            <button
              className="closedMarketToggle"
              type="button"
              onClick={() => setShowManagementRow((current) => !current)}
            >
              <IconLabel icon={showManagementRow ? ChevronDown : ChevronRight}>
                Owner controls
              </IconLabel>
              <span>{joinRequests.length}</span>
            </button>
            {showManagementRow ? (
              <div className="poolManagementGrid">
                <form
                  className="form panel poolSettingsForm"
                  onSubmit={updatePool}
                >
                  <h2>
                    <IconLabel icon={Settings}>Settings</IconLabel>
                  </h2>
                  <label>
                    Pool name
                    <input
                      required
                      type="text"
                      value={name}
                      onChange={(event) => setName(event.target.value)}
                    />
                  </label>
                  <label>
                    Starting balance
                    <input
                      min={1}
                      required
                      type="number"
                      value={startingBalance}
                      onChange={(event) =>
                        setStartingBalance(Number(event.target.value))
                      }
                    />
                  </label>
                  <label className="checkboxRow poolPredictionLockToggle">
                    <input
                      checked={predictionsLocked}
                      type="checkbox"
                      onChange={(event) =>
                        setPredictionsLocked(event.target.checked)
                      }
                    />
                    Lock predictions
                  </label>
                  <button className="button compactButton" type="submit">
                    <IconLabel icon={Save}>Save</IconLabel>
                  </button>
                </form>
                <Panel
                  className="pendingJoinPanel"
                  title="Pending join requests"
                >
                  {joinRequestStatus ? (
                    <p className="statusText">{joinRequestStatus}</p>
                  ) : null}
                  <div className="buttonRow">
                    <button
                      className="button buttonSecondary compactButton"
                      disabled={isLoadingJoinRequests}
                      type="button"
                      onClick={() => loadJoinRequests(pool.id)}
                    >
                      <IconLabel icon={RefreshCw}>Refresh</IconLabel>
                    </button>
                  </div>
                  {joinRequests.length === 0 ? (
                    <p className="mutedText">No pending join requests.</p>
                  ) : (
                    <div className="joinRequestList">
                      {joinRequests.map((request) => (
                        <article className="joinRequestRow" key={request.id}>
                          <span>
                            <strong>{request.displayName}</strong>
                            <small>{request.email}</small>
                          </span>
                          <span>
                            <small>
                              {new Date(request.requestedAt).toLocaleString()}
                            </small>
                          </span>
                          <span className="joinRequestActions">
                            <button
                              className="button buttonSecondary compactButton"
                              type="button"
                              onClick={() =>
                                decideJoinRequest(request.id, "deny")
                              }
                            >
                              <IconLabel icon={UserX}>Deny</IconLabel>
                            </button>
                            <button
                              className="button compactButton"
                              type="button"
                              onClick={() =>
                                decideJoinRequest(request.id, "approve")
                              }
                            >
                              <IconLabel icon={UserCheck}>Approve</IconLabel>
                            </button>
                          </span>
                        </article>
                      ))}
                    </div>
                  )}
                </Panel>
              </div>
            ) : null}
          </section>
        ) : null}
        {pool ? (
          <div className="predictionGrid">
            <Panel title="Markets">
              <div className="marketList">
                {upcomingMarketGroups.map((group) => (
                  <section className="marketGroup" key={group.event.id}>
                    <div className="marketGroupHeader">
                      <strong>{formatMatchName(group.event)}</strong>
                      <small>
                        <IconLabel icon={CalendarClock}>
                          {new Date(group.event.startsAt).toLocaleString()}
                        </IconLabel>
                      </small>
                    </div>
                    {getResultText(group.event) ? (
                      <div className="eventResultStrip">
                        <span>
                          <strong>FT</strong>
                          {getResultText(group.event)?.fullTime}
                        </span>
                        <span>
                          <strong>HT</strong>
                          {getResultText(group.event)?.firstHalf}
                        </span>
                      </div>
                    ) : null}
                    <MarketGroupButtons
                      event={group.event}
                      marketPredictionSummaries={marketPredictionSummaries}
                      markets={group.markets}
                      poolPredictionsLocked={pool.predictionsLocked}
                      selectedMarketId={selectedMarketId}
                      selectedOption={selectedOption}
                      setSelectedMarketId={setSelectedMarketId}
                      setSelectedOption={setSelectedOption}
                    />
                  </section>
                ))}
                {recentClosedMarketGroups.length > 0 ? (
                  <section className="closedMarketPanel">
                    <button
                      className="closedMarketToggle"
                      type="button"
                      onClick={() =>
                        setShowRecentClosedMarkets((current) => !current)
                      }
                    >
                      <IconLabel
                        icon={
                          showRecentClosedMarkets ? ChevronDown : ChevronRight
                        }
                      >
                        Recent closed matches
                      </IconLabel>
                      <span>{recentClosedMarketGroups.length}</span>
                    </button>
                    {showRecentClosedMarkets ? (
                      <div className="closedMarketList">
                        {recentClosedMarketGroups.map((group) => (
                          <section className="marketGroup" key={group.event.id}>
                            <div className="marketGroupHeader">
                              <strong>{formatMatchName(group.event)}</strong>
                              <small>
                                <IconLabel icon={CalendarClock}>
                                  {group.event.status} |{" "}
                                  {new Date(
                                    group.event.startsAt,
                                  ).toLocaleString()}
                                </IconLabel>
                              </small>
                            </div>
                            {getResultText(group.event) ? (
                              <div className="eventResultStrip">
                                <span>
                                  <strong>FT</strong>
                                  {getResultText(group.event)?.fullTime}
                                </span>
                                <span>
                                  <strong>HT</strong>
                                  {getResultText(group.event)?.firstHalf}
                                </span>
                              </div>
                            ) : null}
                            <MarketGroupButtons
                              event={group.event}
                              marketPredictionSummaries={
                                marketPredictionSummaries
                              }
                              markets={group.markets}
                              poolPredictionsLocked={pool.predictionsLocked}
                              selectedMarketId={selectedMarketId}
                              selectedOption={selectedOption}
                              setSelectedMarketId={setSelectedMarketId}
                              setSelectedOption={setSelectedOption}
                            />
                          </section>
                        ))}
                      </div>
                    ) : null}
                  </section>
                ) : null}
              </div>
            </Panel>
            <Panel
              className={`predictionSubmitPanel ${pool.predictionsLocked ? "predictionSubmitPanelLocked" : ""}`}
              title="Chốt kèo đê"
            >
              <form className="form predictionForm" onSubmit={submitPrediction}>
                {pool.predictionsLocked ? (
                  <div className="predictionLockedNotice">
                    <Lock aria-hidden="true" size={18} />
                    <span>
                      <strong>Predictions locked</strong>
                      <small>
                        The pool owner has paused prediction submission.
                      </small>
                    </span>
                  </div>
                ) : null}
                <div className="selectedMarket">
                  <Goal aria-hidden="true" size={18} />
                  <span>
                    <strong>
                      {selectedEvent
                        ? formatMatchName(selectedEvent)
                        : "Select a market"}
                    </strong>
                    <small>
                      {selectedMarket
                        ? selectedMarketAvailability.isAvailable
                          ? `${selectedMarket.period} ${formatMarketTypeLabel(selectedMarket.type)} at ${selectedMarket.payoutMultiplier}x`
                          : selectedMarketAvailability.reason
                        : "No market selected"}
                    </small>
                  </span>
                </div>
                {selectedMarket ? (
                  selectedMarket.type === "CorrectScore" ? (
                    <label>
                      Score
                      <input
                        placeholder="2-1"
                        value={selectedOption}
                        onChange={(event) =>
                          setSelectedOption(event.target.value)
                        }
                      />
                    </label>
                  ) : (
                    <label>
                      Chọn
                      <select
                        value={selectedOption}
                        onChange={(event) =>
                          setSelectedOption(event.target.value)
                        }
                      >
                        <option value="">---</option>
                        {getMarketOptions(
                          selectedMarket,
                          selectedEvent ?? undefined,
                        ).map((option) => (
                          <option key={option} value={option}>
                            {formatMarketOptionDisplay(option, selectedEvent)}
                          </option>
                        ))}
                      </select>
                    </label>
                  )
                ) : null}
                <label>
                  Điểm
                  <input
                    min={1}
                    required
                    type="number"
                    value={stake}
                    onChange={(event) => setStake(Number(event.target.value))}
                  />
                </label>
                <button
                  className="button"
                  disabled={
                    pool.predictionsLocked ||
                    !selectedMarket ||
                    !selectedOption ||
                    stake <= 0 ||
                    !selectedMarketAvailability.isAvailable
                  }
                  type="submit"
                >
                  <IconLabel icon={Send}>Dứt</IconLabel>
                </button>
                <Link
                  className="button buttonSecondary"
                  href={`/pools/${pool.id}/predictions`}
                >
                  <IconLabel icon={History}>Xem lịch sử dự đoán</IconLabel>
                </Link>
                {recentPrediction ? (
                  <div className="recentPredictionCard">
                    <strong>Prediction submitted</strong>
                    <span>{recentPrediction.eventName}</span>
                    <small>
                      {recentPrediction.marketPeriod}{" "}
                      {formatMarketTypeLabel(recentPrediction.marketType)} |{" "}
                      {formatMarketOptionLabel(recentPrediction.selectedOption)}{" "}
                      | {recentPrediction.stake} Điểm |{" "}
                      {recentPrediction.payoutMultiplier}x
                    </small>
                    <small>
                      {new Date(recentPrediction.submittedAt).toLocaleString()}
                    </small>
                  </div>
                ) : null}
              </form>
            </Panel>
          </div>
        ) : null}
      </section>
    </UserShell>
  );
}

type MarketGroupButtonsProps = {
  event: TournamentEvent;
  marketPredictionSummaries: MarketPredictionSummary[];
  markets: Market[];
  poolPredictionsLocked: boolean;
  selectedMarketId: string;
  selectedOption: string;
  setSelectedMarketId: Dispatch<SetStateAction<string>>;
  setSelectedOption: Dispatch<SetStateAction<string>>;
};

function MarketGroupButtons({
  event,
  marketPredictionSummaries,
  markets,
  poolPredictionsLocked,
  selectedMarketId,
  selectedOption,
  setSelectedMarketId,
  setSelectedOption,
}: MarketGroupButtonsProps) {
  return (
    <>
      {markets
        .filter((market) => market.type === "OneXTwo")
        .map((market) => {
          const availability = getMarketAvailability(
            market,
            event,
            poolPredictionsLocked,
          );
          return (
            <div className="oneXTwoMarketRow" key={market.id}>
              {getMarketOptions(market, event).map((option) => (
                <button
                  className={[
                    "oneXTwoOption",
                    market.id === selectedMarketId && selectedOption === option
                      ? "active"
                      : "",
                    availability.isAvailable ? "" : "disabled",
                  ]
                    .filter(Boolean)
                    .join(" ")}
                  disabled={!availability.isAvailable}
                  key={`${market.id}-${option}`}
                  type="button"
                  onClick={() => {
                    setSelectedMarketId(market.id);
                    setSelectedOption(option);
                  }}
                >
                  <strong>{formatOneXTwoOptionLabel(option, event)}</strong>
                  <span>{formatOneXTwoOptionScore(option, event)}</span>
                  <small>
                    {formatPredictionUsers(
                      getPredictionUsers(
                        marketPredictionSummaries,
                        market.id,
                        option,
                      ),
                    )}
                  </small>
                </button>
              ))}
            </div>
          );
        })}
      <div className="marketButtonGrid">
        {markets
          .filter((market) => market.type !== "OneXTwo")
          .map((market) => {
            const availability = getMarketAvailability(
              market,
              event,
              poolPredictionsLocked,
            );
            const isPendingHandicapLine = isHandicapLinePending(market);
            return (
              <button
                className={[
                  "marketButton",
                  market.id === selectedMarketId ? "active" : "",
                  availability.isAvailable ? "" : "disabled",
                  poolPredictionsLocked ? "poolLocked" : "",
                  isPendingHandicapLine ? "linePending" : "",
                ]
                  .filter(Boolean)
                  .join(" ")}
                disabled={!availability.isAvailable}
                key={market.id}
                type="button"
                onClick={() => {
                  setSelectedMarketId(market.id);
                  setSelectedOption("");
                }}
              >
                <strong>{formatMarketCardTitle(market)}</strong>
                <span>
                  {formatMarketPredictionUsers(
                    marketPredictionSummaries,
                    market,
                  )}
                </span>
              </button>
            );
          })}
      </div>
    </>
  );
}

function canManagePool(pool: PoolSummary) {
  return pool.role === "Owner" || pool.role === "Admin";
}

function canManageInvites(pool: PoolSummary) {
  return pool.role === "Owner";
}

function formatMatchName(matchEvent: TournamentEvent) {
  return `${formatParticipantName(matchEvent.homeParticipant, matchEvent.homeParticipantCode)} -vs- ${formatParticipantName(matchEvent.awayParticipant, matchEvent.awayParticipantCode)}`;
}

const scheduledMarketWindowMs = 48 * 60 * 60 * 1000;
const recentClosedMarketWindowMs = 24 * 60 * 60 * 1000;

function shouldShowUpcomingMarketGroup(matchEvent: TournamentEvent) {
  const now = Date.now();
  const startsAt = new Date(matchEvent.startsAt).getTime();
  return (
    matchEvent.status === "Scheduled" &&
    startsAt > now &&
    startsAt <= now + scheduledMarketWindowMs
  );
}

function shouldShowRecentClosedMarketGroup(matchEvent: TournamentEvent) {
  const recentClosedStatuses = new Set([
    "Finished",
    "Settled",
    "Cancelled",
    "Postponed",
  ]);
  if (!recentClosedStatuses.has(matchEvent.status)) {
    return false;
  }

  const now = Date.now();
  const startsAt = new Date(matchEvent.startsAt).getTime();
  return Math.abs(startsAt - now) <= recentClosedMarketWindowMs;
}

function getResultText(matchEvent: TournamentEvent) {
  return matchEvent.status === "Finished" || matchEvent.status === "Settled"
    ? {
        fullTime: formatScore(
          matchEvent.fullTimeHomeScore,
          matchEvent.fullTimeAwayScore,
        ),
        firstHalf: formatScore(
          matchEvent.firstHalfHomeScore,
          matchEvent.firstHalfAwayScore,
        ),
      }
    : null;
}

function getMarketAvailability(
  market: Market,
  matchEvent: TournamentEvent | undefined,
  predictionsLocked = false,
) {
  if (predictionsLocked) {
    return { isAvailable: false, reason: "Pool locked" };
  }

  if (market.status !== "Open") {
    return {
      isAvailable: false,
      reason: market.status === "LinePending" ? "Line pending" : market.status,
    };
  }

  if (!matchEvent) {
    return { isAvailable: true, reason: "" };
  }

  if (matchEvent.status !== "Scheduled") {
    return {
      isAvailable: false,
      reason: `Event status is ${matchEvent.status}`,
    };
  }

  const startsAt = new Date(matchEvent.startsAt).getTime();
  const now = Date.now();
  if (startsAt <= now) {
    return { isAvailable: false, reason: "Locked" };
  }

  if (market.type === "Handicap") {
    if (market.lineValue === null) {
      return { isAvailable: false, reason: "Line pending" };
    }

    const opensAt = startsAt - 24 * 60 * 60 * 1000;
    if (now < opensAt) {
      return { isAvailable: false, reason: "Opens 24h before kickoff" };
    }
  }

  return { isAvailable: true, reason: "" };
}

function formatScore(homeScore: number | null, awayScore: number | null) {
  return homeScore === null || awayScore === null
    ? "-"
    : `${homeScore}-${awayScore}`;
}

function isHandicapLinePending(market: Market) {
  return (
    market.type === "Handicap" &&
    (market.status === "LinePending" || market.lineValue === null)
  );
}

function compareMarketsForDisplay(first: Market, second: Market) {
  const periodDifference =
    getMarketPeriodOrder(first.period) - getMarketPeriodOrder(second.period);
  if (periodDifference !== 0) {
    return periodDifference;
  }

  return getMarketTypeOrder(first.type) - getMarketTypeOrder(second.type);
}

function getMarketPeriodOrder(period: string) {
  return period === "FullTime" ? 0 : period === "FirstHalf" ? 1 : 2;
}

function getMarketTypeOrder(type: string) {
  const order: Record<string, number> = {
    OneXTwo: 0,
    Handicap: 1,
    OverUnder: 2,
    OddEven: 3,
    CorrectScore: 4,
  };

  return order[type] ?? 99;
}

function formatMarketPeriodLabel(period: string) {
  return period === "FullTime" ? "FT" : period === "FirstHalf" ? "HT" : period;
}

function getMarketOptions(
  market: Market,
  matchEvent: TournamentEvent | undefined,
) {
  const home = matchEvent?.homeParticipant ?? "Home";
  const away = matchEvent?.awayParticipant ?? "Away";

  switch (market.type) {
    case "OneXTwo":
      return [home, "Draw", away];
    case "Handicap":
      return [
        `${home} ${formatLine(market.lineValue)}`,
        `${away} ${formatLine(market.lineValue === null ? null : -market.lineValue)}`,
      ];
    case "OverUnder":
      return [
        `Over ${market.lineValue ?? ""}`.trim(),
        `Under ${market.lineValue ?? ""}`.trim(),
      ];
    case "OddEven":
      return ["Odd", "Even"];
    default:
      return [];
  }
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

function formatMarketCardTitle(market: Market) {
  const payoutRate = formatPayoutRate(market.payoutMultiplier);
  if (market.type === "OverUnder") {
    return `${formatMarketTypeLabel(market.type)} (${market.lineValue ?? "-"} @${payoutRate})`;
  }

  if (market.type === "OddEven" || market.type === "CorrectScore") {
    return `${formatMarketTypeLabel(market.type)} (${payoutRate})`;
  }

  return `${formatMarketTypeLabel(market.type)} (${formatMarketPeriodLabel(market.period)} @${payoutRate})`;
}

function formatPayoutRate(payoutMultiplier: number) {
  const profit = Math.max(0, payoutMultiplier - 1);
  return `1:${formatCompactNumber(profit)}`;
}

function formatCompactNumber(value: number) {
  return Number.isInteger(value)
    ? `${value}`
    : value.toFixed(2).replace(/0+$/, "").replace(/\.$/, "");
}

function formatOneXTwoOptionScore(option: string, matchEvent: TournamentEvent) {
  if (option === "Draw") {
    return "-";
  }

  if (option === matchEvent.homeParticipant) {
    return `${matchEvent.fullTimeHomeScore ?? 0}`;
  }

  if (option === matchEvent.awayParticipant) {
    return `${matchEvent.fullTimeAwayScore ?? 0}`;
  }

  return "0";
}

function formatOneXTwoOptionLabel(option: string, matchEvent: TournamentEvent) {
  if (option === matchEvent.homeParticipant) {
    return formatParticipantName(option, matchEvent.homeParticipantCode);
  }

  if (option === matchEvent.awayParticipant) {
    return formatParticipantName(option, matchEvent.awayParticipantCode);
  }

  return formatMarketOptionLabel(option);
}

function formatMarketOptionDisplay(
  option: string,
  matchEvent?: TournamentEvent | null,
) {
  if (!matchEvent) {
    return formatMarketOptionLabel(option);
  }

  if (option.startsWith(`${matchEvent.homeParticipant} `)) {
    return option.replace(
      matchEvent.homeParticipant,
      formatParticipantName(
        matchEvent.homeParticipant,
        matchEvent.homeParticipantCode,
      ),
    );
  }

  if (option.startsWith(`${matchEvent.awayParticipant} `)) {
    return option.replace(
      matchEvent.awayParticipant,
      formatParticipantName(
        matchEvent.awayParticipant,
        matchEvent.awayParticipantCode,
      ),
    );
  }

  return formatOneXTwoOptionLabel(option, matchEvent);
}

function getPredictionUsers(
  summaries: MarketPredictionSummary[],
  marketId: string,
  selectedOption?: string,
) {
  return summaries
    .filter(
      (summary) =>
        summary.marketId === marketId &&
        (selectedOption === undefined ||
          summary.selectedOption === selectedOption),
    )
    .flatMap((summary) => summary.users);
}

function formatPredictionUsers(users: string[]) {
  return users.length === 0 ? "" : users.join(", ");
}

function formatMarketPredictionUsers(
  summaries: MarketPredictionSummary[],
  market: Market,
) {
  const marketSummaries = summaries.filter(
    (summary) => summary.marketId === market.id,
  );
  if (marketSummaries.length === 0) {
    return "";
  }

  return marketSummaries
    .map(
      (summary) =>
        `${formatMarketOptionLabel(summary.selectedOption)}: ${summary.users.join(", ")}`,
    )
    .join(" | ");
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

function formatLine(value: number | null) {
  if (value === null) {
    return "";
  }

  return value > 0 ? `+${value}` : `${value}`;
}

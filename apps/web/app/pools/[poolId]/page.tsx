"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { BadgeDollarSign, CalendarClock, Goal, History, KeyRound, Save, Send, Settings, ShieldCheck, UserCheck, UserX, Users, Waves } from "lucide-react";
import { UserShell } from "../../components/user-shell";
import { IconLabel, PageHeader, Panel, StatGrid, StatusPill } from "../../components/ui";
import { apiUrl, readApiError } from "../../lib/api";
import { getStoredToken } from "../../lib/auth";
import { LeaderboardEntry, Market, PoolJoinRequest, PoolSummary, Prediction, TournamentEvent } from "../../lib/types";

type BalanceResponse = {
  balance: number;
};

export default function PoolOverviewPage() {
  const params = useParams<{ poolId: string }>();
  const poolId = params.poolId;
  const [pool, setPool] = useState<PoolSummary | null>(null);
  const [events, setEvents] = useState<TournamentEvent[]>([]);
  const [markets, setMarkets] = useState<Market[]>([]);
  const [predictions, setPredictions] = useState<Prediction[]>([]);
  const [leaderboard, setLeaderboard] = useState<LeaderboardEntry[]>([]);
  const [joinRequests, setJoinRequests] = useState<PoolJoinRequest[]>([]);
  const [selectedMarketId, setSelectedMarketId] = useState("");
  const [selectedOption, setSelectedOption] = useState("");
  const [stake, setStake] = useState(100);
  const [balance, setBalance] = useState<number | null>(null);
  const [name, setName] = useState("");
  const [startingBalance, setStartingBalance] = useState(1000);
  const [status, setStatus] = useState("Loading pool...");
  const [joinRequestStatus, setJoinRequestStatus] = useState("");

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
    await Promise.all([
      loadEvents(result.tournamentId),
      loadMarkets(result.id),
      loadPredictions(result.id),
      loadLeaderboard(result.id),
      loadBalance(result.id),
      canManagePool(result) ? loadJoinRequests(result.id) : Promise.resolve(),
    ]);
  }

  async function loadEvents(tournamentId: string) {
    const response = await fetch(apiUrl(`/api/tournaments/${tournamentId}/events`));
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

  async function loadPredictions(targetPoolId: string) {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    const response = await fetch(apiUrl(`/api/predictions/pool/${targetPoolId}`), {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (response.ok) {
      setPredictions((await response.json()) as Prediction[]);
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

  async function loadBalance(targetPoolId: string) {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    const response = await fetch(apiUrl(`/api/predictions/balance?poolId=${targetPoolId}`), {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (response.ok) {
      const result = (await response.json()) as BalanceResponse;
      setBalance(result.balance);
    }
  }

  async function loadJoinRequests(targetPoolId: string) {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    const response = await fetch(apiUrl(`/api/pools/${targetPoolId}/join-requests`), {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (response.ok) {
      setJoinRequests((await response.json()) as PoolJoinRequest[]);
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
      body: JSON.stringify({ name, startingBalance }),
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
    const option = market?.type === "CorrectScore" ? selectedOption.trim() : selectedOption;
    if (!option) {
      setStatus("Select a prediction option.");
      return;
    }

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
    setSelectedOption("");
    await Promise.all([loadPredictions(pool.id), loadLeaderboard(pool.id), loadBalance(pool.id)]);
  }

  async function decideJoinRequest(requestId: string, decision: "approve" | "deny") {
    const token = getStoredToken();
    if (!token || !pool) {
      return;
    }

    setJoinRequestStatus(decision === "approve" ? "Approving request..." : "Denying request...");
    const response = await fetch(apiUrl(`/api/pools/${pool.id}/join-requests/${requestId}/${decision}`), {
      method: "POST",
      headers: { Authorization: `Bearer ${token}` },
    });

    if (!response.ok) {
      setJoinRequestStatus(await readApiError(response, "Could not update join request."));
      return;
    }

    setJoinRequestStatus(decision === "approve" ? "Request approved." : "Request denied.");
    await Promise.all([loadJoinRequests(pool.id), loadPool()]);
  }

  const selectedMarket = markets.find((market) => market.id === selectedMarketId) ?? null;
  const selectedEvent = selectedMarket ? events.find((event) => event.id === selectedMarket.eventId) : null;
  const groupedMarkets = events
    .map((matchEvent) => ({
      event: matchEvent,
      markets: markets.filter((market) => market.eventId === matchEvent.id),
    }))
    .filter((group) => group.markets.length > 0);

  return (
    <UserShell>
    <section className="pageStack">
      <PageHeader
        eyebrow="Pool overview"
        title={pool?.name ?? "Pool"}
        icon={Waves}
        actions={pool ? <Link className="button buttonSecondary" href={`/pools/${pool.id}/invites`}><IconLabel icon={KeyRound}>Invites</IconLabel></Link> : null}
      />
      <StatusPill icon={ShieldCheck}>{status}</StatusPill>
      {pool ? (
        <div className="poolGrid">
          <Panel title="Summary">
            <StatGrid
              items={[
                { label: "Role", value: pool.role, icon: ShieldCheck },
                { label: "Members", value: pool.memberCount, icon: Users },
                { label: "Invites", value: pool.inviteCount, icon: KeyRound },
                { label: "Balance", value: balance ?? pool.startingBalance, icon: BadgeDollarSign }
              ]}
            />
            <dl className="detailList">
              <div><dt>Role</dt><dd>{pool.role}</dd></div>
              <div><dt>Profile</dt><dd>{pool.profile}</dd></div>
              <div><dt>Members</dt><dd>{pool.memberCount}</dd></div>
              <div><dt>Invites</dt><dd>{pool.inviteCount}</dd></div>
              <div><dt>Starting balance</dt><dd>{pool.startingBalance}</dd></div>
            </dl>
          </Panel>
          <form className="form panel" onSubmit={updatePool}>
            <h2><IconLabel icon={Settings}>Settings</IconLabel></h2>
            <label>
              Pool name
              <input required type="text" value={name} onChange={(event) => setName(event.target.value)} />
            </label>
            <label>
              Starting balance
              <input min={1} required type="number" value={startingBalance} onChange={(event) => setStartingBalance(Number(event.target.value))} />
            </label>
            <button className="button" disabled={pool.role === "Member"} type="submit"><IconLabel icon={Save}>Save pool</IconLabel></button>
          </form>
        </div>
      ) : null}
      {pool && canManagePool(pool) ? (
        <Panel title="Join requests">
          {joinRequestStatus ? <p className="statusText">{joinRequestStatus}</p> : null}
          {joinRequests.length === 0 ? (
            <p className="mutedText">No join requests yet.</p>
          ) : (
            <div className="joinRequestList">
              {joinRequests.map((request) => (
                <article className="joinRequestRow" key={request.id}>
                  <span>
                    <strong>{request.displayName}</strong>
                    <small>{request.email}</small>
                  </span>
                  <span>
                    <strong>{request.status}</strong>
                    <small>{new Date(request.requestedAt).toLocaleString()}</small>
                  </span>
                  <span className="joinRequestActions">
                    <button className="button buttonSecondary compactButton" disabled={request.status !== "Pending"} type="button" onClick={() => decideJoinRequest(request.id, "deny")}>
                      <IconLabel icon={UserX}>Deny</IconLabel>
                    </button>
                    <button className="button compactButton" disabled={request.status !== "Pending"} type="button" onClick={() => decideJoinRequest(request.id, "approve")}>
                      <IconLabel icon={UserCheck}>Approve</IconLabel>
                    </button>
                  </span>
                </article>
              ))}
            </div>
          )}
        </Panel>
      ) : null}
      {pool ? (
        <div className="predictionGrid">
          <Panel title="Markets">
            <div className="marketList">
              {groupedMarkets.map((group) => (
                <section className="marketGroup" key={group.event.id}>
                  <div className="marketGroupHeader">
                    <strong>{group.event.homeParticipant} vs {group.event.awayParticipant}</strong>
                    <small><IconLabel icon={CalendarClock}>{new Date(group.event.startsAt).toLocaleString()}</IconLabel></small>
                  </div>
                  <div className="marketButtonGrid">
                    {group.markets.map((market) => (
                      <button
                        className={market.id === selectedMarketId ? "marketButton active" : "marketButton"}
                        key={market.id}
                        type="button"
                        onClick={() => {
                          setSelectedMarketId(market.id);
                          setSelectedOption("");
                        }}
                      >
                        <strong>{market.type}</strong>
                        <span>{market.period}{market.lineValue !== null ? ` line ${market.lineValue}` : ""}</span>
                        <small>{market.payoutMultiplier}x payout</small>
                      </button>
                    ))}
                  </div>
                </section>
              ))}
            </div>
          </Panel>
          <Panel title="Submit prediction">
            <form className="form predictionForm" onSubmit={submitPrediction}>
              <div className="selectedMarket">
                <Goal aria-hidden="true" size={18} />
                <span>
                  <strong>{selectedEvent ? `${selectedEvent.homeParticipant} vs ${selectedEvent.awayParticipant}` : "Select a market"}</strong>
                  <small>{selectedMarket ? `${selectedMarket.period} ${selectedMarket.type} at ${selectedMarket.payoutMultiplier}x` : "No market selected"}</small>
                </span>
              </div>
              {selectedMarket ? (
                selectedMarket.type === "CorrectScore" ? (
                  <label>
                    Score
                    <input placeholder="2-1" value={selectedOption} onChange={(event) => setSelectedOption(event.target.value)} />
                  </label>
                ) : (
                  <label>
                    Pick
                    <select value={selectedOption} onChange={(event) => setSelectedOption(event.target.value)}>
                      <option value="">Select option</option>
                      {getMarketOptions(selectedMarket, selectedEvent ?? undefined).map((option) => (
                        <option key={option} value={option}>{option}</option>
                      ))}
                    </select>
                  </label>
                )
              ) : null}
              <label>
                Stake
                <input min={1} required type="number" value={stake} onChange={(event) => setStake(Number(event.target.value))} />
              </label>
              <button className="button" disabled={!selectedMarket || !selectedOption || stake <= 0} type="submit"><IconLabel icon={Send}>Submit prediction</IconLabel></button>
            </form>
          </Panel>
          <Panel className="predictionHistoryPanel" title="Prediction history">
            {leaderboard.length > 0 ? (
              <div className="leaderboardList">
                {leaderboard.map((entry, index) => (
                  <article className={entry.memberId === pool.memberId ? "leaderboardRow active" : "leaderboardRow"} key={entry.memberId}>
                    <span>
                      <strong>#{index + 1} {entry.displayName}</strong>
                      <small>{entry.role}</small>
                    </span>
                    <span>
                      <strong>{entry.balance}</strong>
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
            ) : null}
            {predictions.length === 0 ? (
              <p className="mutedText">No predictions submitted yet.</p>
            ) : (
              <div className="predictionHistory">
                {predictions.map((prediction) => (
                  <article className="historyRow" key={prediction.id}>
                    <span>
                      <strong><IconLabel icon={History}>{prediction.marketType}</IconLabel></strong>
                      <small>{prediction.marketPeriod}</small>
                    </span>
                    <span>
                      <strong>{prediction.selectedOption}</strong>
                      <small>{prediction.stake} points</small>
                    </span>
                    <span>
                      <strong>{prediction.payoutMultiplierSnapshot}x</strong>
                      <small>{new Date(prediction.submittedAt).toLocaleString()}</small>
                    </span>
                    <span>
                      <strong>{prediction.outcome ?? "Pending"}</strong>
                      <small>{formatNetPoints(prediction.netPoints)}</small>
                    </span>
                  </article>
                ))}
              </div>
            )}
          </Panel>
        </div>
      ) : null}
    </section>
    </UserShell>
  );
}

function canManagePool(pool: PoolSummary) {
  return pool.role === "Owner" || pool.role === "Admin";
}

function formatNetPoints(value: number | undefined) {
  if (value === undefined) {
    return "Not settled";
  }

  return value > 0 ? `+${value} net` : `${value} net`;
}

function getMarketOptions(market: Market, matchEvent: TournamentEvent | undefined) {
  const home = matchEvent?.homeParticipant ?? "Home";
  const away = matchEvent?.awayParticipant ?? "Away";

  switch (market.type) {
    case "Winner":
      return [home, "Draw", away];
    case "Handicap":
      return [`${home} ${formatLine(market.lineValue)}`, `${away} ${formatLine(market.lineValue === null ? null : -market.lineValue)}`];
    case "OverUnder":
      return [`Over ${market.lineValue ?? ""}`.trim(), `Under ${market.lineValue ?? ""}`.trim()];
    case "OddEven":
      return ["Odd", "Even"];
    default:
      return [];
  }
}

function formatLine(value: number | null) {
  if (value === null) {
    return "";
  }

  return value > 0 ? `+${value}` : `${value}`;
}

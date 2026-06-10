"use client";

import Link from "next/link";
import {
  CSSProperties,
  Dispatch,
  FormEvent,
  ReactNode,
  SetStateAction,
  useEffect,
  useRef,
  useState,
} from "react";
import { useParams } from "next/navigation";
import {
  CalendarClock,
  ChevronDown,
  ChevronRight,
  Edit3,
  Eye,
  Goal,
  History,
  KeyRound,
  Lock,
  MessageSquare,
  RefreshCw,
  Save,
  Send,
  Settings,
  ShieldCheck,
  Smile,
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
  StatusPill,
} from "../../components/ui";
import { apiUrl, readApiError } from "../../lib/api";
import { getStoredToken } from "../../lib/auth";
import { formatDisplayDateTime } from "../../lib/datetime";
import {
  ParticipantName,
  formatParticipantName,
} from "../../lib/participant-flags";
import {
  LeaderboardEntry,
  Market,
  Prediction,
  PoolJoinRequest,
  PoolMessage,
  PoolMessageKind,
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

type MarketDisplayOptionsResponse = {
  scheduledDisplayWindowHours: number;
};

const supportedChatEmojis = [
  "😀",
  "😂",
  "😍",
  "😎",
  "🤔",
  "👏",
  "🙌",
  "💪",
  "🔥",
  "⚽",
  "🏆",
  "🍻",
  "✅",
  "❌",
  "👀",
  "💸",
];

export default function PoolOverviewPage() {
  const params = useParams<{ poolId: string }>();
  const poolId = params.poolId;
  const [pool, setPool] = useState<PoolSummary | null>(null);
  const [events, setEvents] = useState<TournamentEvent[]>([]);
  const [markets, setMarkets] = useState<Market[]>([]);
  const [scheduledMarketWindowHours, setScheduledMarketWindowHours] =
    useState(defaultScheduledMarketWindowHours);
  const [marketPredictionSummaries, setMarketPredictionSummaries] = useState<
    MarketPredictionSummary[]
  >([]);
  const [leaderboard, setLeaderboard] = useState<LeaderboardEntry[]>([]);
  const [joinRequests, setJoinRequests] = useState<PoolJoinRequest[]>([]);
  const [poolMessages, setPoolMessages] = useState<PoolMessage[]>([]);
  const [predictionHistory, setPredictionHistory] = useState<Prediction[]>([]);
  const [selectedMarketId, setSelectedMarketId] = useState("");
  const [selectedOption, setSelectedOption] = useState("");
  const [stake, setStake] = useState(100);
  const [name, setName] = useState("");
  const [startingBalance, setStartingBalance] = useState(1000);
  const [predictionsLocked, setPredictionsLocked] = useState(false);
  const [coverImageUrl, setCoverImageUrl] = useState("");
  const [announcementTitle, setAnnouncementTitle] = useState("Announcements");
  const [defaultStake, setDefaultStake] = useState(100);
  const [minStake, setMinStake] = useState(10);
  const [maxStake, setMaxStake] = useState(200);
  const [maxTotalStakePerEvent, setMaxTotalStakePerEvent] = useState(400);
  const [status, setStatus] = useState("Loading pool...");
  const [predictionFeedback, setPredictionFeedback] = useState("");
  const [isSavingPool, setIsSavingPool] = useState(false);
  const [joinRequestStatus, setJoinRequestStatus] = useState("");
  const [isLoadingJoinRequests, setIsLoadingJoinRequests] = useState(false);
  const [chatBody, setChatBody] = useState("");
  const [announcementDrafts, setAnnouncementDrafts] = useState<
    Record<number, { title: string; bodyMarkdown: string }>
  >({
    1: { title: "", bodyMarkdown: "" },
    2: { title: "", bodyMarkdown: "" },
  });
  const [chatEditorMode, setChatEditorMode] = useState<"write" | "preview">("write");
  const [isPostingMessage, setIsPostingMessage] = useState(false);
  const [showOverviewRow, setShowOverviewRow] = useState(() =>
    typeof window === "undefined"
      ? true
      : !window.matchMedia("(max-width: 760px)").matches,
  );
  const [showManagementRow, setShowManagementRow] = useState(false);
  const [showRecentClosedMarkets, setShowRecentClosedMarkets] = useState(true);
  const [isPredictionSlipExpanded, setIsPredictionSlipExpanded] = useState(false);
  const [isAnnouncementsExpanded, setIsAnnouncementsExpanded] = useState(false);
  const [isMobileMarketsView, setIsMobileMarketsView] = useState(false);
  const [mobileSelectedMarketEventId, setMobileSelectedMarketEventId] =
    useState("");
  const [recentPrediction, setRecentPrediction] =
    useState<RecentPrediction | null>(null);

  useEffect(() => {
    loadPool();
  }, [poolId]);

  useEffect(() => {
    const query = window.matchMedia("(max-width: 760px)");
    const updateMobileMarketsView = () => setIsMobileMarketsView(query.matches);
    updateMobileMarketsView();
    query.addEventListener("change", updateMobileMarketsView);
    return () => query.removeEventListener("change", updateMobileMarketsView);
  }, []);

  useEffect(() => {
    if (!pool?.id) {
      return;
    }

    const refreshMessages = () => {
      void loadPoolMessages(pool.id, { syncDrafts: false });
    };
    const intervalId = window.setInterval(refreshMessages, 5000);
    return () => window.clearInterval(intervalId);
  }, [pool?.id]);

  useEffect(() => {
    const mediaQuery = window.matchMedia("(max-width: 760px)");
    const syncOverviewVisibility = () => {
      setShowOverviewRow(!mediaQuery.matches);
    };

    syncOverviewVisibility();
    mediaQuery.addEventListener("change", syncOverviewVisibility);
    return () => {
      mediaQuery.removeEventListener("change", syncOverviewVisibility);
    };
  }, []);

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
    setCoverImageUrl(result.coverImageUrl ?? "");
    setAnnouncementTitle(result.announcementTitle || "Announcements");
    setDefaultStake(result.defaultStake);
    setMinStake(result.minStake);
    setMaxStake(result.maxStake);
    setMaxTotalStakePerEvent(result.maxTotalStakePerEvent);
    setStake(result.defaultStake);
    setStatus("Pool loaded.");
    await Promise.all([
      loadEvents(result.tournamentId),
      loadMarketDisplayOptions(),
      loadMarkets(result.id),
      loadPoolMessages(result.id),
      loadMarketPredictionSummaries(result.id),
      loadPredictionHistory(result.id),
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

  async function loadMarketDisplayOptions() {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    const response = await fetch(apiUrl("/api/markets/options"), {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!response.ok) {
      return;
    }

    const result = (await response.json()) as MarketDisplayOptionsResponse;
    setScheduledMarketWindowHours(
      Math.max(1, result.scheduledDisplayWindowHours),
    );
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

  async function loadPoolMessages(
    targetPoolId: string,
    options: { syncDrafts?: boolean } = {},
  ) {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    const response = await fetch(apiUrl(`/api/pools/${targetPoolId}/messages`), {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (response.ok) {
      const messages = (await response.json()) as PoolMessage[];
      setPoolMessages(messages);
      if (options.syncDrafts !== false) {
        syncAnnouncementDrafts(messages);
      }
    }
  }

  function syncAnnouncementDrafts(messages: PoolMessage[]) {
    setAnnouncementDrafts({
      1: getAnnouncementDraft(messages, 1),
      2: getAnnouncementDraft(messages, 2),
    });
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

  async function loadPredictionHistory(targetPoolId: string) {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    const response = await fetch(apiUrl(`/api/predictions/pool/${targetPoolId}`), {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (response.ok) {
      setPredictionHistory((await response.json()) as Prediction[]);
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
    await savePoolSettings();
  }

  async function savePoolSettings() {
    const token = getStoredToken();
    if (!token || !pool) {
      return;
    }

    setIsSavingPool(true);
    setStatus("Saving pool settings...");
    try {
      const response = await fetch(apiUrl(`/api/pools/${pool.id}`), {
        method: "PUT",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          name,
          startingBalance,
          predictionsLocked,
          coverImageUrl: coverImageUrl.trim() || null,
          announcementTitle,
          defaultStake,
          minStake,
          maxStake,
          maxTotalStakePerEvent,
        }),
      });

      if (!response.ok) {
        setStatus(await readApiError(response, "Pool update failed."));
        return;
      }

      setStatus("Pool updated.");
      await loadPool();
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Pool update failed.");
    } finally {
      setIsSavingPool(false);
    }
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
      const message = "Select a prediction option.";
      setStatus(message);
      setPredictionFeedback(message);
      return;
    }
    if (market?.type === "CorrectScore" && !isCorrectScoreOption(option)) {
      const message = "Correct score must use format score_number-score_number, for example 2-1.";
      setStatus(message);
      setPredictionFeedback(message);
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
      const message = await readApiError(response, "Prediction submission failed.");
      setStatus(message);
      setPredictionFeedback(message);
      return;
    }

    setStatus("Prediction submitted.");
    setPredictionFeedback("Prediction submitted.");
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
      loadPredictionHistory(pool.id),
    ]);
  }

  async function submitPoolMessage(kind: PoolMessageKind) {
    const token = getStoredToken();
    if (!token || !pool) {
      return;
    }

    const body = chatBody;
    if (!body.trim()) {
      setStatus("Message body is required.");
      return;
    }

    setIsPostingMessage(true);
    setStatus(kind === "Announcement" ? "Posting announcement..." : "Posting chat...");
    try {
      const response = await fetch(apiUrl(`/api/pools/${pool.id}/messages`), {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          kind,
          bodyMarkdown: body,
        }),
      });

      if (!response.ok) {
        setStatus(await readApiError(response, "Could not post message."));
        return;
      }

      const message = (await response.json()) as PoolMessage;
      setPoolMessages((current) => [...current, message]);
      setChatBody("");
      setChatEditorMode("write");
      setStatus(kind === "Announcement" ? "Announcement posted." : "Chat posted.");
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Could not post message.");
    } finally {
      setIsPostingMessage(false);
    }
  }

  async function submitPoolAnnouncement(slot: number) {
    const token = getStoredToken();
    if (!token || !pool) {
      return;
    }

    const draft = announcementDrafts[slot] ?? { title: "", bodyMarkdown: "" };
    setIsPostingMessage(true);
    setStatus("Saving announcement...");
    try {
      const response = await fetch(
        apiUrl(`/api/pools/${pool.id}/announcements/${slot}`),
        {
          method: "PUT",
          headers: {
            Authorization: `Bearer ${token}`,
            "Content-Type": "application/json",
          },
          body: JSON.stringify(draft),
        },
      );

      if (!response.ok) {
        setStatus(await readApiError(response, "Could not save announcement."));
        return;
      }

      const announcement = (await response.json()) as PoolMessage;
      setPoolMessages((current) => [
        ...current.filter(
          (message) =>
            message.kind !== "Announcement" ||
            message.announcementSlot !== announcement.announcementSlot,
        ),
        announcement,
      ]);
      setStatus("Announcement saved.");
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Could not save announcement.");
    } finally {
      setIsPostingMessage(false);
    }
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
  const selectedEventStakeUsed =
    pool && selectedEvent
      ? getEventStakeUsed(predictionHistory, markets, selectedEvent.id)
      : 0;
  const selectedEventStakeRemaining = pool
    ? Math.max(0, pool.maxTotalStakePerEvent - selectedEventStakeUsed)
    : 0;
  const isStakeBelowMin = pool ? stake < pool.minStake : false;
  const isStakeAboveMax = pool ? stake > pool.maxStake : false;
  const exceedsEventStakeLimit = pool
    ? selectedEventStakeUsed + stake > pool.maxTotalStakePerEvent
    : false;
  const currentMemberLeaderboardEntry = pool?.memberId
    ? leaderboard.find((entry) => entry.memberId === pool.memberId) ?? null
    : null;
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
    shouldShowUpcomingMarketGroup(group.event, scheduledMarketWindowHours),
  );
  const recentClosedMarketGroups = marketGroups.filter((group) =>
    shouldShowRecentClosedMarketGroup(group.event),
  );
  const upcomingMarketGroupIds = upcomingMarketGroups
    .map((group) => group.event.id)
    .join("|");

  useEffect(() => {
    setMobileSelectedMarketEventId((current) =>
      current && upcomingMarketGroups.some((group) => group.event.id === current)
        ? current
        : upcomingMarketGroups[0]?.event.id ?? "",
    );
  }, [upcomingMarketGroupIds]);

  const displayedUpcomingMarketGroups =
    isMobileMarketsView && mobileSelectedMarketEventId
      ? upcomingMarketGroups.filter(
          (group) => group.event.id === mobileSelectedMarketEventId,
        )
      : upcomingMarketGroups;

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
        {pool ? (
          <section
            className={[
              "poolBackdropSection",
              pool.coverImageUrl ? "hasImage" : "",
            ]
              .filter(Boolean)
              .join(" ")}
            style={
              pool.coverImageUrl
                ? {
                    ["--pool-backdrop-image" as "--pool-backdrop-image"]: `linear-gradient(180deg, rgba(3, 8, 18, 0.22) 0%, rgba(3, 8, 18, 0.58) 14%, rgba(3, 8, 18, 0.86) 40%, rgba(3, 8, 18, 0.97) 72%, var(--bg) 100%), url(${pool.coverImageUrl})`,
                  } as CSSProperties
                : undefined
            }
          >
            <div className="poolBackdropContent">
              <StatusPill icon={ShieldCheck}>{status}</StatusPill>
              <section className="poolOverviewSection">
                <button
                  className="overviewRowToggle"
                  type="button"
                  onClick={() => setShowOverviewRow((current) => !current)}
                >
                  <IconLabel icon={showOverviewRow ? ChevronDown : ChevronRight}>
                    Summary & Leaderboard
                  </IconLabel>
                </button>
                {showOverviewRow ? (
                  <div className="poolOverviewGrid">
                    <Panel className="poolSummaryPanel">
                      <h2 className="poolSummaryTitle">
                        Summary (<IconLabel icon={Users}>{pool.memberCount}</IconLabel>)
                      </h2>
                      <div className="poolSummaryCompactGrid">
                        <div className="poolSummaryStatBox">
                          <small><IconLabel icon={ShieldCheck}>Role</IconLabel></small>
                          <strong>{pool.role}</strong>
                        </div>
                        <div className="poolSummaryStatBox">
                          <small><IconLabel icon={Lock}>Prediction status</IconLabel></small>
                          <strong>{pool.predictionsLocked ? "Locked" : "Open"}</strong>
                        </div>
                        <div className="poolSummaryStatBox">
                          <small><IconLabel icon={Waves}>Profile</IconLabel></small>
                          <strong>{pool.profile}</strong>
                        </div>
                        <div className="poolSummaryStatBox">
                          <small>Your balance</small>
                          <strong>
                            {currentMemberLeaderboardEntry
                              ? formatNumberDisplay(currentMemberLeaderboardEntry.balance)
                              : "-"}
                          </strong>
                        </div>
                        <div className="poolSummaryStatBox">
                          <small>Stake range</small>
                          <strong>
                            {formatNumberDisplay(pool.minStake)}-{formatNumberDisplay(pool.maxStake)}
                          </strong>
                        </div>
                        <div className="poolSummaryStatBox">
                          <small>Event cap</small>
                          <strong>{formatNumberDisplay(pool.maxTotalStakePerEvent)}</strong>
                        </div>
                      </div>
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
                                <small>Win rate</small>
                              </span>
                            </article>
                          ))
                        )}
                      </div>
                    </Panel>
                  </div>
                ) : null}
              </section>
              {canManagePool(pool) ? (
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
                  <label className="poolSettingsWideField">
                    Cover image URL
                    <input
                      placeholder="https://..."
                      type="url"
                      value={coverImageUrl}
                      onChange={(event) => setCoverImageUrl(event.target.value)}
                    />
                  </label>
                  <label className="poolSettingsWideField">
                    Announcement title
                    <input
                      maxLength={200}
                      required
                      type="text"
                      value={announcementTitle}
                      onChange={(event) => setAnnouncementTitle(event.target.value)}
                    />
                  </label>
                  <label>
                    Default stake
                    <input
                      min={1}
                      required
                      type="number"
                      value={defaultStake}
                      onChange={(event) => setDefaultStake(Number(event.target.value))}
                    />
                  </label>
                  <label>
                    Min stake
                    <input
                      min={1}
                      required
                      type="number"
                      value={minStake}
                      onChange={(event) => setMinStake(Number(event.target.value))}
                    />
                  </label>
                  <label>
                    Max stake
                    <input
                      min={1}
                      required
                      type="number"
                      value={maxStake}
                      onChange={(event) => setMaxStake(Number(event.target.value))}
                    />
                  </label>
                  <label>
                    Event cap
                    <input
                      min={1}
                      required
                      type="number"
                      value={maxTotalStakePerEvent}
                      onChange={(event) => setMaxTotalStakePerEvent(Number(event.target.value))}
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
                  <button
                    className="button compactButton"
                    disabled={isSavingPool}
                    type="button"
                    onClick={() => void savePoolSettings()}
                  >
                    <IconLabel icon={Save}>{isSavingPool ? "Saving..." : "Save"}</IconLabel>
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
                              {formatDisplayDateTime(request.requestedAt)}
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
              <PoolAnnouncementsPanel
                announcementTitle={pool.announcementTitle || "Announcements"}
                drafts={announcementDrafts}
                isExpanded={isAnnouncementsExpanded}
                isOwner={canManageInvites(pool)}
                isPostingMessage={isPostingMessage}
                messages={poolMessages}
                onDraftChange={setAnnouncementDrafts}
                onSubmit={submitPoolAnnouncement}
                onToggle={() => setIsAnnouncementsExpanded((current) => !current)}
              />
              <div className="predictionGrid">
            <Panel className="marketsPanel" title="Markets">
              <select
                aria-label="Select match"
                className="mobileMarketMatchSelect"
                value={mobileSelectedMarketEventId}
                onChange={(event) =>
                  setMobileSelectedMarketEventId(event.target.value)
                }
              >
                {upcomingMarketGroups.map((group) => (
                  <option key={group.event.id} value={group.event.id}>
                    {formatMatchName(group.event)}
                  </option>
                ))}
              </select>
              <div className="marketList">
                {displayedUpcomingMarketGroups.map((group) => (
                  <section className="marketGroup" key={group.event.id}>
                    <div className="marketGroupHeader">
                      <strong>{formatMatchName(group.event)}</strong>
                      <small>
                        <IconLabel icon={CalendarClock}>
                          {formatDisplayDateTime(group.event.startsAt)}
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
                      onMarketPick={() => setIsPredictionSlipExpanded(true)}
                      poolPredictionsLocked={pool.predictionsLocked}
                      poolProfile={pool.profile}
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
                                  {formatDisplayDateTime(group.event.startsAt)}
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
                              onMarketPick={() =>
                                setIsPredictionSlipExpanded(true)
                              }
                              poolPredictionsLocked={pool.predictionsLocked}
                              poolProfile={pool.profile}
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
            <div className="predictionSideColumn">
              <Panel
                className={[
                  "predictionSubmitPanel",
                  pool.predictionsLocked ? "predictionSubmitPanelLocked" : "",
                  isPredictionSlipExpanded ? "predictionSubmitPanelExpanded" : "",
                ]
                  .filter(Boolean)
                  .join(" ")}
              >
                <button
                  className="predictionSlipToggle"
                  type="button"
                  onClick={() =>
                    setIsPredictionSlipExpanded((current) => !current)
                  }
                >
                  <span>
                    <strong>Chốt kèo đê</strong>
                    <small>
                      {selectedMarket
                        ? selectedOption || formatMarketTypeLabel(selectedMarket.type)
                        : "Chọn kèo"}
                    </small>
                  </span>
                  {isPredictionSlipExpanded ? (
                    <ChevronDown aria-hidden="true" size={18} />
                  ) : (
                    <ChevronRight aria-hidden="true" size={18} />
                  )}
                </button>
                <h2 className="predictionPanelTitle">Chốt kèo đê</h2>
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
                        inputMode="numeric"
                        pattern="[0-9]+-[0-9]+"
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
                            {formatMarketOptionText(option, selectedEvent)}
                          </option>
                        ))}
                      </select>
                    </label>
                  )
                ) : null}
                <label>
                  Điểm
                  <input
                    max={pool.maxStake}
                    min={pool.minStake}
                    required
                    type="number"
                    value={stake}
                    onChange={(event) => setStake(Number(event.target.value))}
                  />
                </label>
                {isStakeBelowMin ? (
                  <p className="stakeValidationText">
                    Stake must be at least {formatNumberDisplay(pool.minStake)}.
                  </p>
                ) : null}
                {isStakeAboveMax ? (
                  <p className="stakeValidationText">
                    Stake cannot exceed {formatNumberDisplay(pool.maxStake)}.
                  </p>
                ) : null}
                {exceedsEventStakeLimit ? (
                  <p className="stakeValidationText">
                    This match allows {formatNumberDisplay(pool.maxTotalStakePerEvent)} total stake.
                    {" "}Remaining allowance: {formatNumberDisplay(selectedEventStakeRemaining)}.
                  </p>
                ) : null}
                <button
                  className="button"
                  disabled={
                    pool.predictionsLocked ||
                    !selectedMarket ||
                    !selectedOption ||
                    stake <= 0 ||
                    isStakeBelowMin ||
                    isStakeAboveMax ||
                    exceedsEventStakeLimit ||
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
                {predictionFeedback ? (
                  <p className="predictionFeedback">{predictionFeedback}</p>
                ) : null}
                {recentPrediction ? (
                  <div className="recentPredictionCard">
                    <strong>Prediction submitted</strong>
                    <span>{recentPrediction.eventName}</span>
                    <small>
                      {recentPrediction.marketPeriod}{" "}
                      {formatMarketTypeLabel(recentPrediction.marketType)} |{" "}
                      {formatMarketOptionLabel(recentPrediction.selectedOption)}{" "}
                      | {formatNumberDisplay(recentPrediction.stake)} Điểm |{" "}
                      {recentPrediction.payoutMultiplier}x
                    </small>
                    <small>
                      {formatDisplayDateTime(recentPrediction.submittedAt)}
                    </small>
                  </div>
                ) : null}
                </form>
              </Panel>
              <PoolMessagesPanel
                chatBody={chatBody}
                chatEditorMode={chatEditorMode}
                isPostingMessage={isPostingMessage}
                messages={poolMessages}
                onChatBodyChange={setChatBody}
                onChatModeChange={setChatEditorMode}
                onSubmit={submitPoolMessage}
              />
            </div>
              </div>
            </div>
          </section>
        ) : null}
      </section>
    </UserShell>
  );
}

type PoolAnnouncementsPanelProps = {
  announcementTitle: string;
  drafts: Record<number, { title: string; bodyMarkdown: string }>;
  isExpanded: boolean;
  isOwner: boolean;
  isPostingMessage: boolean;
  messages: PoolMessage[];
  onDraftChange: Dispatch<
    SetStateAction<Record<number, { title: string; bodyMarkdown: string }>>
  >;
  onSubmit: (slot: number) => Promise<void>;
  onToggle: () => void;
};

function PoolAnnouncementsPanel({
  announcementTitle,
  drafts,
  isExpanded,
  isOwner,
  isPostingMessage,
  messages,
  onDraftChange,
  onSubmit,
  onToggle,
}: PoolAnnouncementsPanelProps) {
  const announcements = getAnnouncementSlots(messages);
  return (
    <Panel className="poolAnnouncementsPanel">
      <button
        className="announcementRowToggle"
        type="button"
        onClick={onToggle}
      >
        <IconLabel icon={isExpanded ? ChevronDown : ChevronRight}>
          {announcementTitle}
        </IconLabel>
      </button>
      {isExpanded ? (
        <div className="announcementSlotGrid">
        {[1, 2].map((slot) => {
          const draft = drafts[slot] ?? { title: "", bodyMarkdown: "" };
          const announcement = announcements[slot];
          return isOwner ? (
            <form
              className="announcementSlotEditor"
              key={slot}
              onSubmit={(event) => {
                event.preventDefault();
                onSubmit(slot);
              }}
            >
              <input
                maxLength={200}
                placeholder="Title"
                value={draft.title}
                onChange={(event) =>
                  onDraftChange((current) => ({
                    ...current,
                    [slot]: {
                      ...(current[slot] ?? { title: "", bodyMarkdown: "" }),
                      title: event.target.value,
                    },
                  }))
                }
              />
              <textarea
                maxLength={4000}
                placeholder="Content"
                rows={4}
                value={draft.bodyMarkdown}
                onChange={(event) =>
                  onDraftChange((current) => ({
                    ...current,
                    [slot]: {
                      ...(current[slot] ?? { title: "", bodyMarkdown: "" }),
                      bodyMarkdown: event.target.value,
                    },
                  }))
                }
              />
              <button
                className="button compactButton"
                disabled={isPostingMessage}
                type="submit"
              >
                <IconLabel icon={Save}>Save</IconLabel>
              </button>
            </form>
          ) : (
            <article className="announcementSlotView" key={slot}>
              {announcement?.title ? (
                <strong>{announcement.title}</strong>
              ) : (
                <strong>No announcement</strong>
              )}
              {announcement?.bodyMarkdown ? (
                <MarkdownPreview body={announcement.bodyMarkdown} />
              ) : (
                <p className="mutedText">No content yet.</p>
              )}
            </article>
          );
        })}
        </div>
      ) : null}
    </Panel>
  );
}

type PoolMessagesPanelProps = {
  chatBody: string;
  chatEditorMode: "write" | "preview";
  isPostingMessage: boolean;
  messages: PoolMessage[];
  onChatBodyChange: Dispatch<SetStateAction<string>>;
  onChatModeChange: Dispatch<SetStateAction<"write" | "preview">>;
  onSubmit: (kind: PoolMessageKind) => Promise<void>;
};

function PoolMessagesPanel({
  chatBody,
  chatEditorMode,
  isPostingMessage,
  messages,
  onChatBodyChange,
  onChatModeChange,
  onSubmit,
}: PoolMessagesPanelProps) {
  const chatMessages = messages.filter((message) => message.kind === "Chat");
  const listRef = useRef<HTMLDivElement | null>(null);
  const isPinnedToBottomRef = useRef(true);
  const hasInitializedScrollRef = useRef(false);
  const lastChatMessageId = chatMessages.at(-1)?.id ?? "";

  useEffect(() => {
    const list = listRef.current;
    if (!list) {
      return;
    }

    if (!hasInitializedScrollRef.current || isPinnedToBottomRef.current) {
      requestAnimationFrame(() => {
        list.scrollTop = list.scrollHeight;
        hasInitializedScrollRef.current = true;
        isPinnedToBottomRef.current = true;
      });
    }
  }, [chatMessages.length, lastChatMessageId]);

  function updatePinnedToBottom() {
    const list = listRef.current;
    if (!list) {
      return;
    }

    isPinnedToBottomRef.current =
      list.scrollHeight - list.scrollTop - list.clientHeight < 12;
  }

  return (
    <Panel className="poolMessagesPanel" title="Hội bà 8">
      <div
        className="poolMessageList"
        ref={listRef}
        onScroll={updatePinnedToBottom}
      >
        {chatMessages.length === 0 ? (
          <p className="mutedText">No pool messages yet.</p>
        ) : (
          chatMessages.map((message) => (
            <PoolMessageCard key={message.id} message={message} />
          ))
        )}
      </div>
      <MarkdownComposer
        body={chatBody}
        icon={MessageSquare}
        mode={chatEditorMode}
        placeholder="Chat with the pool..."
        rows={3}
        showEmojiPicker
        submitLabel="Send"
        title="Chat"
        onBodyChange={onChatBodyChange}
        onModeChange={onChatModeChange}
        onSubmit={() => onSubmit("Chat")}
        disabled={isPostingMessage}
      />
    </Panel>
  );
}

function PoolMessageCard({ message }: { message: PoolMessage }) {
  return (
    <article
      className={[
        "poolMessage",
        message.kind === "Announcement" ? "announcement" : "",
      ]
        .filter(Boolean)
        .join(" ")}
    >
      <div className="poolMessageHeader">
        <AuthorAvatar message={message} />
        <strong>
          {message.authorDisplayName}
        </strong>
        <small>{formatDisplayDateTime(message.createdAt)}</small>
      </div>
      <MarkdownPreview body={message.bodyMarkdown} />
    </article>
  );
}

function AuthorAvatar({ message }: { message: PoolMessage }) {
  return (
    <span className="poolMessageAvatarWrap">
      {message.authorAvatarUrl ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          alt=""
          className="poolMessageAvatar"
          src={message.authorAvatarUrl}
        />
      ) : (
        <span className="poolMessageAvatarFallback">
          {message.authorDisplayName.slice(0, 1).toUpperCase()}
        </span>
      )}
    </span>
  );
}

type MarkdownComposerProps = {
  body: string;
  disabled: boolean;
  icon: typeof MessageSquare;
  mode: "write" | "preview";
  placeholder: string;
  rows?: number;
  showEmojiPicker?: boolean;
  submitLabel: string;
  title: string;
  onBodyChange: Dispatch<SetStateAction<string>>;
  onModeChange: Dispatch<SetStateAction<"write" | "preview">>;
  onSubmit: () => void;
};

function MarkdownComposer({
  body,
  disabled,
  icon,
  mode,
  placeholder,
  rows = 4,
  showEmojiPicker = false,
  submitLabel,
  title,
  onBodyChange,
  onModeChange,
  onSubmit,
}: MarkdownComposerProps) {
  const [isEmojiPickerOpen, setIsEmojiPickerOpen] = useState(false);

  function insertEmoji(emoji: string) {
    onModeChange("write");
    onBodyChange((current) => (current ? `${current}${emoji}` : emoji));
    setIsEmojiPickerOpen(false);
  }

  return (
    <form
      className="markdownComposer"
      onSubmit={(event) => {
        event.preventDefault();
        onSubmit();
      }}
    >
      <div className="markdownComposerHeader">
        <strong>
          <IconLabel icon={icon}>{title}</IconLabel>
        </strong>
        <div className="segmentedControl">
          <button
            className={mode === "write" ? "active" : ""}
            type="button"
            onClick={() => onModeChange("write")}
          >
            <IconLabel icon={Edit3}>Write</IconLabel>
          </button>
          <button
            className={mode === "preview" ? "active" : ""}
            type="button"
            onClick={() => onModeChange("preview")}
          >
            <IconLabel icon={Eye}>Preview</IconLabel>
          </button>
        </div>
      </div>
      {mode === "preview" ? (
        <div className="markdownPreviewBox">
          {body.trim() ? (
            <MarkdownPreview body={body} />
          ) : (
            <p className="mutedText">Nothing to preview.</p>
          )}
        </div>
      ) : (
        <textarea
          maxLength={4000}
          placeholder={placeholder}
          rows={rows}
          value={body}
          onChange={(event) => onBodyChange(event.target.value)}
        />
      )}
      {showEmojiPicker ? (
        <div className="emojiPicker">
          <button
            aria-expanded={isEmojiPickerOpen}
            aria-label="Select emoji"
            className="emojiPickerToggle"
            type="button"
            onClick={() => setIsEmojiPickerOpen((current) => !current)}
          >
            <Smile aria-hidden="true" size={16} />
          </button>
          {isEmojiPickerOpen ? (
            <div className="emojiPickerGrid" aria-label="Supported emoji">
              {supportedChatEmojis.map((emoji) => (
                <button
                  aria-label={`Add ${emoji}`}
                  key={emoji}
                  type="button"
                  onClick={() => insertEmoji(emoji)}
                >
                  {emoji}
                </button>
              ))}
            </div>
          ) : null}
        </div>
      ) : null}
      <button
        className="button compactButton"
        disabled={disabled || !body.trim()}
        type="submit"
      >
        <IconLabel icon={Send}>{submitLabel}</IconLabel>
      </button>
    </form>
  );
}

function MarkdownPreview({ body }: { body: string }) {
  const lines = body.replace(/\r\n/g, "\n").split("\n");
  const nodes: ReactNode[] = [];
  let listItems: ReactNode[] = [];

  const flushList = () => {
    if (listItems.length === 0) {
      return;
    }

    nodes.push(<ul key={`list-${nodes.length}`}>{listItems}</ul>);
    listItems = [];
  };

  lines.forEach((line, index) => {
    const trimmed = line.trim();
    if (!trimmed) {
      flushList();
      return;
    }

    if (trimmed.startsWith("- ") || trimmed.startsWith("* ")) {
      listItems.push(
        <li key={`item-${index}`}>{renderInlineMarkdown(trimmed.slice(2))}</li>,
      );
      return;
    }

    flushList();
    if (trimmed.startsWith("### ")) {
      nodes.push(<h4 key={index}>{renderInlineMarkdown(trimmed.slice(4))}</h4>);
      return;
    }

    if (trimmed.startsWith("## ")) {
      nodes.push(<h3 key={index}>{renderInlineMarkdown(trimmed.slice(3))}</h3>);
      return;
    }

    if (trimmed.startsWith("# ")) {
      nodes.push(<h3 key={index}>{renderInlineMarkdown(trimmed.slice(2))}</h3>);
      return;
    }

    nodes.push(<p key={index}>{renderInlineMarkdown(trimmed)}</p>);
  });

  flushList();
  return <div className="markdownBody">{nodes}</div>;
}

function renderInlineMarkdown(text: string): ReactNode[] {
  const nodes: ReactNode[] = [];
  const pattern = /(\[([^\]]+)\]\(([^)]+)\)|\*\*([^*]+)\*\*|\*([^*]+)\*)/g;
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  while ((match = pattern.exec(text)) !== null) {
    if (match.index > lastIndex) {
      nodes.push(text.slice(lastIndex, match.index));
    }

    if (match[2] && match[3]) {
      const href = normalizeMarkdownHref(match[3]);
      nodes.push(
        href ? (
          <a href={href} key={match.index} rel="noreferrer" target="_blank">
            {match[2]}
          </a>
        ) : (
          match[2]
        ),
      );
    } else if (match[4]) {
      nodes.push(<strong key={match.index}>{match[4]}</strong>);
    } else if (match[5]) {
      nodes.push(<em key={match.index}>{match[5]}</em>);
    }

    lastIndex = pattern.lastIndex;
  }

  if (lastIndex < text.length) {
    nodes.push(text.slice(lastIndex));
  }

  return nodes;
}

function normalizeMarkdownHref(value: string) {
  const trimmed = value.trim();
  return /^(https?:\/\/|mailto:)/i.test(trimmed) ? trimmed : "";
}

type MarketGroupButtonsProps = {
  event: TournamentEvent;
  marketPredictionSummaries: MarketPredictionSummary[];
  markets: Market[];
  onMarketPick: () => void;
  poolPredictionsLocked: boolean;
  poolProfile: string;
  selectedMarketId: string;
  selectedOption: string;
  setSelectedMarketId: Dispatch<SetStateAction<string>>;
  setSelectedOption: Dispatch<SetStateAction<string>>;
};

function MarketGroupButtons({
  event,
  marketPredictionSummaries,
  markets,
  onMarketPick,
  poolPredictionsLocked,
  poolProfile,
  selectedMarketId,
  selectedOption,
  setSelectedMarketId,
  setSelectedOption,
}: MarketGroupButtonsProps) {
  const shouldShowPeriodLabel = poolProfile === "Standard";
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
              <div className="oneXTwoMarketTitle">
                {formatOneXTwoMarketTitle(market, shouldShowPeriodLabel)}
              </div>
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
                    onMarketPick();
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
      {markets
        .filter((market) => market.type === "Handicap")
        .map((market) => {
          const availability = getMarketAvailability(
            market,
            event,
            poolPredictionsLocked,
          );
          const isPendingHandicapLine = isHandicapLinePending(market);
          return (
            <div
              className={[
                "handicapMarketRow",
                isPendingHandicapLine ? "linePending" : "",
                poolPredictionsLocked ? "poolLocked" : "",
              ]
                .filter(Boolean)
                .join(" ")}
              key={market.id}
            >
              {shouldShowPeriodLabel ? (
                <strong className="handicapMarketPeriod">
                  {formatMarketPeriodLongLabel(market.period)}
                </strong>
              ) : null}
              {getMarketOptions(market, event).map((option) => (
                <button
                  className={[
                    "handicapOption",
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
                    onMarketPick();
                  }}
                >
                  <strong>{formatHandicapOptionLabel(option, event)}</strong>
                  <span>{formatHandicapOptionScore(option, event, market)}</span>
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
          .filter((market) => market.type !== "OneXTwo" && market.type !== "Handicap")
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
                  onMarketPick();
                }}
              >
                <strong>
                  {formatMarketCardTitle(market, shouldShowPeriodLabel)}
                </strong>
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

const defaultScheduledMarketWindowHours = 48;
const recentClosedMarketWindowMs = 24 * 60 * 60 * 1000;

function shouldShowUpcomingMarketGroup(
  matchEvent: TournamentEvent,
  scheduledMarketWindowHours: number,
) {
  const now = Date.now();
  const startsAt = new Date(matchEvent.startsAt).getTime();
  const scheduledMarketWindowMs =
    scheduledMarketWindowHours * 60 * 60 * 1000;
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

function isCorrectScoreOption(option: string) {
  return /^[0-9]+-[0-9]+$/.test(option);
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

function formatMarketPeriodLongLabel(period: string) {
  return period === "FullTime"
    ? "Full-Time"
    : period === "FirstHalf"
      ? "Half-Time"
      : period;
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

function formatMarketCardTitle(market: Market, includePeriodLabel = false) {
  const payoutRate = formatPayoutRate(market.payoutMultiplier);
  const marketName = includePeriodLabel
    ? `${formatMarketTypeLabel(market.type)} ${formatMarketPeriodLabel(market.period)}`
    : formatMarketTypeLabel(market.type);
  if (market.type === "OverUnder") {
    return `${marketName} (${market.lineValue ?? "-"} @${payoutRate})`;
  }

  if (market.type === "OddEven" || market.type === "CorrectScore") {
    return `${marketName} (${payoutRate})`;
  }

  return `${marketName} (${formatMarketPeriodLabel(market.period)} @${payoutRate})`;
}

function formatOneXTwoMarketTitle(market: Market, includePeriodLabel = false) {
  const marketName = includePeriodLabel
    ? `1 X 2 ${formatMarketPeriodLabel(market.period)}`
    : "1 X 2";
  return `${marketName} (${formatPayoutRate(market.payoutMultiplier)})`;
}

function formatPayoutRate(payoutMultiplier: number) {
  const profit = Math.max(0, payoutMultiplier - 1);
  return `1:${formatCompactNumber(profit)}`;
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
    return (
      <ParticipantName
        code={matchEvent.homeParticipantCode}
        name={option}
      />
    );
  }

  if (option === matchEvent.awayParticipant) {
    return (
      <ParticipantName
        code={matchEvent.awayParticipantCode}
        name={option}
      />
    );
  }

  return formatMarketOptionLabel(option);
}

function formatHandicapOptionLabel(option: string, matchEvent: TournamentEvent) {
  if (option.startsWith(`${matchEvent.homeParticipant} `)) {
    return (
      <ParticipantName
        code={matchEvent.homeParticipantCode}
        name={matchEvent.homeParticipant}
      />
    );
  }

  if (option.startsWith(`${matchEvent.awayParticipant} `)) {
    return (
      <ParticipantName
        code={matchEvent.awayParticipantCode}
        name={matchEvent.awayParticipant}
      />
    );
  }

  return option;
}

function formatHandicapOptionLine(option: string, matchEvent: TournamentEvent) {
  if (option.startsWith(`${matchEvent.homeParticipant} `)) {
    return option.slice(matchEvent.homeParticipant.length).trim();
  }

  if (option.startsWith(`${matchEvent.awayParticipant} `)) {
    return option.slice(matchEvent.awayParticipant.length).trim();
  }

  return "";
}

function formatHandicapOptionScore(
  option: string,
  matchEvent: TournamentEvent,
  market: Market,
) {
  const line = formatHandicapOptionLine(option, matchEvent);
  const isFirstHalf = market.period === "FirstHalf";
  let score = 0;

  if (option.startsWith(`${matchEvent.homeParticipant} `)) {
    score = isFirstHalf
      ? matchEvent.firstHalfHomeScore ?? 0
      : matchEvent.fullTimeHomeScore ?? 0;
  }

  if (option.startsWith(`${matchEvent.awayParticipant} `)) {
    score = isFirstHalf
      ? matchEvent.firstHalfAwayScore ?? 0
      : matchEvent.fullTimeAwayScore ?? 0;
  }

  return line ? `${score} (${line})` : `${score}`;
}

function formatMarketOptionText(
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

  return formatMarketOptionLabel(option);
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

function getAnnouncementSlots(messages: PoolMessage[]) {
  return messages
    .filter((message) => message.kind === "Announcement")
    .reduce<Record<number, PoolMessage | undefined>>((slots, message) => {
      if (message.announcementSlot === 1 || message.announcementSlot === 2) {
        slots[message.announcementSlot] = message;
      }

      return slots;
    }, {});
}

function getAnnouncementDraft(messages: PoolMessage[], slot: number) {
  const announcement = messages.find(
    (message) =>
      message.kind === "Announcement" && message.announcementSlot === slot,
  );
  return {
    title: announcement?.title ?? "",
    bodyMarkdown: announcement?.bodyMarkdown ?? "",
  };
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

function getEventStakeUsed(
  predictions: Prediction[],
  markets: Market[],
  eventId: string,
) {
  const eventMarketIds = new Set(
    markets
      .filter((market) => market.eventId === eventId)
      .map((market) => market.id),
  );

  return predictions
    .filter((prediction) => eventMarketIds.has(prediction.marketId))
    .reduce((sum, prediction) => sum + prediction.stake, 0);
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

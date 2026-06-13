"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, ArrowUpDown, BadgeDollarSign, Hash, Percent, Target, Trophy, UserRound } from "lucide-react";
import { UserShell } from "../../../../components/user-shell";
import { IconLabel, PageHeader, Panel, StatusPill } from "../../../../components/ui";
import { apiUrl, readApiError } from "../../../../lib/api";
import { getStoredToken } from "../../../../lib/auth";
import { formatDisplayDateTime } from "../../../../lib/datetime";
import { PoolMemberPredictionProfile, PredictionProfileBreakdown } from "../../../../lib/types";

export default function PoolMemberProfilePage() {
  const router = useRouter();
  const params = useParams<{ poolId: string; memberId: string }>();
  const [profile, setProfile] = useState<PoolMemberPredictionProfile | null>(null);
  const [status, setStatus] = useState("Loading member profile...");
  const [predictionPageSize, setPredictionPageSize] = useState(20);
  const [predictionPage, setPredictionPage] = useState(1);
  const [settlementFilter, setSettlementFilter] = useState<
    "All" | "Settled" | "Unsettled"
  >("All");
  const [fromDate, setFromDate] = useState(() => getRelativeDateInputValue(-3));
  const [toDate, setToDate] = useState(() => getRelativeDateInputValue(3));
  const [eventSortOrder, setEventSortOrder] = useState<"desc" | "asc">("desc");

  useEffect(() => {
    const token = getStoredToken();
    if (!token) {
      router.replace("/login");
      return;
    }

    fetch(apiUrl(`/api/predictions/pool/${params.poolId}/members/${params.memberId}/profile`), {
      headers: { Authorization: `Bearer ${token}` },
    })
      .then(async (response) => {
        if (!response.ok) {
          setStatus(await readApiError(response, "Could not load member profile."));
          return;
        }

        setProfile((await response.json()) as PoolMemberPredictionProfile);
        setStatus("Profile loaded.");
      })
      .catch((error) => {
        setStatus(error instanceof Error ? error.message : "Could not load member profile.");
      });
  }, [params.memberId, params.poolId, router]);

  useEffect(() => {
    setPredictionPage(1);
  }, [params.memberId, predictionPageSize, settlementFilter, fromDate, toDate, eventSortOrder]);

  const filteredPredictions = useMemo(() => {
    const source = profile?.predictions ?? [];
    return source
      .filter((prediction) => {
        const outcome = prediction.outcome ?? "Unsettled";
        if (settlementFilter === "Settled" && outcome === "Unsettled") {
          return false;
        }

        if (settlementFilter === "Unsettled" && outcome !== "Unsettled") {
          return false;
        }

        const eventDate = prediction.eventStartsAt
          ? toDateInputValue(new Date(prediction.eventStartsAt))
          : "";
        if (fromDate && eventDate && eventDate < fromDate) {
          return false;
        }

        if (toDate && eventDate && eventDate > toDate) {
          return false;
        }

        return true;
      })
      .sort((left, right) => {
        const leftTime = left.eventStartsAt
          ? new Date(left.eventStartsAt).getTime()
          : new Date(left.submittedAt).getTime();
        const rightTime = right.eventStartsAt
          ? new Date(right.eventStartsAt).getTime()
          : new Date(right.submittedAt).getTime();
        return eventSortOrder === "asc" ? leftTime - rightTime : rightTime - leftTime;
      });
  }, [eventSortOrder, fromDate, profile?.predictions, settlementFilter, toDate]);

  const totalPredictionPages = Math.max(
    1,
    Math.ceil(filteredPredictions.length / predictionPageSize),
  );
  const currentPredictionPage = Math.min(predictionPage, totalPredictionPages);
  const pagedPredictions = useMemo(
    () =>
      filteredPredictions.slice(
        (currentPredictionPage - 1) * predictionPageSize,
        currentPredictionPage * predictionPageSize,
      ),
    [currentPredictionPage, filteredPredictions, predictionPageSize],
  );

  return (
    <UserShell>
      <section className="pageStack">
        <PageHeader
          eyebrow="Pool profile"
          title={profile?.displayName ?? "Member profile"}
          icon={UserRound}
          actions={
            <Link className="button buttonSecondary compactButton" href={`/pools/${params.poolId}`}>
              <IconLabel icon={ArrowLeft}>Back to pool</IconLabel>
            </Link>
          }
        />
        <StatusPill icon={UserRound}>{status}</StatusPill>

        {profile ? (
          <>
            <Panel className="memberProfileHeaderPanel" title="Member">
              <div className="memberProfileHeader">
                <span className="memberProfileAvatar">
                  {profile.avatarUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img alt="" src={profile.avatarUrl} />
                  ) : (
                    <span>{profile.displayName.slice(0, 1).toUpperCase()}</span>
                  )}
                </span>
                <div className="memberProfileIdentity">
                  <strong>{profile.displayName}</strong>
                  <small>
                    {profile.role}
                    {profile.leaderboardStatus === "Excluded" ? " · excluded from leaderboard" : ""}
                  </small>
                </div>
              </div>
            </Panel>

            <div className="memberProfileKpiGrid">
              <MetricTile icon={Trophy} label="Rank" value={profile.rank ? `#${profile.rank}` : "-"} />
              <MetricTile icon={BadgeDollarSign} label="Balance" value={formatNumberDisplay(profile.balance)} />
              <MetricTile icon={Hash} label="Win/Loss" value={formatNumberDisplay(profile.winLoss)} />
              <MetricTile icon={Percent} label="Win rate" value={`${formatNumberDisplay(profile.winRate)}%`} />
              <MetricTile icon={Target} label="ROI" value={`${formatNumberDisplay(profile.roi)}%`} />
              <MetricTile icon={UserRound} label="Predictions" value={`${profile.predictionCount} / ${profile.settledPredictionCount} settled`} />
            </div>

            <div className="memberProfileGrid">
              <Panel title="By market">
                <BreakdownTable rows={profile.marketBreakdown} emptyText="No market stats yet." />
              </Panel>
              <Panel title="By outcome">
                <BreakdownTable rows={profile.outcomeBreakdown} emptyText="No outcome stats yet." />
              </Panel>
            </div>

            <Panel title="Recent predictions">
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
              <div className="memberPredictionToolbar">
                <div className="listToolbarControls">
                  <label>
                    Page size
                    <select
                      value={predictionPageSize}
                      onChange={(event) => setPredictionPageSize(Number(event.target.value))}
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
                      setEventSortOrder((current) => (current === "desc" ? "asc" : "desc"))
                    }
                  >
                    <IconLabel icon={ArrowUpDown}>
                      Kickoff {eventSortOrder === "desc" ? "↓" : "↑"}
                    </IconLabel>
                  </button>
                </div>
                <span>
                  Page {currentPredictionPage} of {totalPredictionPages}
                </span>
                <div className="memberPredictionPager">
                  <button
                    className="button buttonSecondary compactButton"
                    disabled={currentPredictionPage <= 1}
                    type="button"
                    onClick={() => setPredictionPage((current) => Math.max(1, current - 1))}
                  >
                    Previous
                  </button>
                  <button
                    className="button buttonSecondary compactButton"
                    disabled={currentPredictionPage >= totalPredictionPages}
                    type="button"
                    onClick={() => setPredictionPage((current) => Math.min(totalPredictionPages, current + 1))}
                  >
                    Next
                  </button>
                </div>
              </div>
              <div className="memberPredictionList">
                {filteredPredictions.length === 0 ? (
                  <p className="mutedText">No predictions match the current filters.</p>
                ) : null}
                {pagedPredictions.map((prediction) => (
                  <article className="memberPredictionRow" key={prediction.id}>
                    <span>
                      <strong>{prediction.eventName ?? "Event"}</strong>
                      <small>{formatDisplayDateTime(prediction.submittedAt)}</small>
                    </span>
                    <span>
                      <strong>{formatMarketLabel(prediction.marketType, prediction.marketPeriod)}</strong>
                      <small>{prediction.selectedOption}</small>
                    </span>
                    <span>
                      <strong>{formatNumberDisplay(prediction.stake)}</strong>
                      <small>stake</small>
                    </span>
                    <span>
                      <strong>{prediction.outcome ?? "Unsettled"}</strong>
                      <small>{formatSignedNumber(prediction.netPoints ?? 0)} pts</small>
                    </span>
                  </article>
                ))}
              </div>
            </Panel>
          </>
        ) : null}
      </section>
    </UserShell>
  );
}

function MetricTile({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof Trophy;
  label: string;
  value: string;
}) {
  return (
    <div className="memberProfileMetric">
      <Icon aria-hidden="true" size={18} />
      <span>
        <small>{label}</small>
        <strong>{value}</strong>
      </span>
    </div>
  );
}

function BreakdownTable({
  rows,
  emptyText,
}: {
  rows: PredictionProfileBreakdown[];
  emptyText: string;
}) {
  return (
    <div className="memberBreakdownTable">
      {rows.length === 0 ? <p className="mutedText">{emptyText}</p> : null}
      {rows.map((row) => (
        <div className="memberBreakdownRow" key={row.label}>
          <strong>{formatMarketType(row.label)}</strong>
          <span>{row.predictionCount}</span>
          <span>{row.settledPredictionCount} settled</span>
          <span>{row.winCount} wins</span>
          <span>{formatSignedNumber(row.netPoints)}</span>
        </div>
      ))}
    </div>
  );
}

function formatMarketLabel(type: string, period: string) {
  return `${formatMarketType(type)} ${period === "FullTime" ? "FT" : period === "FirstHalf" ? "HT" : period}`;
}

function formatMarketType(value: string) {
  const labels: Record<string, string> = {
    OneXTwo: "1X2",
    OverUnder: "Over/Under",
    OddEven: "Odd/Even",
    CorrectScore: "Correct score",
    Handicap: "Handicap",
  };

  return labels[value] ?? value;
}

function formatSignedNumber(value: number) {
  return value > 0 ? `+${formatNumberDisplay(value)}` : formatNumberDisplay(value);
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

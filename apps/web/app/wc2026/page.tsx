"use client";

import { useEffect, useMemo, useState } from "react";
import { CalendarDays, CheckCircle2, Clock, Database, Trophy } from "lucide-react";
import { PageHeader, Panel, StatGrid, StatusPill } from "../components/ui";
import { UserShell } from "../components/user-shell";
import { apiUrl } from "../lib/api";
import { formatDisplayDate, formatDisplayDateTime } from "../lib/datetime";
import { ParticipantName } from "../lib/participant-flags";
import { Tournament, TournamentEvent } from "../lib/types";

const footballDataProvider = "FootballData";

export default function Wc2026Page() {
  const [tournament, setTournament] = useState<Tournament | null>(null);
  const [events, setEvents] = useState<TournamentEvent[]>([]);
  const [status, setStatus] = useState("Loading WC2026 calendar...");

  useEffect(() => {
    let ignore = false;

    async function loadCalendar() {
      try {
        const tournamentsResponse = await fetch(apiUrl("/api/tournaments"));
        if (!tournamentsResponse.ok) {
          setStatus("Could not load tournaments from database.");
          return;
        }

        const tournaments = (await tournamentsResponse.json()) as Tournament[];
        const selectedTournament =
          tournaments.find(
            (item) =>
              item.provider === footballDataProvider &&
              !item.isTestData &&
              item.name.toLowerCase().includes("world cup") &&
              item.name.includes("2026"),
          ) ?? null;

        if (ignore) {
          return;
        }

        setTournament(selectedTournament);

        if (!selectedTournament) {
          setStatus("World Cup 2026 has not been synced into the database yet.");
          return;
        }

        const eventsResponse = await fetch(apiUrl(`/api/tournaments/${selectedTournament.id}/events`));
        if (!eventsResponse.ok) {
          setStatus("Could not load WC2026 matches from database.");
          return;
        }

        const result = (await eventsResponse.json()) as TournamentEvent[];
        if (ignore) {
          return;
        }

        setEvents(result);
        setStatus(
          result.length === 0
            ? "No WC2026 matches are stored yet."
            : `${result.length} WC2026 matches from ${selectedTournament.provider}.`,
        );
      } catch (error) {
        if (!ignore) {
          setStatus(error instanceof Error ? error.message : "Could not load WC2026 calendar.");
        }
      }
    }

    void loadCalendar();

    return () => {
      ignore = true;
    };
  }, []);

  const visibleEvents = useMemo(() => filterCalendarEvents(events), [events]);
  const matchesByDate = useMemo(() => groupEventsByDate(visibleEvents), [visibleEvents]);
  const todayLinkDateKey = useMemo(() => getNearestDateKey(matchesByDate), [matchesByDate]);
  const completedCount = visibleEvents.filter((event) => hasResult(event)).length;
  const upcomingCount = visibleEvents.length - completedCount;

  return (
    <UserShell>
      <section className="pageStack">
        <PageHeader
          eyebrow="World Cup 2026"
          title="Lịch thi đấu và kết quả"
          icon={Trophy}
          actions={
            <>
              {todayLinkDateKey ? (
                <a className="button buttonSecondary compactButton" href={`#${matchDayAnchorId(todayLinkDateKey)}`}>
                  Hôm nay
                </a>
              ) : null}
              <StatusPill icon={Database}>{status}</StatusPill>
            </>
          }
        />

        <StatGrid
          items={[
            { label: "Matches", value: visibleEvents.length, icon: CalendarDays },
            { label: "Results", value: completedCount, icon: CheckCircle2 },
            { label: "Upcoming", value: upcomingCount, icon: Clock },
          ]}
        />

        {tournament ? (
          <Panel className="wcCalendarMeta">
            <div>
              <p className="eyebrow">{tournament.sport}</p>
              <h2>{tournament.name}</h2>
            </div>
            <dl className="detailList compactDetails">
              <div>
                <dt>Dates</dt>
                <dd>
                  {formatDisplayDate(tournament.startsOn)} - {formatDisplayDate(tournament.endsOn)}
                </dd>
              </div>
              <div>
                <dt>Source</dt>
                <dd>
                  {tournament.provider}
                  {tournament.isTestData ? " test data" : ""}
                </dd>
              </div>
            </dl>
          </Panel>
        ) : null}

        {matchesByDate.length === 0 ? (
          <Panel>
            <div className="emptyState">
              <h2>No calendar available</h2>
              <p>{status}</p>
            </div>
          </Panel>
        ) : (
          <div className="wcCalendarList">
            {matchesByDate.map((group) => (
              <section className="wcMatchDay" id={matchDayAnchorId(group.dateKey)} key={group.dateKey}>
                <div className="wcMatchDayHeader">
                  <p className="eyebrow">{group.matches.length} matches</p>
                  <h2>{formatMatchDay(group.dateKey)}</h2>
                </div>
                <div className="wcMatchRows">
                  {group.matches.map((matchEvent) => {
                    const halfTimeScore = halfTimeScoreText(matchEvent);
                    const displayLive = isDisplayLive(matchEvent);

                    return (
                      <article className="wcMatchRow" key={matchEvent.id}>
                        <time dateTime={matchEvent.startsAt}>{formatDisplayDateTime(matchEvent.startsAt)}</time>
                        <div className="wcTeams">
                          <div className="wcTeamName wcTeamNameHome">
                            <ParticipantName code={matchEvent.homeParticipantCode} name={matchEvent.homeParticipant} />
                          </div>
                          <div className="wcScore">
                            <strong>{scoreText(matchEvent)}</strong>
                            {halfTimeScore ? <small>{halfTimeScore}</small> : null}
                          </div>
                          <div className="wcTeamName">
                            <ParticipantName code={matchEvent.awayParticipantCode} name={matchEvent.awayParticipant} />
                          </div>
                        </div>
                        <span className={displayLive ? "wcStatus wcStatusLive" : "wcStatus"}>
                          {displayLive ? <span className="wcLiveDot" aria-hidden="true" /> : null}
                          {resultStatus(matchEvent)}
                        </span>
                      </article>
                    );
                  })}
                </div>
              </section>
            ))}
          </div>
        )}
      </section>
    </UserShell>
  );
}

function groupEventsByDate(events: TournamentEvent[]) {
  const groups = new Map<string, TournamentEvent[]>();

  for (const event of events) {
    const dateKey = event.startsAt.slice(0, 10);
    groups.set(dateKey, [...(groups.get(dateKey) ?? []), event]);
  }

  return [...groups.entries()].map(([dateKey, matches]) => ({
    dateKey,
    matches,
  }));
}

function filterCalendarEvents(events: TournamentEvent[]) {
  const yesterday = new Date();
  yesterday.setDate(yesterday.getDate() - 1);
  const oldestVisibleDateKey = localDateKey(yesterday);

  return events.filter((event) => event.startsAt.slice(0, 10) >= oldestVisibleDateKey);
}

function getNearestDateKey(groups: Array<{ dateKey: string; matches: TournamentEvent[] }>) {
  if (groups.length === 0) {
    return "";
  }

  const today = localDateKey(new Date());
  const exactMatch = groups.find((group) => group.dateKey === today);
  if (exactMatch) {
    return exactMatch.dateKey;
  }

  const todayTime = dateKeyTime(today);
  return groups.reduce((nearest, group) => {
    const nearestDelta = Math.abs(dateKeyTime(nearest.dateKey) - todayTime);
    const groupDelta = Math.abs(dateKeyTime(group.dateKey) - todayTime);

    if (groupDelta < nearestDelta) {
      return group;
    }

    if (groupDelta === nearestDelta && group.dateKey > today && nearest.dateKey < today) {
      return group;
    }

    return nearest;
  }).dateKey;
}

function matchDayAnchorId(dateKey: string) {
  return `matchday-${dateKey}`;
}

function localDateKey(date: Date) {
  const year = date.getFullYear();
  const month = (date.getMonth() + 1).toString().padStart(2, "0");
  const day = date.getDate().toString().padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function dateKeyTime(dateKey: string) {
  return new Date(`${dateKey}T00:00:00`).getTime();
}

function hasResult(matchEvent: TournamentEvent) {
  return matchEvent.fullTimeHomeScore !== null && matchEvent.fullTimeAwayScore !== null;
}

function scoreText(matchEvent: TournamentEvent) {
  return hasResult(matchEvent)
    ? `${matchEvent.fullTimeHomeScore} - ${matchEvent.fullTimeAwayScore}`
    : "vs";
}

function halfTimeScoreText(matchEvent: TournamentEvent) {
  return hasFirstHalfResult(matchEvent)
    ? `HT ${matchEvent.firstHalfHomeScore} - ${matchEvent.firstHalfAwayScore}`
    : "";
}

function resultStatus(matchEvent: TournamentEvent) {
  if (hasResult(matchEvent)) {
    return matchEvent.status === "Finished" || matchEvent.status === "Settled" ? "Final" : "Result";
  }

  if (isDisplayLive(matchEvent)) {
    return "Live";
  }

  return matchEvent.status;
}

function hasFirstHalfResult(matchEvent: TournamentEvent) {
  return matchEvent.firstHalfHomeScore !== null && matchEvent.firstHalfAwayScore !== null;
}

function isDisplayLive(matchEvent: TournamentEvent) {
  if (matchEvent.status !== "Scheduled") {
    return matchEvent.status === "Live";
  }

  const startsAt = new Date(matchEvent.startsAt).getTime();
  return Number.isFinite(startsAt) && Date.now() >= startsAt;
}

function formatMatchDay(dateKey: string) {
  return new Intl.DateTimeFormat("vi-VN", {
    weekday: "long",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(new Date(`${dateKey}T00:00:00`));
}

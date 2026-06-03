"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import {
  CalendarDays,
  LogIn,
  Plus,
  Trophy,
} from "lucide-react";
import { IconLabel, PageHeader, StatusPill } from "./components/ui";
import { apiUrl } from "./lib/api";
import { getStoredToken } from "./lib/auth";
import { UserShell } from "./components/user-shell";
import { appName } from "./lib/config";
import { Tournament } from "./lib/types";

export default function Home() {
  const [tournaments, setTournaments] = useState<Tournament[]>([]);
  const [status, setStatus] = useState("Loading tournaments...");
  const [isSignedIn, setIsSignedIn] = useState(false);

  useEffect(() => {
    setIsSignedIn(Boolean(getStoredToken()));

    fetch(apiUrl("/api/tournaments"))
      .then(async (response) => {
        if (!response.ok) {
          setStatus("Could not load tournaments.");
          return;
        }

        const result = (await response.json()) as Tournament[];
        setTournaments(result);
        setStatus(
          result.length === 0
            ? "No running or upcoming tournaments yet."
            : `${result.length} tournament available.`,
        );
      })
      .catch((error) =>
        setStatus(
          error instanceof Error
            ? error.message
            : "Could not load tournaments.",
        ),
      );
  }, []);

  return (
    <UserShell>
      <section className="publicHero">
        <p className="eyebrow">Prediction pools</p>
        <h1>{appName}</h1>
        <p className="mutedText">
          Browse running and upcoming tournaments, then create a prediction pool
          when you are ready to compete with friends.
        </p>
      </section>

      <section className="pageStack">
        <PageHeader
          eyebrow="Available"
          title="Tournaments"
          icon={Trophy}
          actions={<StatusPill icon={CalendarDays}>{status}</StatusPill>}
        />

        <div className="tournamentGrid">
          {tournaments.map((tournament) => (
            <article className="tournamentCard" key={tournament.id}>
              <div>
                <p className="eyebrow">{tournament.sport}</p>
                <h2>{tournament.name}</h2>
              </div>
              <dl className="detailList compactDetails">
                <div>
                  <dt>Starts</dt>
                  <dd>{formatDate(tournament.startsOn)}</dd>
                </div>
                <div>
                  <dt>Ends</dt>
                  <dd>{formatDate(tournament.endsOn)}</dd>
                </div>
                <div>
                  <dt>Source</dt>
                  <dd>
                    {tournament.provider}
                    {tournament.isTestData ? " test data" : ""}
                  </dd>
                </div>
              </dl>
              {isSignedIn ? (
                <Link className="button buttonSecondary" href={`/pools/new?tournamentId=${encodeURIComponent(tournament.id)}`}>
                  <IconLabel icon={Plus}>Create pool</IconLabel>
                </Link>
              ) : (
                <Link className="button buttonSecondary" href="/login">
                  <IconLabel icon={LogIn}>Login to create pool</IconLabel>
                </Link>
              )}
            </article>
          ))}
        </div>
      </section>
    </UserShell>
  );
}

function formatDate(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString();
}

"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { CalendarDays, Gauge, Plus, Trophy, UserPlus, Waves } from "lucide-react";
import { IconLabel, PageHeader, Panel, StatusPill } from "../components/ui";
import { apiUrl } from "../lib/api";
import { Tournament } from "../lib/types";

export default function DashboardPage() {
  const [tournaments, setTournaments] = useState<Tournament[]>([]);
  const [status, setStatus] = useState("Loading tournaments...");

  useEffect(() => {
    fetch(apiUrl("/api/tournaments"))
      .then(async (response) => {
        if (!response.ok) {
          setStatus("Could not load tournaments.");
          return;
        }

        const result = (await response.json()) as Tournament[];
        setTournaments(result);
        setStatus(result.length === 0 ? "No running or upcoming tournaments yet." : `${result.length} tournament available.`);
      })
      .catch((error) => setStatus(error instanceof Error ? error.message : "Could not load tournaments."));
  }, []);

  return (
    <section className="pageStack">
      <PageHeader eyebrow="Dashboard" title="Pool workspace" icon={Gauge} />
      <Panel title="Start here">
        <p className="mutedText">Use pools to create a World Cup prediction pool or join one by invite code.</p>
        <div className="buttonRow">
          <Link className="button" href="/app/pools"><IconLabel icon={Waves}>View pools</IconLabel></Link>
          <Link className="button buttonSecondary" href="/app/pools/new"><IconLabel icon={Plus}>Create pool</IconLabel></Link>
          <Link className="button buttonSecondary" href="/app/pools/join"><IconLabel icon={UserPlus}>Join pool</IconLabel></Link>
        </div>
      </Panel>
      <Panel>
        <PageHeader
          eyebrow="Available"
          title="Tournaments"
          icon={Trophy}
          actions={<StatusPill icon={CalendarDays}>{status}</StatusPill>}
        />
        <div className="tournamentGrid">
          {tournaments.map((tournament) => (
            <article className="tournamentCard compactTournamentCard" key={tournament.id}>
              <div>
                <p className="eyebrow">{tournament.sport}</p>
                <h2>{tournament.name}</h2>
              </div>
              <Link className="button buttonSecondary" href="/app/pools/new"><IconLabel icon={Plus}>Create pool</IconLabel></Link>
            </article>
          ))}
        </div>
      </Panel>
    </section>
  );
}

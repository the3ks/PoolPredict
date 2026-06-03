"use client";

import { FormEvent, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Plus, Waves } from "lucide-react";
import { UserShell } from "../../components/user-shell";
import { IconLabel, PageHeader } from "../../components/ui";
import { apiUrl, readApiError } from "../../lib/api";
import { getStoredToken } from "../../lib/auth";
import { MarketProfile, Tournament } from "../../lib/types";

export default function NewPoolPage() {
  const router = useRouter();
  const [tournaments, setTournaments] = useState<Tournament[]>([]);
  const [selectedTournamentId, setSelectedTournamentId] = useState("");
  const [name, setName] = useState("");
  const [profile, setProfile] = useState<MarketProfile>("Standard");
  const [startingBalance, setStartingBalance] = useState(1000);
  const [status, setStatus] = useState("Create a pool.");

  useEffect(() => {
    fetch(apiUrl("/api/tournaments"))
      .then(async (response) => {
        const result = response.ok ? ((await response.json()) as Tournament[]) : [];
        const requestedTournamentId = new URLSearchParams(window.location.search).get("tournamentId");
        setTournaments(result);
        setSelectedTournamentId(
          result.some((tournament) => tournament.id === requestedTournamentId)
            ? requestedTournamentId ?? ""
            : result[0]?.id ?? ""
        );
      })
      .catch((error) => setStatus(error instanceof Error ? error.message : "Could not load tournaments."));
  }, []);

  async function createPool(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const token = getStoredToken();
    const tournamentId = selectedTournamentId;
    if (!token || !tournamentId) {
      setStatus("Missing session or tournament.");
      return;
    }

    const response = await fetch(apiUrl("/api/pools"), {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ name, tournamentId, profile, startingBalance }),
    });

    if (!response.ok) {
      setStatus(await readApiError(response, "Pool creation failed."));
      return;
    }

    const pool = await response.json();
    router.push(`/pools/${pool.id}`);
  }

  return (
    <UserShell>
    <section className="pageStack">
      <PageHeader eyebrow="Pools" title="Create pool" icon={Plus} />
      <form className="form panel narrowPanel" onSubmit={createPool}>
        <label>
          Pool name
          <input required type="text" value={name} onChange={(event) => setName(event.target.value)} />
        </label>
        <label>
          Tournament
          <select required value={selectedTournamentId} onChange={(event) => setSelectedTournamentId(event.target.value)}>
            {tournaments.map((tournament) => (
              <option key={tournament.id} value={tournament.id}>{tournament.name} ({tournament.provider}{tournament.isTestData ? " test" : ""})</option>
            ))}
          </select>
        </label>
        <label>
          Profile
          <select value={profile} onChange={(event) => setProfile(event.target.value as MarketProfile)}>
            <option value="Casual">Casual</option>
            <option value="Standard">Standard</option>
            <option value="Expert">Expert</option>
          </select>
        </label>
        <label>
          Starting balance
          <input min={1} required type="number" value={startingBalance} onChange={(event) => setStartingBalance(Number(event.target.value))} />
        </label>
        <button className="button" type="submit"><IconLabel icon={Waves}>Create pool</IconLabel></button>
        <p className="statusText">{status}</p>
      </form>
    </section>
    </UserShell>
  );
}

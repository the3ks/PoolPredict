"use client";

import { FormEvent, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { apiUrl, readApiError } from "../../../lib/api";
import { getStoredToken } from "../../../lib/auth";
import { MarketProfile, Tournament } from "../../../lib/types";

export default function NewPoolPage() {
  const router = useRouter();
  const [tournaments, setTournaments] = useState<Tournament[]>([]);
  const [name, setName] = useState("");
  const [profile, setProfile] = useState<MarketProfile>("Standard");
  const [startingBalance, setStartingBalance] = useState(1000);
  const [status, setStatus] = useState("Create a pool.");

  useEffect(() => {
    fetch(apiUrl("/api/tournaments"))
      .then(async (response) => setTournaments(response.ok ? await response.json() : []))
      .catch((error) => setStatus(error instanceof Error ? error.message : "Could not load tournaments."));
  }, []);

  async function createPool(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const token = getStoredToken();
    const tournamentId = tournaments[0]?.id;
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
    router.push(`/app/pools/${pool.id}`);
  }

  return (
    <section className="pageStack">
      <div className="pageHeader">
        <div>
          <p className="eyebrow">Pools</p>
          <h1>Create pool</h1>
        </div>
      </div>
      <form className="form panel narrowPanel" onSubmit={createPool}>
        <label>
          Pool name
          <input required type="text" value={name} onChange={(event) => setName(event.target.value)} />
        </label>
        <label>
          Tournament
          <select disabled value={tournaments[0]?.id ?? ""}>
            {tournaments.map((tournament) => (
              <option key={tournament.id} value={tournament.id}>{tournament.name}</option>
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
        <button className="button" type="submit">Create pool</button>
        <p className="statusText">{status}</p>
      </form>
    </section>
  );
}

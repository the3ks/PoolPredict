"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { apiUrl, readApiError } from "../../../lib/api";
import { getStoredToken } from "../../../lib/auth";

export default function JoinPoolPage() {
  const router = useRouter();
  const [inviteCode, setInviteCode] = useState("");
  const [status, setStatus] = useState("Enter an invite code.");

  async function joinPool(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const token = getStoredToken();
    if (!token) {
      setStatus("Session is missing.");
      return;
    }

    const response = await fetch(apiUrl("/api/pools/join"), {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ inviteCode }),
    });

    if (!response.ok) {
      setStatus(await readApiError(response, "Join failed."));
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
          <h1>Join pool</h1>
        </div>
      </div>
      <form className="form panel narrowPanel" onSubmit={joinPool}>
        <label>
          Invite code
          <input required type="text" value={inviteCode} onChange={(event) => setInviteCode(event.target.value)} />
        </label>
        <button className="button" type="submit">Join pool</button>
        <p className="statusText">{status}</p>
      </form>
    </section>
  );
}

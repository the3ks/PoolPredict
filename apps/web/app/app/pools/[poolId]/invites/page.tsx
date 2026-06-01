"use client";

import { useParams } from "next/navigation";
import { useState } from "react";
import { KeyRound, Plus } from "lucide-react";
import { IconLabel, PageHeader } from "../../../../components/ui";
import { apiUrl, readApiError } from "../../../../lib/api";
import { getStoredToken } from "../../../../lib/auth";
import { PoolInvite } from "../../../../lib/types";

export default function PoolInvitesPage() {
  const params = useParams<{ poolId: string }>();
  const [latestInvite, setLatestInvite] = useState<PoolInvite | null>(null);
  const [status, setStatus] = useState("Create an invite code for this pool.");

  async function createInvite() {
    const token = getStoredToken();
    if (!token) {
      setStatus("Session is missing.");
      return;
    }

    const response = await fetch(apiUrl(`/api/pools/${params.poolId}/invites`), {
      method: "POST",
      headers: { Authorization: `Bearer ${token}` },
    });

    if (!response.ok) {
      setStatus(await readApiError(response, "Invite creation failed."));
      return;
    }

    const invite = (await response.json()) as PoolInvite;
    setLatestInvite(invite);
    setStatus("Invite ready.");
  }

  return (
    <section className="pageStack">
      <PageHeader
        eyebrow="Invites"
        title="Pool invites"
        icon={KeyRound}
        actions={<button className="button" type="button" onClick={createInvite}><IconLabel icon={Plus}>Create invite</IconLabel></button>}
      />
      <section className="invitePanel">
        <div>
          <p className="eyebrow">Latest code</p>
          <h2>{status}</h2>
        </div>
        {latestInvite ? <code>{latestInvite.code}</code> : null}
      </section>
    </section>
  );
}

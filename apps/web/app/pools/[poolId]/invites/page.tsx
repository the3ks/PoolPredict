"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { ArrowLeft, Ban, KeyRound, Plus, RefreshCw } from "lucide-react";
import { UserShell } from "../../../components/user-shell";
import {
  IconLabel,
  PageHeader,
  Panel,
  StatusPill,
} from "../../../components/ui";
import { apiUrl, readApiError } from "../../../lib/api";
import { getStoredToken } from "../../../lib/auth";
import { formatDisplayDateTime } from "../../../lib/datetime";
import { PoolInvite, PoolSummary } from "../../../lib/types";

export default function PoolInvitesPage() {
  const params = useParams<{ poolId: string }>();
  const [pool, setPool] = useState<PoolSummary | null>(null);
  const [invites, setInvites] = useState<PoolInvite[]>([]);
  const [status, setStatus] = useState("Loading invite codes...");
  const [isBusy, setIsBusy] = useState(false);

  useEffect(() => {
    void Promise.all([loadPool(), loadInvites()]);
  }, [params.poolId]);

  async function loadPool() {
    const token = getStoredToken();
    if (!token) {
      setStatus("Session is missing.");
      return;
    }

    try {
      const response = await fetch(apiUrl(`/api/pools/${params.poolId}`), {
        headers: { Authorization: `Bearer ${token}` },
      });

      if (response.ok) {
        setPool((await response.json()) as PoolSummary);
      }
    } catch {
      // The invite list remains usable even if the heading cannot load the pool name.
    }
  }

  async function loadInvites() {
    const token = getStoredToken();
    if (!token) {
      setStatus("Session is missing.");
      return;
    }

    setIsBusy(true);
    setStatus("Loading invite codes...");
    try {
      const response = await fetch(
        apiUrl(`/api/pools/${params.poolId}/invites`),
        {
          headers: { Authorization: `Bearer ${token}` },
        },
      );

      if (!response.ok) {
        setStatus(await readApiError(response, "Could not load invite codes."));
        return;
      }

      const result = (await response.json()) as PoolInvite[];
      setInvites(result);
      setStatus(
        result.length === 0
          ? "No invite codes created yet."
          : `${result.length} invite code${result.length === 1 ? "" : "s"} loaded.`,
      );
    } catch (error) {
      setStatus(
        error instanceof Error ? error.message : "Could not load invite codes.",
      );
    } finally {
      setIsBusy(false);
    }
  }

  async function createInvite() {
    const token = getStoredToken();
    if (!token) {
      setStatus("Session is missing.");
      return;
    }

    setIsBusy(true);
    setStatus("Creating invite code...");
    try {
      const response = await fetch(
        apiUrl(`/api/pools/${params.poolId}/invites`),
        {
          method: "POST",
          headers: { Authorization: `Bearer ${token}` },
        },
      );

      if (!response.ok) {
        setStatus(await readApiError(response, "Invite creation failed."));
        return;
      }

      setStatus("Invite code created.");
      await loadInvites();
    } catch (error) {
      setStatus(
        error instanceof Error ? error.message : "Invite creation failed.",
      );
    } finally {
      setIsBusy(false);
    }
  }

  async function revokeInvite(invite: PoolInvite) {
    const token = getStoredToken();
    if (!token || !invite.id) {
      setStatus("Session or invite ID is missing.");
      return;
    }

    setIsBusy(true);
    setStatus(`Revoking ${invite.code}...`);
    try {
      const response = await fetch(
        apiUrl(`/api/pools/${params.poolId}/invites/${invite.id}/revoke`),
        {
          method: "POST",
          headers: { Authorization: `Bearer ${token}` },
        },
      );

      if (!response.ok) {
        setStatus(await readApiError(response, "Invite revocation failed."));
        return;
      }

      setStatus("Invite code revoked.");
      await loadInvites();
    } catch (error) {
      setStatus(
        error instanceof Error ? error.message : "Invite revocation failed.",
      );
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <UserShell>
      <section className="pageStack">
        <PageHeader
          eyebrow="Invites"
          title={`${pool?.name ?? "Pool"}`}
          icon={KeyRound}
          actions={
            <>
              <Link
                className="button buttonSecondary"
                href={`/pools/${params.poolId}`}
              >
                <IconLabel icon={ArrowLeft}>Back to pool</IconLabel>
              </Link>
              <button
                className="button"
                disabled={isBusy}
                type="button"
                onClick={createInvite}
              >
                <IconLabel icon={Plus}>Create invite</IconLabel>
              </button>
              <button
                className="button buttonSecondary"
                disabled={isBusy}
                type="button"
                onClick={loadInvites}
              >
                <IconLabel icon={RefreshCw}>Refresh</IconLabel>
              </button>
            </>
          }
        />
        <StatusPill icon={KeyRound}>{status}</StatusPill>
        <Panel title="Invite codes">
          {invites.length === 0 ? (
            <p className="mutedText">No invite codes created yet.</p>
          ) : (
            <div className="inviteList">
              {invites.map((invite) => (
                <article
                  className={
                    invite.isRevoked ? "inviteRow revoked" : "inviteRow"
                  }
                  key={invite.id ?? invite.code}
                >
                  <span>
                    <strong>{invite.code}</strong>
                    <small>
                      Created{" "}
                      {invite.createdAt
                        ? formatDisplayDateTime(invite.createdAt)
                        : "unknown"}
                    </small>
                  </span>
                  <span>
                    <strong>{invite.isRevoked ? "Revoked" : "Active"}</strong>
                    <small>
                      {invite.revokedAt
                        ? formatDisplayDateTime(invite.revokedAt)
                        : "Can be used to join"}
                    </small>
                  </span>
                  <button
                    className="button buttonSecondary compactButton"
                    disabled={isBusy || Boolean(invite.isRevoked)}
                    type="button"
                    onClick={() => revokeInvite(invite)}
                  >
                    <IconLabel icon={Ban}>Revoke</IconLabel>
                  </button>
                </article>
              ))}
            </div>
          )}
        </Panel>
      </section>
    </UserShell>
  );
}

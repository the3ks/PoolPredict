"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  BadgeDollarSign,
  CalendarDays,
  Hash,
  LogIn,
  Plus,
  ShieldCheck,
  Trophy,
  Users,
} from "lucide-react";
import { IconLabel, PageHeader, Panel, StatusPill } from "./components/ui";
import { apiUrl } from "./lib/api";
import { getStoredToken, subscribeToAuthChanges } from "./lib/auth";
import { UserShell } from "./components/user-shell";
import { appName } from "./lib/config";
import { formatDisplayDate } from "./lib/datetime";
import { DiscoverPool, PoolSummary, Tournament } from "./lib/types";

export default function Home() {
  const router = useRouter();
  const [tournaments, setTournaments] = useState<Tournament[]>([]);
  const [myPools, setMyPools] = useState<PoolSummary[]>([]);
  const [communityPools, setCommunityPools] = useState<DiscoverPool[]>([]);
  const [status, setStatus] = useState("Loading tournaments...");
  const [isSignedIn, setIsSignedIn] = useState(false);

  useEffect(() => {
    syncAuthState();
    const unsubscribe = subscribeToAuthChanges(syncAuthState);

    if (!getStoredToken()) {
      router.replace("/login");
      return unsubscribe;
    }

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

    return unsubscribe;
  }, [router]);

  function syncAuthState() {
    const token = getStoredToken();
    setIsSignedIn(Boolean(token));
    if (!token) {
      router.replace("/login");
    }
    if (token) {
      void loadPoolPreviews(token);
      return;
    }

    setMyPools([]);
    setCommunityPools([]);
  }

  async function loadPoolPreviews(token: string) {
    try {
      const [myPoolsResponse, communityPoolsResponse] = await Promise.all([
        fetch(apiUrl("/api/pools"), {
          headers: { Authorization: `Bearer ${token}` },
        }),
        fetch(apiUrl("/api/pools/discover"), {
          headers: { Authorization: `Bearer ${token}` },
        }),
      ]);

      if (myPoolsResponse.ok) {
        setMyPools((await myPoolsResponse.json()) as PoolSummary[]);
      }

      if (communityPoolsResponse.ok) {
        const pools = (await communityPoolsResponse.json()) as DiscoverPool[];
        setCommunityPools(pickRandomPools(pools, 3));
      }
    } catch {
      setMyPools([]);
      setCommunityPools([]);
    }
  }

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
        {isSignedIn && myPools.length > 0 ? (
          <Panel title="My Pools">
            <div className="poolList">
              {myPools.map((pool) => (
                <article className="poolCard" key={pool.id}>
                  <Link className="poolCardLink" href={`/pools/${pool.id}`}>
                    <span>
                      <strong>{pool.name}</strong>
                      <small>{pool.profile} profile</small>
                    </span>
                    <span>
                      <strong>{pool.memberCount}</strong>
                      <small>
                        <IconLabel icon={Users}>members</IconLabel>
                      </small>
                    </span>
                    <span>
                      <strong>{pool.role}</strong>
                      <small>
                        <IconLabel icon={pool.role === "Member" ? Hash : ShieldCheck}>
                          {pool.inviteCount} invites
                        </IconLabel>
                      </small>
                    </span>
                  </Link>
                </article>
              ))}
            </div>
          </Panel>
        ) : null}

        {isSignedIn ? (
          <Panel title="Community">
            <div className="homePanelActionRow">
              <Link className="button buttonSecondary compactButton" href="/pools">
                Browse more
              </Link>
            </div>
            <div className="poolList">
              {communityPools.length === 0 ? (
                <p className="mutedText">
                  No other pools are available right now.
                </p>
              ) : null}
              {communityPools.map((pool) => (
                <article className="poolCard" key={pool.id}>
                  <Link className="poolCardLink homeCommunityCardLink" href="/pools">
                    <span>
                      <strong>{pool.name}</strong>
                      <small>{pool.profile} profile</small>
                    </span>
                    <span>
                      <strong>{pool.tournamentName}</strong>
                      <small>
                        <IconLabel icon={Trophy}>
                          {pool.provider}
                          {pool.isTestData ? " test data" : ""}
                        </IconLabel>
                      </small>
                    </span>
                    <span>
                      <strong>{pool.memberCount}</strong>
                      <small>
                        <IconLabel icon={Users}>members</IconLabel>
                      </small>
                    </span>
                    <span>
                      <strong>{pool.startingBalance}</strong>
                      <small>
                        <IconLabel icon={BadgeDollarSign}>
                          balance
                        </IconLabel>
                      </small>
                    </span>
                  </Link>
                </article>
              ))}
            </div>
          </Panel>
        ) : null}

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
                  <dd>{formatDisplayDate(tournament.startsOn)}</dd>
                </div>
                <div>
                  <dt>Ends</dt>
                  <dd>{formatDisplayDate(tournament.endsOn)}</dd>
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

function pickRandomPools<T>(items: T[], count: number) {
  return [...items].sort(() => Math.random() - 0.5).slice(0, count);
}

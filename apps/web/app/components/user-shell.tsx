"use client";

import Link from "next/link";
import { ReactNode, useEffect, useState } from "react";
import { CalendarDays, Home, LogIn, LogOut, Shield, UserPlus, Waves } from "lucide-react";
import { clearToken, getStoredToken, UserProfile } from "../lib/auth";
import { appName } from "../lib/config";
import { IconLabel } from "./ui";
import { ThemeToggle } from "./theme-toggle";
import { apiUrl } from "../lib/api";

export function UserShell({ children }: { children: ReactNode }) {
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [sessionState, setSessionState] = useState<"checking" | "signedIn" | "signedOut">("checking");

  useEffect(() => {
    const token = getStoredToken();

    if (!token) {
      setSessionState("signedOut");
      return;
    }

    try {
      fetch(apiUrl("/api/auth/me"), {
        headers: { Authorization: `Bearer ${token}` },
      })
        .then(async (response) => {
          if (!response.ok) {
            clearToken();
            setProfile(null);
            setSessionState("signedOut");
            return;
          }

          setProfile(await response.json());
          setSessionState("signedIn");
        })
        .catch(() => {
          clearToken();
          setProfile(null);
          setSessionState("signedOut");
        });
    } catch {
      clearToken();
      setProfile(null);
      setSessionState("signedOut");
    }
  }, []);

  const isSignedIn = sessionState === "signedIn";

  function signOut() {
    clearToken();
    setSessionState("signedOut");
    setProfile(null);
  }

  return (
    <main className="publicShell">
      <header className="publicTopbar">
        <Link className="brandLink" href="/">
          <span aria-hidden="true" className="brandBallIcon">⚽</span>
          {appName}
        </Link>
        <nav className="userNav" aria-label="Primary">
          <Link href="/">
            <IconLabel icon={Home}>Home</IconLabel>
          </Link>
          <Link href="/wc2026">
            <IconLabel icon={CalendarDays}>Lịch WC2026</IconLabel>
          </Link>
          <Link href="/pools">
            <IconLabel icon={Waves}>Pools</IconLabel>
          </Link>
          {profile?.role === "PlatformAdmin" ? (
            <Link href="/admin">
              <IconLabel icon={Shield}>Admin</IconLabel>
            </Link>
          ) : null}
        </nav>
        <div className="buttonRow">
          <ThemeToggle />
          {sessionState === "checking" ? null : isSignedIn ? (
            <>
              <Link className="appProfileLink" href="/profile">
                <span className="appProfileIdentity">
                  {profile?.avatarUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img alt="" className="topbarAvatar" src={profile.avatarUrl} />
                  ) : (
                    <span className="topbarAvatarFallback">
                      {(profile?.displayName ?? "P").slice(0, 1).toUpperCase()}
                    </span>
                  )}
                  <span>{profile?.displayName ?? "Profile"}</span>
                </span>
              </Link>
              <button
                className="button buttonSecondary"
                type="button"
                onClick={signOut}
              >
                <IconLabel icon={LogOut}>Sign out</IconLabel>
              </button>
            </>
          ) : (
            <>
              <Link className="button buttonSecondary" href="/login">
                <IconLabel icon={LogIn}>Login</IconLabel>
              </Link>
              <Link className="button" href="/register">
                <IconLabel icon={UserPlus}>Register</IconLabel>
              </Link>
            </>
          )}
        </div>
      </header>
      {children}
    </main>
  );
}

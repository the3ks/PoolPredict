"use client";

import Link from "next/link";
import { ReactNode, useEffect, useState } from "react";
import { LogIn, LogOut, Shield, UserPlus, UserRound, Waves } from "lucide-react";
import { clearToken, getStoredToken, UserProfile } from "../lib/auth";
import { appName } from "../lib/config";
import { IconLabel } from "./ui";
import { ThemeToggle } from "./theme-toggle";
import { apiUrl } from "../lib/api";

export function UserShell({ children }: { children: ReactNode }) {
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [isSignedIn, setIsSignedIn] = useState(false);

  useEffect(() => {
    const token = getStoredToken();
    setIsSignedIn(Boolean(token));

    if (!token) {
      return;
    }

    fetch(apiUrl("/api/auth/me"), {
      headers: { Authorization: `Bearer ${token}` },
    }).then(async (response) => {
      if (response.ok) {
        setProfile(await response.json());
      }
    }).catch(() => {
      setProfile(null);
    });
  }, []);

  function signOut() {
    clearToken();
    setIsSignedIn(false);
    setProfile(null);
  }

  return (
    <main className="publicShell">
      <header className="publicTopbar">
        <Link className="brandLink" href="/">
          {appName}
        </Link>
        <nav className="userNav" aria-label="Primary">
          <Link href="/">Tournaments</Link>
          {isSignedIn ? <Link href="/pools">Pools</Link> : null}
          {isSignedIn ? <Link href="/profile">Profile</Link> : null}
          {profile?.role === "PlatformAdmin" ? <Link href="/admin"><IconLabel icon={Shield}>Admin</IconLabel></Link> : null}
        </nav>
        <div className="buttonRow">
          <ThemeToggle />
          {isSignedIn ? (
            <button className="button buttonSecondary" type="button" onClick={signOut}>
              <IconLabel icon={LogOut}>Sign out</IconLabel>
            </button>
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
      {isSignedIn ? (
        <div className="userQuickActions">
          <Link className="button buttonSecondary" href="/pools">
            <IconLabel icon={Waves}>Pools</IconLabel>
          </Link>
          <Link className="button buttonSecondary" href="/profile">
            <IconLabel icon={UserRound}>Profile</IconLabel>
          </Link>
        </div>
      ) : null}
      {children}
    </main>
  );
}

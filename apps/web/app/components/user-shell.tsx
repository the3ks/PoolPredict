"use client";

import Link from "next/link";
import { ReactNode, useEffect, useState } from "react";
import { Home, LogIn, LogOut, Shield, UserPlus, Waves } from "lucide-react";
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
    })
      .then(async (response) => {
        if (response.ok) {
          setProfile(await response.json());
        }
      })
      .catch(() => {
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
          <span aria-hidden="true" className="brandBallIcon">⚽</span>
          {appName}
        </Link>
        <nav className="userNav" aria-label="Primary">
          <Link href="/">
            <IconLabel icon={Home}>Home</IconLabel>
          </Link>
          {isSignedIn ? (
            <Link href="/pools">
              <IconLabel icon={Waves}>Pools</IconLabel>
            </Link>
          ) : null}
          {profile?.role === "PlatformAdmin" ? (
            <Link href="/admin">
              <IconLabel icon={Shield}>Admin</IconLabel>
            </Link>
          ) : null}
        </nav>
        <div className="buttonRow">
          <ThemeToggle />
          {isSignedIn ? (
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

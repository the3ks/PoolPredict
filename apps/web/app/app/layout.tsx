"use client";

import Link from "next/link";
import { ReactNode, useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { apiBaseUrl, apiUrl } from "../lib/api";
import { clearToken, getStoredToken, UserProfile } from "../lib/auth";

const navItems = [
  { href: "/app", label: "Dashboard" },
  { href: "/app/pools", label: "Pools" },
  { href: "/app/profile", label: "Profile" },
];

export default function AppLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [status, setStatus] = useState("Loading session...");

  useEffect(() => {
    const token = getStoredToken();
    if (!token) {
      router.replace("/login");
      return;
    }

    if (!apiBaseUrl) {
      setStatus("NEXT_PUBLIC_API_BASE_URL is not configured.");
      return;
    }

    fetch(apiUrl("/api/auth/me"), {
      headers: { Authorization: `Bearer ${token}` },
    }).then(async (response) => {
      if (!response.ok) {
        clearToken();
        router.replace("/login");
        return;
      }

      setProfile(await response.json());
      setStatus("Session active");
    });
  }, [router]);

  function signOut() {
    clearToken();
    router.replace("/login");
  }

  return (
    <div className="appShell">
      <header className="appTopbar">
        <Link className="brandLink" href="/app">PoolPredict</Link>
        <div className="appUser">
          <span>{profile?.displayName ?? status}</span>
          <button className="button buttonSecondary compactButton" type="button" onClick={signOut}>Sign out</button>
        </div>
      </header>
      <aside className="appSidebar">
        <nav>
          {navItems.map((item) => (
            <Link className={pathname === item.href ? "active" : ""} href={item.href} key={item.href}>
              {item.label}
            </Link>
          ))}
          {profile?.role === "PlatformAdmin" ? (
            <Link className={pathname === "/app/admin" ? "active" : ""} href="/app/admin">
              Admin
            </Link>
          ) : null}
        </nav>
      </aside>
      <main className="appMain">{children}</main>
    </div>
  );
}

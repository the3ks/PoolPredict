"use client";

import Link from "next/link";
import { ReactNode, useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { CalendarClock, CheckCircle2, Gauge, LogOut, Settings, Shield, SlidersHorizontal, Trophy, UserRound, Users, Waves } from "lucide-react";
import { ThemeToggle } from "../components/theme-toggle";
import { IconLabel } from "../components/ui";
import { apiBaseUrl, apiUrl } from "../lib/api";
import { clearToken, getStoredToken, UserProfile } from "../lib/auth";
import { appName } from "../lib/config";

const navItems = [
  { href: "/app", label: "Dashboard", icon: Gauge },
  { href: "/app/pools", label: "Pools", icon: Waves },
  { href: "/app/profile", label: "Profile", icon: UserRound },
];

const adminNavItems = [
  { href: "/app/admin/provider", label: "Tournament provider", icon: Trophy },
  { href: "/app/admin/events", label: "Event management", icon: CalendarClock },
  { href: "/app/admin/settlement", label: "Settlement", icon: CheckCircle2 },
  { href: "/app/admin/payout", label: "Payout", icon: SlidersHorizontal },
  { href: "/app/admin/users", label: "User management", icon: Users },
  { href: "/app/admin/system", label: "System settings", icon: Settings },
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
        <Link className="brandLink" href="/app">{appName}</Link>
        <div className="appUser">
          <span>{profile?.displayName ?? status}</span>
          <ThemeToggle />
          <button className="button buttonSecondary compactButton" type="button" onClick={signOut}>
            <IconLabel icon={LogOut}>Sign out</IconLabel>
          </button>
        </div>
      </header>
      <aside className="appSidebar">
        <nav>
          {navItems.map((item) => (
            <Link className={pathname === item.href ? "active" : ""} href={item.href} key={item.href}>
              <item.icon aria-hidden="true" size={18} strokeWidth={2.2} />
              <span>{item.label}</span>
            </Link>
          ))}
          {profile?.role === "PlatformAdmin" ? (
            <div className="sidebarGroup">
              <Link className={pathname === "/app/admin" ? "active" : ""} href="/app/admin">
                <Shield aria-hidden="true" size={18} strokeWidth={2.2} />
                <span>Admin</span>
              </Link>
              <div className="sidebarSubnav">
                {adminNavItems.map((item) => (
                  <Link className={pathname === item.href ? "active" : ""} href={item.href} key={item.href}>
                    <item.icon aria-hidden="true" size={16} strokeWidth={2.2} />
                    <span>{item.label}</span>
                  </Link>
                ))}
              </div>
            </div>
          ) : null}
        </nav>
      </aside>
      <main className="appMain">{children}</main>
    </div>
  );
}

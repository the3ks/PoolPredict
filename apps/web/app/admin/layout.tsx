"use client";

import Link from "next/link";
import { ReactNode, useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { CalendarClock, CheckCircle2, Gauge, LogOut, Settings, Shield, SlidersHorizontal, Trophy, Users, Waves } from "lucide-react";
import { apiBaseUrl, apiUrl } from "../lib/api";
import { clearToken, getStoredToken, UserProfile } from "../lib/auth";
import { appName } from "../lib/config";
import { ThemeToggle } from "../components/theme-toggle";
import { IconLabel } from "../components/ui";

const adminNavItems = [
  { href: "/admin/pools", label: "Pools", icon: Waves },
  { href: "/admin/provider", label: "Tournament provider", icon: Trophy },
  { href: "/admin/events", label: "Event management", icon: CalendarClock },
  { href: "/admin/settlement", label: "Settlement", icon: CheckCircle2 },
  { href: "/admin/payout", label: "Payout", icon: SlidersHorizontal },
  { href: "/admin/users", label: "User management", icon: Users },
  {
    href: "/admin/system",
    label: "System settings",
    icon: Settings,
    children: [
      { href: "/admin/system", label: "SMTP settings" },
      { href: "/admin/system/backup", label: "Database backup" },
    ],
  },
];

export default function AdminLayout({ children }: { children: ReactNode }) {
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

      const result = (await response.json()) as UserProfile;
      if (result.role !== "PlatformAdmin") {
        router.replace("/");
        return;
      }

      setProfile(result);
      setStatus("Admin session active");
    });
  }, [router]);

  function signOut() {
    clearToken();
    router.replace("/login");
  }

  return (
    <div className="appShell">
      <header className="appTopbar">
        <Link className="brandLink" href="/admin">{appName} Admin</Link>
        <div className="appUser">
          <Link className="button buttonSecondary compactButton" href="/">
            <IconLabel icon={Gauge}>User app</IconLabel>
          </Link>
          <ThemeToggle />
          <Link className="appProfileLink" href="/profile">
            <span className="appProfileIdentity">
              {profile?.avatarUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img alt="" className="topbarAvatar" src={profile.avatarUrl} />
              ) : (
                <span className="topbarAvatarFallback">
                  {(profile?.displayName ?? "A").slice(0, 1).toUpperCase()}
                </span>
              )}
              <span>{profile?.displayName ?? status}</span>
            </span>
          </Link>
          <button className="button buttonSecondary compactButton" type="button" onClick={signOut}>
            <IconLabel icon={LogOut}>Sign out</IconLabel>
          </button>
        </div>
      </header>
      <aside className="appSidebar">
        <nav>
          <Link className={pathname === "/admin" ? "active" : ""} href="/admin">
            <Shield aria-hidden="true" size={18} strokeWidth={2.2} />
            <span>Admin dashboard</span>
          </Link>
          {adminNavItems.map((item) => {
            const parentActive = pathname === item.href || pathname.startsWith(`${item.href}/`);
            if (!item.children) {
              return (
                <Link className={pathname === item.href ? "active" : ""} href={item.href} key={item.href}>
                  <item.icon aria-hidden="true" size={18} strokeWidth={2.2} />
                  <span>{item.label}</span>
                </Link>
              );
            }

            return (
              <div className="sidebarGroup" key={item.href}>
                <Link className={parentActive ? "active" : ""} href={item.href}>
                  <item.icon aria-hidden="true" size={18} strokeWidth={2.2} />
                  <span>{item.label}</span>
                </Link>
                <div className="sidebarSubnav">
                  {item.children.map((child) => (
                    <Link className={pathname === child.href ? "active" : ""} href={child.href} key={child.href}>
                      <span>{child.label}</span>
                    </Link>
                  ))}
                </div>
              </div>
            );
          })}
        </nav>
      </aside>
      <main className="appMain">{children}</main>
    </div>
  );
}

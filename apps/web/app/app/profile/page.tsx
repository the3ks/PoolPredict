"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { apiUrl } from "../../lib/api";
import { clearToken, getStoredToken, UserProfile } from "../../lib/auth";

export default function ProfilePage() {
  const router = useRouter();
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [status, setStatus] = useState("Loading profile...");

  useEffect(() => {
    const token = getStoredToken();
    if (!token) {
      router.replace("/login");
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
      setStatus("Profile loaded.");
    }).catch((error) => setStatus(error instanceof Error ? error.message : "Could not load profile."));
  }, [router]);

  function signOut() {
    clearToken();
    router.replace("/login");
  }

  return (
    <section className="pageStack">
      <div className="pageHeader">
        <div>
          <p className="eyebrow">Profile</p>
          <h1>{profile?.displayName ?? "Profile"}</h1>
        </div>
      </div>
      <section className="panel narrowPanel">
        <p className="statusText">{status}</p>
        {profile ? (
          <dl className="detailList">
            <div><dt>Email</dt><dd>{profile.email}</dd></div>
            <div><dt>Role</dt><dd>{profile.role}</dd></div>
            <div><dt>User ID</dt><dd>{profile.id}</dd></div>
          </dl>
        ) : null}
        <button className="button buttonSecondary" type="button" onClick={signOut}>Sign out</button>
      </section>
    </section>
  );
}

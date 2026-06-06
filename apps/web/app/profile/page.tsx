"use client";

import { FormEvent, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Eye, EyeOff, LogOut, Save, UserRound } from "lucide-react";
import { UserShell } from "../components/user-shell";
import { IconLabel, PageHeader, Panel } from "../components/ui";
import { apiUrl, readApiError } from "../lib/api";
import { clearToken, getStoredToken, UserProfile } from "../lib/auth";

export default function ProfilePage() {
  const router = useRouter();
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [status, setStatus] = useState("Loading profile...");
  const [displayName, setDisplayName] = useState("");
  const [avatarUrl, setAvatarUrl] = useState("");
  const [profileStatus, setProfileStatus] = useState("Update your display name and avatar.");
  const [isSavingProfile, setIsSavingProfile] = useState(false);
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [passwordStatus, setPasswordStatus] = useState("Use this if you need to update your password.");
  const [isChangingPassword, setIsChangingPassword] = useState(false);

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

      const result = (await response.json()) as UserProfile;
      setProfile(result);
      setDisplayName(result.displayName);
      setAvatarUrl(result.avatarUrl ?? "");
      setStatus("Profile loaded.");
    }).catch((error) => setStatus(error instanceof Error ? error.message : "Could not load profile."));
  }, [router]);

  function signOut() {
    clearToken();
    router.replace("/login");
  }

  async function updateProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const token = getStoredToken();
    if (!token) {
      setProfileStatus("Session is missing.");
      return;
    }

    setIsSavingProfile(true);
    setProfileStatus("Saving profile...");
    try {
      const response = await fetch(apiUrl("/api/auth/me"), {
        method: "PUT",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          displayName,
          avatarUrl: avatarUrl.trim() || null,
        }),
      });

      if (!response.ok) {
        setProfileStatus(await readApiError(response, "Could not update profile."));
        return;
      }

      const result = (await response.json()) as UserProfile;
      setProfile(result);
      setDisplayName(result.displayName);
      setAvatarUrl(result.avatarUrl ?? "");
      setProfileStatus("Profile updated.");
    } catch (error) {
      setProfileStatus(error instanceof Error ? error.message : "Could not update profile.");
    } finally {
      setIsSavingProfile(false);
    }
  }

  async function changePassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const token = getStoredToken();
    if (!token) {
      setPasswordStatus("Session is missing.");
      return;
    }

    setIsChangingPassword(true);
    setPasswordStatus("Changing password...");
    try {
      const response = await fetch(apiUrl("/api/auth/change-password"), {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ currentPassword, newPassword }),
      });

      if (!response.ok) {
        setPasswordStatus(await readApiError(response, "Could not change password."));
        return;
      }

      const result = (await response.json()) as { message: string };
      setPasswordStatus(result.message);
      setCurrentPassword("");
      setNewPassword("");
    } catch (error) {
      setPasswordStatus(error instanceof Error ? error.message : "Could not change password.");
    } finally {
      setIsChangingPassword(false);
    }
  }

  return (
    <UserShell>
      <section className="pageStack">
        <PageHeader eyebrow="Profile" title={profile?.displayName ?? "Profile"} icon={UserRound} />
        <div className="profileGrid">
          <Panel className="profileReadPanel" title="Account">
            <p className="statusText">{status}</p>
            {profile ? (
              <>
                <div className="profileStaticHeader">
                  <div className="profileAvatarPreview">
                    {avatarUrl ? (
                      // eslint-disable-next-line @next/next/no-img-element
                      <img alt={displayName || profile.displayName || "Profile avatar"} src={avatarUrl} />
                    ) : (
                      <span>{(displayName || profile.displayName || "P").slice(0, 1).toUpperCase()}</span>
                    )}
                  </div>
                  <div className="profileIdentityBlock">
                    <strong>{profile.displayName}</strong>
                    <small>{profile.email}</small>
                  </div>
                </div>
                <dl className="detailList profileStaticDetails">
                  <div><dt>Role</dt><dd>{profile.role}</dd></div>
                  <div><dt>Email verified</dt><dd>{profile.isEmailVerified ? "Yes" : "No"}</dd></div>
                  <div><dt>Password change required</dt><dd>{profile.mustChangePassword ? "Yes" : "No"}</dd></div>
                  <div><dt>User ID</dt><dd>{profile.id}</dd></div>
                </dl>
              </>
            ) : null}
            <button className="button buttonSecondary" type="button" onClick={signOut}><IconLabel icon={LogOut}>Sign out</IconLabel></button>
          </Panel>
          <div className="profileActionColumn">
            <Panel className="profileCompactPanel" title="Edit">
              <form className="form profileCompactForm" onSubmit={updateProfile}>
                <p className="statusText">{profileStatus}</p>
                <label>
                  Display name
                  <input maxLength={200} required type="text" value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
                </label>
                <label>
                  Avatar URL
                  <input placeholder="https://..." type="url" value={avatarUrl} onChange={(event) => setAvatarUrl(event.target.value)} />
                </label>
                <button className="button compactButton" disabled={isSavingProfile} type="submit"><IconLabel icon={Save}>Save profile</IconLabel></button>
              </form>
            </Panel>
            <Panel className="profileCompactPanel" title="Password">
              <form className="form profileCompactForm" onSubmit={changePassword}>
                <p className="statusText">{passwordStatus}</p>
                <label>
                  Current password
                  <span className="passwordField">
                    <input autoComplete="current-password" minLength={8} required type={showCurrentPassword ? "text" : "password"} value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} />
                    <button aria-label={showCurrentPassword ? "Hide current password" : "Show current password"} aria-pressed={showCurrentPassword} className="passwordToggle" type="button" onClick={() => setShowCurrentPassword((current) => !current)}>
                      {showCurrentPassword ? <EyeOff size={18} /> : <Eye size={18} />}
                    </button>
                  </span>
                </label>
                <label>
                  New password
                  <span className="passwordField">
                    <input autoComplete="new-password" minLength={8} required type={showNewPassword ? "text" : "password"} value={newPassword} onChange={(event) => setNewPassword(event.target.value)} />
                    <button aria-label={showNewPassword ? "Hide new password" : "Show new password"} aria-pressed={showNewPassword} className="passwordToggle" type="button" onClick={() => setShowNewPassword((current) => !current)}>
                      {showNewPassword ? <EyeOff size={18} /> : <Eye size={18} />}
                    </button>
                  </span>
                </label>
                <button className="button compactButton" disabled={isChangingPassword} type="submit"><IconLabel icon={Save}>Change password</IconLabel></button>
              </form>
            </Panel>
          </div>
        </div>
      </section>
    </UserShell>
  );
}

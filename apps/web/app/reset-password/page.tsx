"use client";

import Link from "next/link";
import { FormEvent, useMemo, useState } from "react";
import { Eye, EyeOff, LogIn, Save } from "lucide-react";
import { ThemeToggle } from "../components/theme-toggle";
import { IconLabel } from "../components/ui";
import { apiUrl, readApiError } from "../lib/api";
import { appName } from "../lib/config";

export default function ResetPasswordPage() {
  const token = useMemo(() => (typeof window === "undefined" ? "" : new URLSearchParams(window.location.search).get("token") ?? ""), []);
  const [newPassword, setNewPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [status, setStatus] = useState(token ? "Enter a new password." : "Reset token is missing.");
  const [isBusy, setIsBusy] = useState(false);

  async function submitResetPassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      setStatus("Reset token is missing.");
      return;
    }

    setIsBusy(true);
    setStatus("Resetting password...");
    try {
      const response = await fetch(apiUrl("/api/auth/reset-password"), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token, newPassword }),
      });

      if (!response.ok) {
        setStatus(await readApiError(response, "Password reset failed."));
        return;
      }

      const result = (await response.json()) as { message: string };
      setStatus(result.message);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Password reset failed.");
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <main className="authShell">
      <section className="authCard">
        <div className="authTopline">
          <p className="eyebrow">{appName}</p>
          <ThemeToggle />
        </div>
        <h1>New password</h1>
        <form className="form" onSubmit={submitResetPassword}>
          <label>
            Password
            <span className="passwordField">
              <input
                autoComplete="new-password"
                minLength={8}
                required
                type={showPassword ? "text" : "password"}
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
              />
              <button
                aria-label={showPassword ? "Hide password" : "Show password"}
                aria-pressed={showPassword}
                className="passwordToggle"
                type="button"
                onClick={() => setShowPassword((current) => !current)}
              >
                {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
              </button>
            </span>
          </label>
          <button className="button" disabled={isBusy || !token} type="submit"><IconLabel icon={Save}>Set password</IconLabel></button>
        </form>
        <p className="statusText">{status}</p>
        <Link href="/login"><IconLabel icon={LogIn}>Back to login</IconLabel></Link>
      </section>
    </main>
  );
}

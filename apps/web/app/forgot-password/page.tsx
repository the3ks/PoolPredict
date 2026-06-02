"use client";

import Link from "next/link";
import { FormEvent, useState } from "react";
import { LogIn, Mail } from "lucide-react";
import { ThemeToggle } from "../components/theme-toggle";
import { IconLabel } from "../components/ui";
import { apiUrl, readApiError } from "../lib/api";
import { appName } from "../lib/config";

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [status, setStatus] = useState("Enter your account email.");
  const [isBusy, setIsBusy] = useState(false);

  async function submitForgotPassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsBusy(true);
    setStatus("Sending reset link...");

    try {
      const response = await fetch(apiUrl("/api/auth/forgot-password"), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
      });

      if (!response.ok) {
        setStatus(await readApiError(response, "Could not send reset link."));
        return;
      }

      const result = (await response.json()) as { message: string };
      setStatus(result.message);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Could not send reset link.");
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
        <h1>Reset password</h1>
        <form className="form" onSubmit={submitForgotPassword}>
          <label>
            Email
            <input autoComplete="email" inputMode="email" required type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
          </label>
          <button className="button" disabled={isBusy} type="submit"><IconLabel icon={Mail}>Send reset link</IconLabel></button>
        </form>
        <p className="statusText">{status}</p>
        <Link href="/login"><IconLabel icon={LogIn}>Back to login</IconLabel></Link>
      </section>
    </main>
  );
}

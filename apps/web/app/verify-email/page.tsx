"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { CheckCircle2, LogIn } from "lucide-react";
import { ThemeToggle } from "../components/theme-toggle";
import { IconLabel } from "../components/ui";
import { apiUrl, readApiError } from "../lib/api";
import { appName } from "../lib/config";

export default function VerifyEmailPage() {
  const [status, setStatus] = useState("Verifying email...");

  useEffect(() => {
    const token = new URLSearchParams(window.location.search).get("token");
    if (!token) {
      setStatus("Verification token is missing.");
      return;
    }

    fetch(apiUrl("/api/auth/verify-email"), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ token }),
    }).then(async (response) => {
      if (!response.ok) {
        setStatus(await readApiError(response, "Verification failed."));
        return;
      }

      const result = (await response.json()) as { message: string };
      setStatus(result.message);
    }).catch((error) => setStatus(error instanceof Error ? error.message : "Verification failed."));
  }, []);

  return (
    <main className="authShell">
      <section className="authCard">
        <div className="authTopline">
          <p className="eyebrow">{appName}</p>
          <ThemeToggle />
        </div>
        <h1>Email verification</h1>
        <p className="statusText"><IconLabel icon={CheckCircle2}>{status}</IconLabel></p>
        <Link href="/login"><IconLabel icon={LogIn}>Back to login</IconLabel></Link>
      </section>
    </main>
  );
}

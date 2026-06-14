"use client";

import Link from "next/link";
import { CheckCircle2, LogIn } from "lucide-react";
import { ThemeToggle } from "../components/theme-toggle";
import { IconLabel } from "../components/ui";
import { appName } from "../lib/config";

export default function VerifyEmailPage() {
  return (
    <main className="authShell">
      <section className="authCard">
        <div className="authTopline">
          <p className="eyebrow">{appName}</p>
          <ThemeToggle />
        </div>
        <h1>Email verification</h1>
        <p className="statusText">
          <IconLabel icon={CheckCircle2}>Self-activation is disabled. A platform admin must activate your account before you can log in.</IconLabel>
        </p>
        <Link href="/login"><IconLabel icon={LogIn}>Back to login</IconLabel></Link>
      </section>
    </main>
  );
}

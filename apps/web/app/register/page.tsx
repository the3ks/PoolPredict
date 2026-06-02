"use client";

import Link from "next/link";
import { FormEvent, useState } from "react";
import { Eye, EyeOff, LogIn, UserPlus } from "lucide-react";
import { ThemeToggle } from "../components/theme-toggle";
import { IconLabel } from "../components/ui";
import { apiUrl, readApiError } from "../lib/api";
import { appName } from "../lib/config";

export default function RegisterPage() {
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [status, setStatus] = useState("Create an account to start.");
  const [isBusy, setIsBusy] = useState(false);

  async function submitRegister(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsBusy(true);
    setStatus("Creating account...");

    try {
      const response = await fetch(apiUrl("/api/auth/register"), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, displayName, password }),
      });

      if (!response.ok) {
        setStatus(await readApiError(response, "Registration failed."));
        return;
      }

      const result = (await response.json()) as { message: string };
      setStatus(result.message);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Registration failed.");
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
        <h1>Register</h1>
        <form className="form" onSubmit={submitRegister}>
          <label>
            Email
            <input autoComplete="email" inputMode="email" required type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
          </label>
          <label>
            Display name
            <input autoComplete="name" required type="text" value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
          </label>
          <label>
            Password
            <span className="passwordField">
              <input
                autoComplete="new-password"
                id="register-password"
                minLength={8}
                required
                type={showPassword ? "text" : "password"}
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
              <button
                aria-controls="register-password"
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
          <button className="button" disabled={isBusy} type="submit"><IconLabel icon={UserPlus}>Create account</IconLabel></button>
        </form>
        <p className="statusText">{status}</p>
        <Link href="/login"><IconLabel icon={LogIn}>Back to login</IconLabel></Link>
      </section>
    </main>
  );
}

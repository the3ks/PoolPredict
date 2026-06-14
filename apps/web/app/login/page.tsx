"use client";

import Link from "next/link";
import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { LogIn, UserPlus } from "lucide-react";
import { ThemeToggle } from "../components/theme-toggle";
import { IconLabel } from "../components/ui";
import { apiUrl, readApiError } from "../lib/api";
import { AuthResponse, storeToken } from "../lib/auth";
import { appName } from "../lib/config";

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [status, setStatus] = useState("Sign in to continue.");
  const [isBusy, setIsBusy] = useState(false);

  async function submitLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsBusy(true);
    setStatus("Signing in...");
    await submitAuth("login", { email, password });
    setIsBusy(false);
  }

  async function submitAuth(path: string, body: object) {
    try {
      const response = await fetch(apiUrl(`/api/auth/${path}`), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });

      if (!response.ok) {
        const message = await readApiError(response, "Authentication failed.");
        setStatus(message);
        return;
      }

      const result = (await response.json()) as AuthResponse;
      storeToken(result.accessToken);
      router.push("/pools");
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Authentication failed.");
    }
  }

  return (
    <main className="authShell">
      <section className="authCard">
        <div className="authTopline">
          <p className="eyebrow">{appName}</p>
          <ThemeToggle />
        </div>
        <h1>Login</h1>
        <form className="form" onSubmit={submitLogin}>
          <label>
            Email
            <input autoComplete="email" inputMode="email" required type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
          </label>
          <label>
            Password
            <input autoComplete="current-password" minLength={8} required type="password" value={password} onChange={(event) => setPassword(event.target.value)} />
          </label>
          <button className="button" disabled={isBusy} type="submit"><IconLabel icon={LogIn}>Sign in</IconLabel></button>
        </form>
        <p className="statusText">{status}</p>
        <Link href="/forgot-password">Forgot password?</Link>
        <Link href="/register"><IconLabel icon={UserPlus}>Create an account</IconLabel></Link>
      </section>
    </main>
  );
}

"use client";

import Link from "next/link";
import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { apiUrl, readApiError } from "../lib/api";
import { AuthResponse, storeToken } from "../lib/auth";

export default function RegisterPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
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

      const result = (await response.json()) as AuthResponse;
      storeToken(result.accessToken);
      router.push("/app/pools");
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Registration failed.");
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <main className="authShell">
      <section className="authCard">
        <p className="eyebrow">PoolPredict</p>
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
            <input autoComplete="new-password" minLength={8} required type="password" value={password} onChange={(event) => setPassword(event.target.value)} />
          </label>
          <button className="button" disabled={isBusy} type="submit">Create account</button>
        </form>
        <p className="statusText">{status}</p>
        <Link href="/login">Back to login</Link>
      </section>
    </main>
  );
}

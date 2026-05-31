"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { getStoredToken } from "./lib/auth";

export default function Home() {
  const router = useRouter();

  useEffect(() => {
    router.replace(getStoredToken() ? "/app" : "/login");
  }, [router]);

  return (
    <main className="authShell">
      <section className="authCard">
        <p className="eyebrow">PoolPredict</p>
        <h1>Loading</h1>
      </section>
    </main>
  );
}

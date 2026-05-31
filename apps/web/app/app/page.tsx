"use client";

import Link from "next/link";

export default function DashboardPage() {
  return (
    <section className="pageStack">
      <div className="pageHeader">
        <div>
          <p className="eyebrow">Dashboard</p>
          <h1>Pool workspace</h1>
        </div>
      </div>
      <section className="panel">
        <h2>Start here</h2>
        <p className="mutedText">Use pools to create a World Cup prediction pool or join one by invite code.</p>
        <div className="buttonRow">
          <Link className="button" href="/app/pools">View pools</Link>
          <Link className="button buttonSecondary" href="/app/pools/new">Create pool</Link>
          <Link className="button buttonSecondary" href="/app/pools/join">Join pool</Link>
        </div>
      </section>
    </section>
  );
}

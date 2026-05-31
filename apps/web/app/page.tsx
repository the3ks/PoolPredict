const apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

export default async function Home() {
  return (
    <main className="shell">
      <section className="topbar">
        <div>
          <p className="eyebrow">PoolPredict MVP</p>
          <h1>Prediction pools with virtual points</h1>
        </div>
        <a className="button" href={`${apiBaseUrl}/health`}>
          API health
        </a>
      </section>

      <section className="grid">
        <article className="panel">
          <h2>Current Slice</h2>
          <p>
            The API can list seeded tournaments, create pools, auto-generate MVP markets,
            and snapshot point payout configuration when predictions are submitted.
          </p>
        </article>

        <article className="panel">
          <h2>MVP Rules</h2>
          <ul>
            <li>No real money or public betting odds.</li>
            <li>Platform Admin owns global point payout defaults.</li>
            <li>Pool owners create a pool, invite members, and stop there.</li>
          </ul>
        </article>
      </section>
    </main>
  );
}

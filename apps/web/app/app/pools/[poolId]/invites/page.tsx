import { redirect } from "next/navigation";

export default async function AppPoolInvitesPage({ params }: { params: Promise<{ poolId: string }> }) {
  const { poolId } = await params;
  redirect(`/pools/${poolId}/invites`);
}

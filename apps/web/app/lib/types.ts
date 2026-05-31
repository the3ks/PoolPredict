export type MarketProfile = "Casual" | "Standard" | "Expert";

export type Tournament = {
  id: string;
  name: string;
  sport: string;
};

export type PoolSummary = {
  id: string;
  name: string;
  tournamentId: string;
  profile: MarketProfile;
  startingBalance: number;
  role: string;
  memberCount: number;
  inviteCount: number;
};

export type PoolInvite = {
  code: string;
  poolId: string;
  createdAt?: string;
};

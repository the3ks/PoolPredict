export type MarketProfile = "Casual" | "Standard" | "Expert";

export type Tournament = {
  id: string;
  name: string;
  sport: string;
  startsOn: string;
  endsOn: string;
  provider: string;
  isTestData: boolean;
};

export type PoolSummary = {
  id: string;
  memberId?: string;
  name: string;
  tournamentId: string;
  profile: MarketProfile;
  startingBalance: number;
  role: string;
  memberCount: number;
  inviteCount: number;
};

export type DiscoverPool = {
  id: string;
  name: string;
  ownerDisplayName: string;
  ownerEmail: string;
  tournamentName: string;
  provider: string;
  isTestData: boolean;
  profile: string;
  startingBalance: number;
  memberCount: number;
  inviteCount: number;
  hasPendingJoinRequest: boolean;
};

export type PoolJoinRequest = {
  id: string;
  poolId: string;
  userId: string;
  displayName: string;
  email: string;
  status: string;
  requestedAt: string;
};

export type PoolInvite = {
  code: string;
  poolId: string;
  createdAt?: string;
};

export type TournamentEvent = {
  id: string;
  tournamentId: string;
  homeParticipant: string;
  awayParticipant: string;
  startsAt: string;
  status: string;
  managementMode: string;
  provider: string;
  isTestData: boolean;
};

export type Market = {
  id: string;
  poolId: string;
  eventId: string;
  type: string;
  period: string;
  lineValue: number | null;
  payoutMultiplier: number;
  payoutConfigurationVersion: number;
  status: string;
};

export type Prediction = {
  id: string;
  poolId: string;
  memberId: string;
  marketId: string;
  selectedOption: string;
  stake: number;
  marketType: string;
  marketPeriod: string;
  lineValueSnapshot: number | null;
  payoutMultiplierSnapshot: number;
  payoutConfigurationVersionSnapshot: number;
  submittedAt: string;
  marketStatus?: string | null;
  outcome?: string;
  settlementCredit?: number;
  netPoints?: number;
};

export type LeaderboardEntry = {
  memberId: string;
  userId: string;
  displayName: string;
  role: string;
  balance: number;
  predictionCount: number;
  settledPredictionCount: number;
  winCount: number;
  winRate: number;
  roi: number;
};

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
  predictionsLocked: boolean;
  coverImageUrl?: string | null;
  announcementTitle: string;
  defaultStake: number;
  minStake: number;
  maxStake: number;
  maxTotalStakePerEvent: number;
  role: string;
  memberCount: number;
  inviteCount: number;
};

export type DiscoverPool = {
  id: string;
  name: string;
  coverImageUrl?: string | null;
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
  id?: string;
  code: string;
  poolId: string;
  createdAt?: string;
  createdByUserId?: string;
  revokedAt?: string | null;
  revokedByUserId?: string | null;
  isRevoked?: boolean;
};

export type PoolMessageKind = "Chat" | "Announcement";

export type PoolMessage = {
  id: string;
  poolId: string;
  authorMemberId: string;
  authorDisplayName: string;
  authorAvatarUrl?: string | null;
  authorRole: string;
  kind: PoolMessageKind;
  announcementSlot?: number | null;
  title: string;
  bodyMarkdown: string;
  createdAt: string;
  editedAt?: string | null;
};

export type TournamentEvent = {
  id: string;
  tournamentId: string;
  homeParticipant: string;
  awayParticipant: string;
  homeParticipantCode?: string | null;
  awayParticipantCode?: string | null;
  startsAt: string;
  status: string;
  managementMode: string;
  provider: string;
  isTestData: boolean;
  firstHalfHomeScore: number | null;
  firstHalfAwayScore: number | null;
  fullTimeHomeScore: number | null;
  fullTimeAwayScore: number | null;
  resultRecordedAt: string | null;
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
  eventName?: string | null;
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
  avatarUrl?: string | null;
  role: string;
  balance: number;
  winLoss: number;
  predictionCount: number;
  settledPredictionCount: number;
  winCount: number;
  winRate: number;
  roi: number;
};

export type AutoPickEligibleEvent = {
  eventId: string;
  eventName: string;
  marketId: string;
  marketType: string;
  marketPeriod: string;
};

export type AutoPickSkippedEvent = {
  eventId: string;
  eventName: string;
  reason: string;
};

export type AutoPickPreview = {
  stake: number;
  eligibleEventCount: number;
  skippedEventCount: number;
  totalStake: number;
  currentBalance: number;
  balanceAfterAutoPick: number;
  hasEnoughBalance: boolean;
  eligibleEvents: AutoPickEligibleEvent[];
  skippedEvents: AutoPickSkippedEvent[];
};

export type AutoPickSubmission = {
  stake: number;
  createdCount: number;
  skippedCount: number;
  totalStake: number;
  currentBalanceBefore: number;
  balanceAfter: number;
  createdPredictions: Array<{
    predictionId: string;
    eventId: string;
    eventName: string;
    marketId: string;
    marketType: string;
    marketPeriod: string;
    selectedOption: string;
    stake: number;
    submittedAt: string;
  }>;
  skippedEvents: AutoPickSkippedEvent[];
};

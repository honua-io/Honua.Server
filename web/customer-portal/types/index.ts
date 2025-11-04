// Build Types
export type BuildStatus =
  | 'pending'
  | 'queued'
  | 'building'
  | 'success'
  | 'failed'
  | 'cancelled';

export type BuildTier = 'basic' | 'standard' | 'premium' | 'enterprise';

export type Architecture = 'amd64' | 'arm64' | 'multi';

export type RegistryType = 'ecr' | 'acr' | 'gcr' | 'ghcr' | 'dockerhub';

export interface Build {
  id: string;
  configurationName: string;
  status: BuildStatus;
  tier: BuildTier;
  architecture: Architecture;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  durationSeconds?: number;
  cacheHit: boolean;
  imageSizeMb?: number;
  downloadUrl?: string;
  errorMessage?: string;
  buildLogUrl?: string;
  tags: string[];
  metadata: {
    frameworks: string[];
    runtime?: string;
    customizations: string[];
  };
}

// Dashboard Types
export interface DashboardStats {
  totalBuilds: number;
  successRate: number;
  cacheHitRate: number;
  timeSavedMinutes: number;
  buildsThisMonth: number;
  avgBuildTimeSeconds: number;
  totalStorageGb: number;
}

export interface RecentBuild extends Pick<Build,
  'id' | 'configurationName' | 'status' | 'createdAt' | 'cacheHit'
> {}

// Intake Types
export type MessageRole = 'user' | 'assistant' | 'system';

export interface Message {
  id?: string;
  role: MessageRole;
  content: string;
  timestamp?: string;
  metadata?: {
    costEstimate?: CostEstimate;
    buildPreview?: BuildConfig;
  };
}

export interface Conversation {
  id: string;
  createdAt: string;
  updatedAt: string;
  messages: Message[];
  buildConfig?: BuildConfig;
  buildReady: boolean;
}

export interface BuildConfig {
  name: string;
  tier: BuildTier;
  architecture: Architecture;
  frameworks: string[];
  runtime?: string;
  customizations: string[];
  estimatedSizeMb: number;
  estimatedBuildTime: string;
  tags: string[];
}

export interface CostEstimate {
  buildCost: number;
  storageCostPerMonth: number;
  totalFirstMonth: number;
  breakdown: {
    item: string;
    cost: number;
    description: string;
  }[];
}

// License Types
export type LicenseTier = 'trial' | 'starter' | 'professional' | 'enterprise';

export interface License {
  id: string;
  tier: LicenseTier;
  organizationName: string;
  expiresAt: string;
  isActive: boolean;
  features: {
    maxBuildsPerMonth: number;
    maxRegistries: number;
    maxStorageGb: number;
    advancedFrameworks: boolean;
    prioritySupport: boolean;
    customizations: boolean;
    multiArch: boolean;
  };
  usage: {
    buildsThisMonth: number;
    registriesProvisioned: number;
    storageUsedGb: number;
  };
  billing: {
    nextBillingDate: string;
    monthlyPrice: number;
    billingEmail: string;
  };
}

// Registry Types
export interface Registry {
  id: string;
  type: RegistryType;
  name: string;
  url: string;
  username: string;
  region?: string;
  provisionedAt: string;
  lastUsed?: string;
  status: 'active' | 'expired' | 'revoked';
  credentials: {
    username: string;
    password: string;
    token?: string;
  };
  connectionInstructions: string;
  buildsCount: number;
}

// API Response Types
export interface ApiResponse<T> {
  data: T;
  success: boolean;
  error?: string;
  timestamp: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
}

// Filter and Sort Types
export interface BuildFilters {
  status?: BuildStatus[];
  tier?: BuildTier[];
  architecture?: Architecture[];
  dateFrom?: string;
  dateTo?: string;
  cacheHit?: boolean;
  search?: string;
}

export interface SortOptions {
  field: string;
  direction: 'asc' | 'desc';
}

// WebSocket Types
export interface WebSocketMessage {
  type: 'build_update' | 'build_complete' | 'build_failed' | 'queue_update';
  payload: any;
  timestamp: string;
}

export interface BuildUpdate {
  buildId: string;
  status: BuildStatus;
  progress?: number;
  message?: string;
}

// User Types
export interface User {
  id: string;
  email: string;
  name: string;
  organizationId: string;
  role: 'admin' | 'member' | 'viewer';
  avatarUrl?: string;
}

// Queue Types
export interface QueueStats {
  position: number;
  estimatedWaitTime: string;
  queueLength: number;
  activeBuilds: number;
}

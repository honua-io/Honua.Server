import type {
  ApiResponse,
  Build,
  BuildConfig,
  BuildFilters,
  Conversation,
  CostEstimate,
  DashboardStats,
  License,
  Message,
  PaginatedResponse,
  QueueStats,
  Registry,
  SortOptions,
} from '@/types';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

class ApiClient {
  private baseUrl: string;

  constructor(baseUrl: string = API_BASE_URL) {
    this.baseUrl = baseUrl;
  }

  private async request<T>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<T> {
    const url = `${this.baseUrl}${endpoint}`;

    const headers = {
      'Content-Type': 'application/json',
      ...options.headers,
    };

    try {
      const response = await fetch(url, {
        ...options,
        headers,
        credentials: 'include',
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({
          error: `HTTP ${response.status}: ${response.statusText}`,
        }));
        throw new Error(error.error || `Request failed with status ${response.status}`);
      }

      return await response.json();
    } catch (error) {
      console.error(`API request failed: ${endpoint}`, error);
      throw error;
    }
  }

  // Dashboard APIs
  async getDashboardStats(): Promise<DashboardStats> {
    const response = await this.request<ApiResponse<DashboardStats>>(
      '/api/dashboard/stats'
    );
    return response.data;
  }

  async getRecentBuilds(limit: number = 10): Promise<Build[]> {
    const response = await this.request<ApiResponse<Build[]>>(
      `/api/builds/recent?limit=${limit}`
    );
    return response.data;
  }

  // Build APIs
  async getBuilds(
    filters?: BuildFilters,
    sort?: SortOptions,
    page: number = 1,
    pageSize: number = 20
  ): Promise<PaginatedResponse<Build>> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });

    if (filters) {
      if (filters.status) params.append('status', filters.status.join(','));
      if (filters.tier) params.append('tier', filters.tier.join(','));
      if (filters.architecture) params.append('architecture', filters.architecture.join(','));
      if (filters.dateFrom) params.append('dateFrom', filters.dateFrom);
      if (filters.dateTo) params.append('dateTo', filters.dateTo);
      if (filters.cacheHit !== undefined) params.append('cacheHit', filters.cacheHit.toString());
      if (filters.search) params.append('search', filters.search);
    }

    if (sort) {
      params.append('sortBy', sort.field);
      params.append('sortDir', sort.direction);
    }

    const response = await this.request<ApiResponse<PaginatedResponse<Build>>>(
      `/api/builds?${params.toString()}`
    );
    return response.data;
  }

  async getBuild(buildId: string): Promise<Build> {
    const response = await this.request<ApiResponse<Build>>(
      `/api/builds/${buildId}`
    );
    return response.data;
  }

  async downloadBuild(buildId: string): Promise<Blob> {
    const url = `${this.baseUrl}/api/builds/${buildId}/download`;
    const response = await fetch(url, { credentials: 'include' });

    if (!response.ok) {
      throw new Error(`Download failed with status ${response.status}`);
    }

    return await response.blob();
  }

  async cancelBuild(buildId: string): Promise<void> {
    await this.request<ApiResponse<void>>(`/api/builds/${buildId}/cancel`, {
      method: 'POST',
    });
  }

  async retryBuild(buildId: string): Promise<Build> {
    const response = await this.request<ApiResponse<Build>>(
      `/api/builds/${buildId}/retry`,
      { method: 'POST' }
    );
    return response.data;
  }

  async getQueueStats(): Promise<QueueStats> {
    const response = await this.request<ApiResponse<QueueStats>>(
      '/api/builds/queue/stats'
    );
    return response.data;
  }

  // Intake/Conversation APIs
  async createConversation(): Promise<Conversation> {
    const response = await this.request<ApiResponse<Conversation>>(
      '/api/intake/conversations',
      { method: 'POST' }
    );
    return response.data;
  }

  async getConversation(conversationId: string): Promise<Conversation> {
    const response = await this.request<ApiResponse<Conversation>>(
      `/api/intake/conversations/${conversationId}`
    );
    return response.data;
  }

  async sendIntakeMessage(
    conversationId: string,
    message: string
  ): Promise<{
    reply: string;
    buildReady: boolean;
    buildConfig?: BuildConfig;
    costEstimate?: CostEstimate;
  }> {
    const response = await this.request<ApiResponse<any>>(
      `/api/intake/conversations/${conversationId}/messages`,
      {
        method: 'POST',
        body: JSON.stringify({ message }),
      }
    );
    return response.data;
  }

  async getCostEstimate(buildConfig: BuildConfig): Promise<CostEstimate> {
    const response = await this.request<ApiResponse<CostEstimate>>(
      '/api/intake/estimate',
      {
        method: 'POST',
        body: JSON.stringify(buildConfig),
      }
    );
    return response.data;
  }

  async startBuildFromConversation(conversationId: string): Promise<Build> {
    const response = await this.request<ApiResponse<Build>>(
      `/api/intake/conversations/${conversationId}/build`,
      { method: 'POST' }
    );
    return response.data;
  }

  // License APIs
  async getLicense(): Promise<License> {
    const response = await this.request<ApiResponse<License>>(
      '/api/license'
    );
    return response.data;
  }

  async getUpgradeOptions(): Promise<{
    tier: string;
    monthlyPrice: number;
    features: string[];
    savings?: string;
  }[]> {
    const response = await this.request<ApiResponse<any>>(
      '/api/license/upgrade-options'
    );
    return response.data;
  }

  // Registry APIs
  async getRegistries(): Promise<Registry[]> {
    const response = await this.request<ApiResponse<Registry[]>>(
      '/api/registries'
    );
    return response.data;
  }

  async getRegistry(registryId: string): Promise<Registry> {
    const response = await this.request<ApiResponse<Registry>>(
      `/api/registries/${registryId}`
    );
    return response.data;
  }

  async provisionRegistry(type: string, name: string, region?: string): Promise<Registry> {
    const response = await this.request<ApiResponse<Registry>>(
      '/api/registries',
      {
        method: 'POST',
        body: JSON.stringify({ type, name, region }),
      }
    );
    return response.data;
  }

  async revokeRegistry(registryId: string): Promise<void> {
    await this.request<ApiResponse<void>>(
      `/api/registries/${registryId}/revoke`,
      { method: 'POST' }
    );
  }

  async rotateRegistryCredentials(registryId: string): Promise<Registry> {
    const response = await this.request<ApiResponse<Registry>>(
      `/api/registries/${registryId}/rotate`,
      { method: 'POST' }
    );
    return response.data;
  }

  async testRegistryConnection(registryId: string): Promise<{ success: boolean; message: string }> {
    const response = await this.request<ApiResponse<{ success: boolean; message: string }>>(
      `/api/registries/${registryId}/test`,
      { method: 'POST' }
    );
    return response.data;
  }
}

export const api = new ApiClient();
export default api;

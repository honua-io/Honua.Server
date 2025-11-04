'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { BuildStatusBadge } from '@/components/BuildStatusBadge';
import { Skeleton } from '@/components/ui/skeleton';
import { useRouter, useSearchParams } from 'next/navigation';
import { useToast } from '@/components/ui/use-toast';
import type { BuildFilters, BuildStatus, BuildTier, Architecture } from '@/types';
import {
  formatDate,
  formatDuration,
  formatBytes,
  debounce,
} from '@/lib/utils';
import {
  Download,
  RefreshCw,
  Search,
  Filter,
  Plus,
  ExternalLink,
  XCircle,
} from 'lucide-react';

export default function BuildsPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { toast } = useToast();

  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState<BuildFilters>({});
  const [searchQuery, setSearchQuery] = useState('');

  const {
    data: buildsData,
    isLoading,
    refetch,
  } = useQuery({
    queryKey: ['builds', page, filters],
    queryFn: () => api.getBuilds(filters, undefined, page, 20),
    refetchInterval: 5000, // Poll every 5 seconds for status updates
  });

  const handleSearch = debounce((query: string) => {
    setFilters((prev) => ({ ...prev, search: query }));
    setPage(1);
  }, 500);

  const handleDownload = async (buildId: string) => {
    try {
      const blob = await api.downloadBuild(buildId);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `build-${buildId}.tar.gz`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);

      toast({
        title: 'Download started',
        description: 'Your build artifact is being downloaded.',
      });
    } catch (error) {
      toast({
        title: 'Download failed',
        description: 'Failed to download build artifact.',
        variant: 'destructive',
      });
    }
  };

  const handleCancel = async (buildId: string) => {
    try {
      await api.cancelBuild(buildId);
      toast({
        title: 'Build cancelled',
        description: 'The build has been cancelled successfully.',
      });
      refetch();
    } catch (error) {
      toast({
        title: 'Cancel failed',
        description: 'Failed to cancel the build.',
        variant: 'destructive',
      });
    }
  };

  const handleRetry = async (buildId: string) => {
    try {
      const newBuild = await api.retryBuild(buildId);
      toast({
        title: 'Build retried',
        description: `New build ${newBuild.id} has been queued.`,
      });
      refetch();
    } catch (error) {
      toast({
        title: 'Retry failed',
        description: 'Failed to retry the build.',
        variant: 'destructive',
      });
    }
  };

  return (
    <div className="container mx-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Build History</h1>
          <p className="text-muted-foreground mt-1">
            View and manage your container builds
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => refetch()}>
            <RefreshCw className="h-4 w-4 mr-2" />
            Refresh
          </Button>
          <Button onClick={() => router.push('/intake')}>
            <Plus className="h-4 w-4 mr-2" />
            New Build
          </Button>
        </div>
      </div>

      {/* Search and Filters */}
      <Card className="p-4">
        <div className="flex flex-col md:flex-row gap-4">
          <div className="flex-1 relative">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
              placeholder="Search builds..."
              value={searchQuery}
              onChange={(e) => {
                setSearchQuery(e.target.value);
                handleSearch(e.target.value);
              }}
              className="pl-10"
            />
          </div>
          <div className="flex gap-2">
            <Button variant="outline" size="sm">
              <Filter className="h-4 w-4 mr-2" />
              Filters
            </Button>
          </div>
        </div>
      </Card>

      {/* Builds Table */}
      <Card>
        {isLoading ? (
          <div className="p-6 space-y-4">
            {[...Array(5)].map((_, i) => (
              <div key={i} className="flex items-center gap-4">
                <Skeleton className="h-12 flex-1" />
              </div>
            ))}
          </div>
        ) : buildsData && buildsData.items.length > 0 ? (
          <>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Configuration</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Tier</TableHead>
                  <TableHead>Architecture</TableHead>
                  <TableHead>Duration</TableHead>
                  <TableHead>Size</TableHead>
                  <TableHead>Created</TableHead>
                  <TableHead>Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {buildsData.items.map((build) => (
                  <TableRow key={build.id}>
                    <TableCell className="font-medium">
                      <div>
                        <p>{build.configurationName}</p>
                        {build.cacheHit && (
                          <span className="text-xs text-green-600">
                            Cache Hit
                          </span>
                        )}
                      </div>
                    </TableCell>
                    <TableCell>
                      <BuildStatusBadge status={build.status} />
                    </TableCell>
                    <TableCell className="capitalize">{build.tier}</TableCell>
                    <TableCell>{build.architecture}</TableCell>
                    <TableCell>
                      {build.durationSeconds
                        ? formatDuration(build.durationSeconds)
                        : '-'}
                    </TableCell>
                    <TableCell>
                      {build.imageSizeMb
                        ? formatBytes(build.imageSizeMb * 1024 * 1024)
                        : '-'}
                    </TableCell>
                    <TableCell>{formatDate(build.createdAt)}</TableCell>
                    <TableCell>
                      <div className="flex gap-1">
                        {build.status === 'success' && (
                          <>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => handleDownload(build.id)}
                            >
                              <Download className="h-4 w-4" />
                            </Button>
                            {build.buildLogUrl && (
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() =>
                                  window.open(build.buildLogUrl, '_blank')
                                }
                              >
                                <ExternalLink className="h-4 w-4" />
                              </Button>
                            )}
                          </>
                        )}
                        {build.status === 'failed' && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleRetry(build.id)}
                          >
                            <RefreshCw className="h-4 w-4" />
                          </Button>
                        )}
                        {(build.status === 'queued' ||
                          build.status === 'building') && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleCancel(build.id)}
                          >
                            <XCircle className="h-4 w-4" />
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            {/* Pagination */}
            {buildsData.hasMore && (
              <div className="p-4 border-t flex justify-between items-center">
                <p className="text-sm text-muted-foreground">
                  Showing {(page - 1) * 20 + 1} to{' '}
                  {Math.min(page * 20, buildsData.total)} of {buildsData.total}{' '}
                  builds
                </p>
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page === 1}
                  >
                    Previous
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage((p) => p + 1)}
                    disabled={!buildsData.hasMore}
                  >
                    Next
                  </Button>
                </div>
              </div>
            )}
          </>
        ) : (
          <div className="p-12 text-center">
            <p className="text-lg font-medium mb-2">No builds found</p>
            <p className="text-muted-foreground mb-4">
              {searchQuery
                ? 'Try adjusting your search or filters'
                : 'Start your first build to see it here'}
            </p>
            <Button onClick={() => router.push('/intake')}>
              <Plus className="h-4 w-4 mr-2" />
              New Build
            </Button>
          </div>
        )}
      </Card>
    </div>
  );
}

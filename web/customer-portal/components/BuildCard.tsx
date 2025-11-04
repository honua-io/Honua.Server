import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { BuildStatusBadge } from './BuildStatusBadge';
import { Button } from '@/components/ui/button';
import type { Build } from '@/types';
import { formatRelativeTime, formatDuration, formatBytes } from '@/lib/utils';
import { Download, Clock, HardDrive, Zap } from 'lucide-react';

interface BuildCardProps {
  build: Build;
  onDownload?: (buildId: string) => void;
  onViewDetails?: (buildId: string) => void;
}

export function BuildCard({ build, onDownload, onViewDetails }: BuildCardProps) {
  return (
    <Card className="hover:shadow-md transition-shadow">
      <CardHeader className="pb-3">
        <div className="flex items-start justify-between">
          <div>
            <CardTitle className="text-lg">{build.configurationName}</CardTitle>
            <p className="text-sm text-muted-foreground mt-1">
              {formatRelativeTime(build.createdAt)}
            </p>
          </div>
          <BuildStatusBadge status={build.status} />
        </div>
      </CardHeader>
      <CardContent>
        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-3 text-sm">
            <div className="flex items-center gap-2">
              <span className="text-muted-foreground">Tier:</span>
              <span className="font-medium capitalize">{build.tier}</span>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-muted-foreground">Arch:</span>
              <span className="font-medium">{build.architecture}</span>
            </div>
          </div>

          {build.durationSeconds && (
            <div className="flex items-center gap-2 text-sm">
              <Clock className="h-4 w-4 text-muted-foreground" />
              <span>{formatDuration(build.durationSeconds)}</span>
            </div>
          )}

          {build.imageSizeMb && (
            <div className="flex items-center gap-2 text-sm">
              <HardDrive className="h-4 w-4 text-muted-foreground" />
              <span>{formatBytes(build.imageSizeMb * 1024 * 1024)}</span>
            </div>
          )}

          {build.cacheHit && (
            <div className="flex items-center gap-2 text-sm text-green-600 dark:text-green-400">
              <Zap className="h-4 w-4" />
              <span>Cache Hit</span>
            </div>
          )}

          {build.metadata.frameworks.length > 0 && (
            <div className="flex flex-wrap gap-1 mt-2">
              {build.metadata.frameworks.map((framework) => (
                <span
                  key={framework}
                  className="inline-flex items-center px-2 py-0.5 rounded text-xs bg-secondary text-secondary-foreground"
                >
                  {framework}
                </span>
              ))}
            </div>
          )}

          <div className="flex gap-2 mt-4 pt-3 border-t">
            <Button
              variant="outline"
              size="sm"
              className="flex-1"
              onClick={() => onViewDetails?.(build.id)}
            >
              Details
            </Button>
            {build.status === 'success' && onDownload && (
              <Button
                variant="default"
                size="sm"
                className="flex-1"
                onClick={() => onDownload(build.id)}
              >
                <Download className="h-4 w-4 mr-2" />
                Download
              </Button>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import type { Registry } from '@/types';
import { formatRelativeTime } from '@/lib/utils';
import {
  Copy,
  ExternalLink,
  RotateCw,
  Trash2,
  CheckCircle2,
  XCircle,
  Eye,
  EyeOff,
} from 'lucide-react';
import { useState } from 'react';
import { useToast } from '@/components/ui/use-toast';

interface RegistryCardProps {
  registry: Registry;
  onRevoke?: (id: string) => void;
  onRotate?: (id: string) => void;
  onTest?: (id: string) => void;
}

export function RegistryCard({
  registry,
  onRevoke,
  onRotate,
  onTest,
}: RegistryCardProps) {
  const [showPassword, setShowPassword] = useState(false);
  const { toast } = useToast();

  const copyToClipboard = (text: string, label: string) => {
    navigator.clipboard.writeText(text);
    toast({
      title: 'Copied to clipboard',
      description: `${label} has been copied to your clipboard.`,
    });
  };

  const getRegistryIcon = (type: string) => {
    switch (type) {
      case 'ecr':
        return '🪣'; // AWS icon placeholder
      case 'acr':
        return '☁️'; // Azure icon placeholder
      case 'gcr':
        return '🔵'; // GCP icon placeholder
      case 'ghcr':
        return '🐙'; // GitHub icon placeholder
      case 'dockerhub':
        return '🐳'; // Docker icon placeholder
      default:
        return '📦';
    }
  };

  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex items-start justify-between">
          <div className="flex items-center gap-3">
            <span className="text-2xl">{getRegistryIcon(registry.type)}</span>
            <div>
              <CardTitle className="text-lg">{registry.name}</CardTitle>
              <p className="text-sm text-muted-foreground mt-1">
                {registry.type.toUpperCase()}
                {registry.region && ` • ${registry.region}`}
              </p>
            </div>
          </div>
          <Badge
            variant={registry.status === 'active' ? 'outline' : 'destructive'}
          >
            {registry.status === 'active' ? (
              <CheckCircle2 className="h-3 w-3 mr-1" />
            ) : (
              <XCircle className="h-3 w-3 mr-1" />
            )}
            {registry.status}
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Registry URL */}
        <div>
          <label className="text-xs font-medium text-muted-foreground">
            Registry URL
          </label>
          <div className="flex items-center gap-2 mt-1">
            <code className="flex-1 px-3 py-2 bg-muted rounded text-sm font-mono">
              {registry.url}
            </code>
            <Button
              variant="outline"
              size="icon"
              onClick={() => copyToClipboard(registry.url, 'Registry URL')}
            >
              <Copy className="h-4 w-4" />
            </Button>
          </div>
        </div>

        {/* Credentials */}
        <div>
          <label className="text-xs font-medium text-muted-foreground">
            Username
          </label>
          <div className="flex items-center gap-2 mt-1">
            <code className="flex-1 px-3 py-2 bg-muted rounded text-sm font-mono">
              {registry.credentials.username}
            </code>
            <Button
              variant="outline"
              size="icon"
              onClick={() =>
                copyToClipboard(registry.credentials.username, 'Username')
              }
            >
              <Copy className="h-4 w-4" />
            </Button>
          </div>
        </div>

        <div>
          <label className="text-xs font-medium text-muted-foreground">
            Password
          </label>
          <div className="flex items-center gap-2 mt-1">
            <code className="flex-1 px-3 py-2 bg-muted rounded text-sm font-mono">
              {showPassword
                ? registry.credentials.password
                : '••••••••••••••••'}
            </code>
            <Button
              variant="outline"
              size="icon"
              onClick={() => setShowPassword(!showPassword)}
            >
              {showPassword ? (
                <EyeOff className="h-4 w-4" />
              ) : (
                <Eye className="h-4 w-4" />
              )}
            </Button>
            <Button
              variant="outline"
              size="icon"
              onClick={() =>
                copyToClipboard(registry.credentials.password, 'Password')
              }
            >
              <Copy className="h-4 w-4" />
            </Button>
          </div>
        </div>

        {/* Connection Instructions */}
        <div className="pt-3 border-t">
          <details className="group">
            <summary className="cursor-pointer text-sm font-medium flex items-center gap-2">
              <ExternalLink className="h-4 w-4" />
              Connection Instructions
            </summary>
            <pre className="mt-2 p-3 bg-muted rounded text-xs overflow-x-auto">
              {registry.connectionInstructions}
            </pre>
          </details>
        </div>

        {/* Stats */}
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>{registry.buildsCount} builds</span>
          <span>
            Provisioned {formatRelativeTime(registry.provisionedAt)}
          </span>
        </div>

        {/* Actions */}
        <div className="flex gap-2 pt-3 border-t">
          <Button
            variant="outline"
            size="sm"
            className="flex-1"
            onClick={() => onTest?.(registry.id)}
          >
            Test Connection
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => onRotate?.(registry.id)}
          >
            <RotateCw className="h-4 w-4" />
          </Button>
          <Button
            variant="destructive"
            size="sm"
            onClick={() => onRevoke?.(registry.id)}
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

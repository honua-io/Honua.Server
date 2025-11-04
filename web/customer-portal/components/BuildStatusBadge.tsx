import { Badge } from '@/components/ui/badge';
import type { BuildStatus } from '@/types';
import {
  CheckCircle2,
  Clock,
  Loader2,
  XCircle,
  Ban,
  HelpCircle,
} from 'lucide-react';

interface BuildStatusBadgeProps {
  status: BuildStatus;
  className?: string;
}

const statusConfig: Record<
  BuildStatus,
  { label: string; variant: 'default' | 'secondary' | 'destructive' | 'outline'; icon: any }
> = {
  pending: {
    label: 'Pending',
    variant: 'secondary',
    icon: Clock,
  },
  queued: {
    label: 'Queued',
    variant: 'secondary',
    icon: Clock,
  },
  building: {
    label: 'Building',
    variant: 'default',
    icon: Loader2,
  },
  success: {
    label: 'Success',
    variant: 'outline',
    icon: CheckCircle2,
  },
  failed: {
    label: 'Failed',
    variant: 'destructive',
    icon: XCircle,
  },
  cancelled: {
    label: 'Cancelled',
    variant: 'secondary',
    icon: Ban,
  },
};

export function BuildStatusBadge({ status, className }: BuildStatusBadgeProps) {
  const config = statusConfig[status] || {
    label: status,
    variant: 'secondary' as const,
    icon: HelpCircle,
  };

  const Icon = config.icon;

  return (
    <Badge variant={config.variant} className={className}>
      <Icon className={`mr-1 h-3 w-3 ${status === 'building' ? 'animate-spin' : ''}`} />
      {config.label}
    </Badge>
  );
}

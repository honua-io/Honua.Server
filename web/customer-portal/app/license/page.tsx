'use client';

import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Progress } from '@/components/ui/progress';
import { useToast } from '@/components/ui/use-toast';
import {
  CheckCircle2,
  XCircle,
  Clock,
  TrendingUp,
  Package,
  Database,
  Zap,
  Crown,
  Shield,
  Sparkles,
} from 'lucide-react';
import { calculateDaysUntil, formatCurrency } from '@/lib/utils';

export default function LicensePage() {
  const { toast } = useToast();

  const { data: license, isLoading } = useQuery({
    queryKey: ['license'],
    queryFn: () => api.getLicense(),
  });

  const { data: upgradeOptions } = useQuery({
    queryKey: ['upgrade-options'],
    queryFn: () => api.getUpgradeOptions(),
  });

  const daysUntilExpiry = license
    ? calculateDaysUntil(license.expiresAt)
    : null;

  const usagePercentage = (used: number, max: number) =>
    max === 0 ? 0 : Math.min((used / max) * 100, 100);

  return (
    <div className="container mx-auto p-6 space-y-8">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold">License Management</h1>
        <p className="text-muted-foreground mt-1">
          View your license details and usage quotas
        </p>
      </div>

      {isLoading ? (
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <Skeleton className="h-8 w-48" />
            </CardHeader>
            <CardContent>
              <Skeleton className="h-24 w-full" />
            </CardContent>
          </Card>
        </div>
      ) : license ? (
        <>
          {/* License Overview */}
          <Card>
            <CardHeader>
              <div className="flex items-start justify-between">
                <div>
                  <CardTitle className="text-2xl flex items-center gap-2">
                    <Crown className="h-6 w-6 text-yellow-600" />
                    {license.tier.charAt(0).toUpperCase() +
                      license.tier.slice(1)}{' '}
                    Plan
                  </CardTitle>
                  <p className="text-muted-foreground mt-2">
                    {license.organizationName}
                  </p>
                </div>
                <Badge
                  variant={license.isActive ? 'outline' : 'destructive'}
                  className="text-base px-4 py-1"
                >
                  {license.isActive ? (
                    <>
                      <CheckCircle2 className="h-4 w-4 mr-2" />
                      Active
                    </>
                  ) : (
                    <>
                      <XCircle className="h-4 w-4 mr-2" />
                      Inactive
                    </>
                  )}
                </Badge>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div className="flex items-center gap-3 p-4 rounded-lg bg-muted">
                  <Clock className="h-5 w-5 text-muted-foreground" />
                  <div>
                    <p className="text-sm text-muted-foreground">
                      Expires In
                    </p>
                    <p className="text-xl font-bold">
                      {daysUntilExpiry !== null && daysUntilExpiry > 0
                        ? `${daysUntilExpiry} days`
                        : 'Expired'}
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-3 p-4 rounded-lg bg-muted">
                  <TrendingUp className="h-5 w-5 text-muted-foreground" />
                  <div>
                    <p className="text-sm text-muted-foreground">
                      Monthly Price
                    </p>
                    <p className="text-xl font-bold">
                      {formatCurrency(license.billing.monthlyPrice)}
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-3 p-4 rounded-lg bg-muted">
                  <Clock className="h-5 w-5 text-muted-foreground" />
                  <div>
                    <p className="text-sm text-muted-foreground">
                      Next Billing
                    </p>
                    <p className="text-lg font-bold">
                      {new Date(
                        license.billing.nextBillingDate
                      ).toLocaleDateString()}
                    </p>
                  </div>
                </div>
              </div>

              {daysUntilExpiry !== null &&
                daysUntilExpiry <= 30 &&
                daysUntilExpiry > 0 && (
                  <div className="flex items-start gap-2 p-4 rounded-lg bg-yellow-50 dark:bg-yellow-950 border border-yellow-200 dark:border-yellow-800">
                    <Clock className="h-5 w-5 text-yellow-600 mt-0.5" />
                    <div>
                      <p className="font-medium text-yellow-900 dark:text-yellow-100">
                        License Expiring Soon
                      </p>
                      <p className="text-sm text-yellow-800 dark:text-yellow-200 mt-1">
                        Your license will expire in {daysUntilExpiry} days.
                        Consider renewing to avoid service interruption.
                      </p>
                    </div>
                  </div>
                )}
            </CardContent>
          </Card>

          {/* Usage Quotas */}
          <Card>
            <CardHeader>
              <CardTitle>Usage & Quotas</CardTitle>
            </CardHeader>
            <CardContent className="space-y-6">
              {/* Builds */}
              <div>
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-2">
                    <Package className="h-4 w-4 text-muted-foreground" />
                    <span className="font-medium">Builds This Month</span>
                  </div>
                  <span className="text-sm text-muted-foreground">
                    {license.usage.buildsThisMonth} /{' '}
                    {license.features.maxBuildsPerMonth}
                  </span>
                </div>
                <Progress
                  value={usagePercentage(
                    license.usage.buildsThisMonth,
                    license.features.maxBuildsPerMonth
                  )}
                  className="h-2"
                />
              </div>

              {/* Storage */}
              <div>
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-2">
                    <Database className="h-4 w-4 text-muted-foreground" />
                    <span className="font-medium">Storage</span>
                  </div>
                  <span className="text-sm text-muted-foreground">
                    {license.usage.storageUsedGb.toFixed(2)} GB /{' '}
                    {license.features.maxStorageGb} GB
                  </span>
                </div>
                <Progress
                  value={usagePercentage(
                    license.usage.storageUsedGb,
                    license.features.maxStorageGb
                  )}
                  className="h-2"
                />
              </div>

              {/* Registries */}
              <div>
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-2">
                    <Shield className="h-4 w-4 text-muted-foreground" />
                    <span className="font-medium">Registry Credentials</span>
                  </div>
                  <span className="text-sm text-muted-foreground">
                    {license.usage.registriesProvisioned} /{' '}
                    {license.features.maxRegistries}
                  </span>
                </div>
                <Progress
                  value={usagePercentage(
                    license.usage.registriesProvisioned,
                    license.features.maxRegistries
                  )}
                  className="h-2"
                />
              </div>
            </CardContent>
          </Card>

          {/* Features */}
          <Card>
            <CardHeader>
              <CardTitle>Plan Features</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <Feature
                  enabled={license.features.advancedFrameworks}
                  label="Advanced Frameworks"
                />
                <Feature
                  enabled={license.features.customizations}
                  label="Custom Configurations"
                />
                <Feature
                  enabled={license.features.multiArch}
                  label="Multi-Architecture Builds"
                />
                <Feature
                  enabled={license.features.prioritySupport}
                  label="Priority Support"
                />
              </div>
            </CardContent>
          </Card>

          {/* Upgrade Options */}
          {upgradeOptions && upgradeOptions.length > 0 && (
            <div>
              <h2 className="text-2xl font-bold mb-4">Upgrade Options</h2>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                {upgradeOptions.map((option) => (
                  <Card key={option.tier} className="relative overflow-hidden">
                    {option.savings && (
                      <div className="absolute top-4 right-4">
                        <Badge variant="destructive">{option.savings}</Badge>
                      </div>
                    )}
                    <CardHeader>
                      <CardTitle className="flex items-center gap-2">
                        <Sparkles className="h-5 w-5" />
                        {option.tier}
                      </CardTitle>
                      <p className="text-3xl font-bold mt-4">
                        {formatCurrency(option.monthlyPrice)}
                        <span className="text-base font-normal text-muted-foreground">
                          /month
                        </span>
                      </p>
                    </CardHeader>
                    <CardContent>
                      <ul className="space-y-2 mb-6">
                        {option.features.map((feature, index) => (
                          <li
                            key={index}
                            className="flex items-start gap-2 text-sm"
                          >
                            <CheckCircle2 className="h-4 w-4 text-green-600 mt-0.5 flex-shrink-0" />
                            <span>{feature}</span>
                          </li>
                        ))}
                      </ul>
                      <Button className="w-full">Upgrade to {option.tier}</Button>
                    </CardContent>
                  </Card>
                ))}
              </div>
            </div>
          )}
        </>
      ) : (
        <Card>
          <CardContent className="py-12 text-center">
            <XCircle className="h-12 w-12 mx-auto text-destructive mb-4" />
            <p className="text-lg font-medium mb-2">No License Found</p>
            <p className="text-muted-foreground">
              Please contact support to activate your license.
            </p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function Feature({ enabled, label }: { enabled: boolean; label: string }) {
  return (
    <div className="flex items-center gap-2">
      {enabled ? (
        <CheckCircle2 className="h-5 w-5 text-green-600" />
      ) : (
        <XCircle className="h-5 w-5 text-muted-foreground" />
      )}
      <span className={enabled ? '' : 'text-muted-foreground'}>{label}</span>
    </div>
  );
}

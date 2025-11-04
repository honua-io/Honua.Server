'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { RegistryCard } from '@/components/RegistryCard';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import {
  Plus,
  Key,
  AlertCircle,
  CheckCircle2,
  Package,
} from 'lucide-react';
import type { RegistryType } from '@/types';

export default function RegistriesPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [isProvisioning, setIsProvisioning] = useState(false);

  const { data: registries, isLoading } = useQuery({
    queryKey: ['registries'],
    queryFn: () => api.getRegistries(),
  });

  const { data: license } = useQuery({
    queryKey: ['license'],
    queryFn: () => api.getLicense(),
  });

  const revokeMutation = useMutation({
    mutationFn: (registryId: string) => api.revokeRegistry(registryId),
    onSuccess: () => {
      toast({
        title: 'Registry revoked',
        description: 'The registry credentials have been revoked successfully.',
      });
      queryClient.invalidateQueries({ queryKey: ['registries'] });
    },
    onError: (error) => {
      toast({
        title: 'Revoke failed',
        description: error instanceof Error ? error.message : 'Failed to revoke registry',
        variant: 'destructive',
      });
    },
  });

  const rotateMutation = useMutation({
    mutationFn: (registryId: string) => api.rotateRegistryCredentials(registryId),
    onSuccess: () => {
      toast({
        title: 'Credentials rotated',
        description: 'New credentials have been generated successfully.',
      });
      queryClient.invalidateQueries({ queryKey: ['registries'] });
    },
    onError: (error) => {
      toast({
        title: 'Rotation failed',
        description: error instanceof Error ? error.message : 'Failed to rotate credentials',
        variant: 'destructive',
      });
    },
  });

  const testMutation = useMutation({
    mutationFn: (registryId: string) => api.testRegistryConnection(registryId),
    onSuccess: (data) => {
      toast({
        title: data.success ? 'Connection successful' : 'Connection failed',
        description: data.message,
        variant: data.success ? 'default' : 'destructive',
      });
    },
    onError: (error) => {
      toast({
        title: 'Test failed',
        description: error instanceof Error ? error.message : 'Failed to test connection',
        variant: 'destructive',
      });
    },
  });

  const canProvisionMore =
    license &&
    registries &&
    registries.filter((r) => r.status === 'active').length <
      license.features.maxRegistries;

  return (
    <div className="container mx-auto p-6 space-y-8">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <Key className="h-8 w-8" />
            Registry Credentials
          </h1>
          <p className="text-muted-foreground mt-1">
            Manage your container registry access credentials
          </p>
        </div>
        <Button
          onClick={() => setIsProvisioning(true)}
          disabled={!canProvisionMore}
        >
          <Plus className="h-4 w-4 mr-2" />
          Provision Registry
        </Button>
      </div>

      {/* Quota Alert */}
      {license && registries && (
        <Card className="border-primary/20">
          <CardContent className="py-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary/10">
                  <Package className="h-5 w-5 text-primary" />
                </div>
                <div>
                  <p className="font-medium">Registry Quota</p>
                  <p className="text-sm text-muted-foreground">
                    {registries.filter((r) => r.status === 'active').length} of{' '}
                    {license.features.maxRegistries} registries provisioned
                  </p>
                </div>
              </div>
              {!canProvisionMore && (
                <div className="flex items-center gap-2 text-yellow-600">
                  <AlertCircle className="h-5 w-5" />
                  <span className="text-sm font-medium">Quota Reached</span>
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Registries Grid */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {[...Array(4)].map((_, i) => (
            <Card key={i}>
              <CardContent className="p-6">
                <div className="space-y-4">
                  <Skeleton className="h-8 w-48" />
                  <Skeleton className="h-20 w-full" />
                  <Skeleton className="h-10 w-full" />
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : registries && registries.length > 0 ? (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {registries.map((registry) => (
            <RegistryCard
              key={registry.id}
              registry={registry}
              onRevoke={(id) => revokeMutation.mutate(id)}
              onRotate={(id) => rotateMutation.mutate(id)}
              onTest={(id) => testMutation.mutate(id)}
            />
          ))}
        </div>
      ) : (
        <Card>
          <CardContent className="py-12 text-center">
            <Key className="h-12 w-12 mx-auto text-muted-foreground mb-4" />
            <p className="text-lg font-medium mb-2">No Registries Yet</p>
            <p className="text-muted-foreground mb-4">
              Provision your first container registry to get started
            </p>
            <Button onClick={() => setIsProvisioning(true)}>
              <Plus className="h-4 w-4 mr-2" />
              Provision Registry
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Registry Types Info */}
      <Card>
        <CardContent className="p-6">
          <h3 className="font-semibold mb-4">Supported Registry Types</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <RegistryTypeCard
              icon="🪣"
              name="Amazon ECR"
              description="Elastic Container Registry"
            />
            <RegistryTypeCard
              icon="☁️"
              name="Azure ACR"
              description="Azure Container Registry"
            />
            <RegistryTypeCard
              icon="🔵"
              name="Google GCR"
              description="Google Container Registry"
            />
            <RegistryTypeCard
              icon="🐙"
              name="GitHub GHCR"
              description="GitHub Container Registry"
            />
          </div>
        </CardContent>
      </Card>

      {/* Security Notice */}
      <Card className="border-yellow-200 dark:border-yellow-800 bg-yellow-50 dark:bg-yellow-950">
        <CardContent className="py-4">
          <div className="flex items-start gap-3">
            <AlertCircle className="h-5 w-5 text-yellow-600 mt-0.5 flex-shrink-0" />
            <div>
              <p className="font-medium text-yellow-900 dark:text-yellow-100">
                Security Best Practices
              </p>
              <ul className="text-sm text-yellow-800 dark:text-yellow-200 mt-2 space-y-1 list-disc list-inside">
                <li>Rotate credentials regularly (every 90 days recommended)</li>
                <li>Never commit credentials to source control</li>
                <li>Use environment variables for credential storage</li>
                <li>Revoke credentials immediately if compromised</li>
              </ul>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

function RegistryTypeCard({
  icon,
  name,
  description,
}: {
  icon: string;
  name: string;
  description: string;
}) {
  return (
    <div className="flex items-center gap-3 p-3 rounded-lg border bg-card">
      <span className="text-2xl">{icon}</span>
      <div>
        <p className="font-medium text-sm">{name}</p>
        <p className="text-xs text-muted-foreground">{description}</p>
      </div>
    </div>
  );
}

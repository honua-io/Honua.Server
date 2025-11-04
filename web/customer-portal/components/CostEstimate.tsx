import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import type { CostEstimate as CostEstimateType } from '@/types';
import { formatCurrency } from '@/lib/utils';
import { DollarSign, TrendingUp } from 'lucide-react';

interface CostEstimateProps {
  estimate: CostEstimateType;
}

export function CostEstimate({ estimate }: CostEstimateProps) {
  return (
    <Card className="border-primary/20">
      <CardHeader className="pb-3">
        <CardTitle className="text-base flex items-center gap-2">
          <DollarSign className="h-4 w-4" />
          Cost Estimate
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <p className="text-sm text-muted-foreground">Build Cost</p>
              <p className="text-lg font-bold">{formatCurrency(estimate.buildCost)}</p>
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Storage/Month</p>
              <p className="text-lg font-bold">{formatCurrency(estimate.storageCostPerMonth)}</p>
            </div>
          </div>

          <div className="pt-3 border-t">
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium">Total First Month</span>
              <span className="text-xl font-bold text-primary">
                {formatCurrency(estimate.totalFirstMonth)}
              </span>
            </div>
          </div>

          {estimate.breakdown && estimate.breakdown.length > 0 && (
            <div className="pt-3 border-t space-y-2">
              <p className="text-xs font-medium text-muted-foreground">Breakdown</p>
              {estimate.breakdown.map((item, index) => (
                <div key={index} className="flex justify-between text-sm">
                  <div>
                    <p className="font-medium">{item.item}</p>
                    <p className="text-xs text-muted-foreground">{item.description}</p>
                  </div>
                  <span className="font-medium">{formatCurrency(item.cost)}</span>
                </div>
              ))}
            </div>
          )}

          <div className="pt-2 flex items-start gap-2 text-xs text-muted-foreground">
            <TrendingUp className="h-3 w-3 mt-0.5 flex-shrink-0" />
            <p>Recurring storage costs apply. One-time build costs are cached for reuse.</p>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

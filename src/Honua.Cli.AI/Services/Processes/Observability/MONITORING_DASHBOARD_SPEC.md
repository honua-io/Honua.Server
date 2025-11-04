# Process Framework Monitoring Dashboard Specification

## Overview
This document specifies the monitoring dashboard requirements for the Honua Process Framework. The dashboard provides real-time visibility into process execution, performance, and health.

## Dashboard Sections

### 1. Executive Summary (Top Row)
High-level KPIs for at-a-glance system health.

**Metrics:**
- **Total Active Processes**: Current number of running processes
  - Query: `process.active.count`
  - Visualization: Single Stat (with trend indicator)
  - Alert: Warning if > 50, Critical if > 100

- **Overall Success Rate (24h)**: Percentage of successful process completions
  - Query: `sum(process.completed) / (sum(process.completed) + sum(process.failed)) * 100`
  - Visualization: Gauge (0-100%)
  - Color coding: Green (>95%), Yellow (80-95%), Red (<80%)

- **Average Execution Time**: Mean process execution duration
  - Query: `avg(process.execution.duration)`
  - Visualization: Single Stat with sparkline
  - Unit: milliseconds

- **Error Rate (1h)**: Recent error frequency
  - Query: `rate(process.failed[1h])`
  - Visualization: Single Stat
  - Alert: Warning if > 5%, Critical if > 10%

### 2. Process Execution Trends (Row 2)
Time-series visualizations showing process activity over time.

**Charts:**

#### 2.1 Process Starts Over Time
- **Query**: `rate(process.started[5m])`
- **Visualization**: Line graph
- **Time range**: Last 24 hours
- **Granularity**: 5-minute intervals
- **Series**: Color-coded by workflow type (Deployment, Upgrade, Metadata, GitOps, Benchmark)

#### 2.2 Process Completion Rate
- **Query**: `rate(process.completed[5m])` and `rate(process.failed[5m])`
- **Visualization**: Stacked area chart
- **Series**: Successful (green), Failed (red)
- **Time range**: Last 24 hours

#### 2.3 Execution Duration Distribution
- **Query**: `histogram_quantile(0.5, process.execution.duration)`, `histogram_quantile(0.95, process.execution.duration)`, `histogram_quantile(0.99, process.execution.duration)`
- **Visualization**: Multi-line graph
- **Series**: P50, P95, P99
- **Time range**: Last 24 hours

### 3. Workflow-Specific Metrics (Row 3)
Breakdown by workflow type for detailed analysis.

**Panels:**

#### 3.1 Success Rate by Workflow Type
- **Query**: `process.workflow.success_rate{workflow.type=~".*"}`
- **Visualization**: Bar gauge
- **Dimensions**: One bar per workflow type
- **Color coding**: Green (>95%), Yellow (80-95%), Red (<80%)

#### 3.2 Execution Count by Workflow (24h)
- **Query**: `sum by (workflow.type) (increase(process.completed[24h]))`
- **Visualization**: Pie chart
- **Legend**: Show counts and percentages

#### 3.3 Average Duration by Workflow
- **Query**: `avg by (workflow.type) (process.execution.duration)`
- **Visualization**: Horizontal bar chart
- **Unit**: milliseconds
- **Sorting**: Descending by duration

### 4. Step-Level Performance (Row 4)
Detailed metrics for individual process steps.

**Panels:**

#### 4.1 Top 10 Slowest Steps
- **Query**: `topk(10, avg by (step.name) (process.step.duration))`
- **Visualization**: Table
- **Columns**: Step Name, Avg Duration (ms), Execution Count, Failure Rate
- **Sorting**: By average duration (descending)

#### 4.2 Step Execution Count
- **Query**: `sum by (step.name) (rate(process.step.executed[5m]))`
- **Visualization**: Heatmap
- **X-axis**: Time
- **Y-axis**: Step name
- **Color**: Execution rate

#### 4.3 Step Failure Rate
- **Query**: `sum by (step.name) (rate(process.step.failed[5m]))`
- **Visualization**: Line graph
- **Series**: One line per frequently-failing step
- **Alert**: Highlight if failure rate > 5%

### 5. Error Analysis (Row 5)
Detailed error tracking and diagnostics.

**Panels:**

#### 5.1 Error Rate Trend
- **Query**: `rate(process.failed[5m])`
- **Visualization**: Line graph with threshold lines
- **Thresholds**: Warning (5%), Critical (10%)
- **Time range**: Last 6 hours

#### 5.2 Errors by Type
- **Query**: `sum by (error.type) (process.failed)`
- **Visualization**: Pie chart
- **Legend**: Show error type and count

#### 5.3 Recent Errors
- **Query**: Process failure logs
- **Visualization**: Logs panel
- **Columns**: Timestamp, Process ID, Workflow Type, Error Reason
- **Limit**: Last 50 errors
- **Auto-refresh**: Every 30 seconds

### 6. Infrastructure Health (Row 6)
Health of supporting services and resources.

**Panels:**

#### 6.1 Redis Health
- **Metrics**:
  - Connection status
  - Latency (ms)
  - Number of connected endpoints
- **Visualization**: Stat panels with status indicators
- **Alert**: Red if disconnected or latency > 100ms

#### 6.2 LLM Service Availability
- **Metrics**:
  - Service status
  - Provider type
  - Configuration status
- **Visualization**: Stat panel with status badge

#### 6.3 Active Process Count by Workflow
- **Query**: `process.active.count{workflow.type=~".*"}`
- **Visualization**: Stacked bar chart
- **Series**: One per workflow type
- **Alert**: Warning if total > 50

### 7. Performance Analysis (Row 7)
Advanced performance metrics and trends.

**Panels:**

#### 7.1 Throughput (Processes/Hour)
- **Query**: `sum(rate(process.completed[1h])) * 3600`
- **Visualization**: Line graph with prediction band
- **Time range**: Last 7 days
- **Granularity**: 1-hour intervals

#### 7.2 Concurrent Process Capacity
- **Query**: `max(process.active.count)` and `avg(process.active.count)`
- **Visualization**: Multi-line graph
- **Series**: Maximum, Average
- **Time range**: Last 24 hours

#### 7.3 Resource Utilization Correlation
- **Queries**:
  - Active processes: `process.active.count`
  - CPU usage (if available)
  - Memory usage (if available)
- **Visualization**: Multi-axis line graph
- **Purpose**: Identify resource bottlenecks

## Alert Rules

### Critical Alerts
1. **Process Framework Down**
   - Condition: No process starts in last 5 minutes AND expected activity
   - Severity: Critical
   - Action: Page on-call engineer

2. **High Failure Rate**
   - Condition: `rate(process.failed[5m]) / rate(process.started[5m]) > 0.3`
   - Severity: Critical
   - Action: Send to incident channel

3. **Redis Connection Lost**
   - Condition: Redis health check fails
   - Severity: Critical
   - Action: Auto-restart Redis client, alert team

### Warning Alerts
1. **Degraded Success Rate**
   - Condition: Success rate < 90% for any workflow over 15 minutes
   - Severity: Warning
   - Action: Send to monitoring channel

2. **High Latency**
   - Condition: P95 execution duration > 2x baseline for 10 minutes
   - Severity: Warning
   - Action: Investigate performance

3. **Process Backlog**
   - Condition: Active processes > 50
   - Severity: Warning
   - Action: Check capacity planning

## Refresh and Retention

- **Dashboard Auto-Refresh**: 30 seconds
- **Metrics Retention**:
  - Raw metrics: 7 days
  - Aggregated (5m): 30 days
  - Aggregated (1h): 90 days
- **Log Retention**: 14 days
- **Trace Retention**: 7 days

## Access Control

- **View Access**: All engineers
- **Edit Access**: SRE team, Platform team
- **Alert Management**: SRE team

## Integration Points

- **Metrics Source**: OpenTelemetry Collector → Prometheus
- **Logs Source**: OpenTelemetry Collector → Loki or Azure Monitor
- **Traces Source**: OpenTelemetry Collector → Tempo or Application Insights
- **Alerting**: Prometheus Alertmanager or Azure Monitor Alerts
- **On-Call Integration**: PagerDuty / OpsGenie

## Dashboard Links

- **Related Dashboards**:
  - Semantic Kernel Performance
  - LLM API Usage and Costs
  - Infrastructure Overview
  - Application Health

- **Useful Links**:
  - Process Framework Documentation
  - Runbook: Process Framework Troubleshooting
  - Architecture Decision Records (ADRs)

// GeoETL Load Testing Script using k6
// Install k6: https://k6.io/docs/getting-started/installation/
// Run: k6 run load-test.js

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// Custom metrics
const workflowSuccessRate = new Rate('workflow_success_rate');
const workflowDuration = new Trend('workflow_duration');
const workflowErrors = new Counter('workflow_errors');

// Test configuration
export const options = {
    scenarios: {
        // Scenario 1: Ramp up concurrent workflow executions
        concurrent_workflows: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '2m', target: 10 },  // Ramp up to 10 users
                { duration: '5m', target: 50 },  // Ramp up to 50 users
                { duration: '5m', target: 50 },  // Stay at 50 users
                { duration: '2m', target: 0 },   // Ramp down
            ],
            gracefulRampDown: '30s',
        },

        // Scenario 2: Constant load
        constant_load: {
            executor: 'constant-vus',
            vus: 20,
            duration: '10m',
            startTime: '14m',
        },

        // Scenario 3: Spike test
        spike_test: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '1m', target: 10 },
                { duration: '30s', target: 100 },  // Spike
                { duration: '1m', target: 10 },
            ],
            startTime: '24m',
        },
    },

    thresholds: {
        // Workflow execution should succeed 95% of the time
        'workflow_success_rate': ['rate>0.95'],
        // 95% of workflows should complete within 60 seconds
        'workflow_duration': ['p(95)<60000'],
        // Error rate should be less than 5%
        'http_req_failed': ['rate<0.05'],
        // Response time thresholds
        'http_req_duration': ['p(95)<2000'],
    },
};

// Test configuration
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const API_KEY = __ENV.API_KEY || 'test-api-key';
const TENANT_ID = __ENV.TENANT_ID || '00000000-0000-0000-0000-000000000001';

// Headers
const headers = {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${API_KEY}`,
    'X-Tenant-Id': TENANT_ID,
};

// Sample workflow definition
const sampleWorkflow = {
    metadata: {
        name: 'Load Test Workflow',
        description: 'Test workflow for load testing',
        tags: ['load-test'],
    },
    parameters: {},
    nodes: [
        {
            id: 'source',
            type: 'data_source.postgis',
            name: 'Load Features',
            parameters: {
                tableName: 'test_features',
                limit: 1000,
            },
        },
        {
            id: 'buffer',
            type: 'geoprocessing.buffer',
            name: 'Buffer',
            parameters: {
                distance: 100,
            },
        },
        {
            id: 'sink',
            type: 'data_sink.postgis',
            name: 'Save Results',
            parameters: {
                tableName: 'test_results',
            },
        },
    ],
    edges: [
        { from: 'source', to: 'buffer' },
        { from: 'buffer', to: 'sink' },
    ],
};

export default function () {
    // Test 1: Create workflow
    const createRes = http.post(
        `${BASE_URL}/api/admin/geoetl/workflows`,
        JSON.stringify(sampleWorkflow),
        { headers }
    );

    const createSuccess = check(createRes, {
        'workflow created': (r) => r.status === 200 || r.status === 201,
    });

    if (!createSuccess) {
        workflowErrors.add(1);
        return;
    }

    const workflow = createRes.json();
    const workflowId = workflow.id;

    sleep(1);

    // Test 2: Execute workflow
    const executeStartTime = Date.now();
    const executeRes = http.post(
        `${BASE_URL}/api/admin/geoetl/workflows/${workflowId}/execute`,
        JSON.stringify({
            parameterValues: {},
            triggerType: 'manual',
        }),
        { headers }
    );

    const executeSuccess = check(executeRes, {
        'workflow executed': (r) => r.status === 200 || r.status === 202,
    });

    if (!executeSuccess) {
        workflowErrors.add(1);
        return;
    }

    const run = executeRes.json();
    const runId = run.id;

    // Test 3: Poll for completion
    let completed = false;
    let attempts = 0;
    const maxAttempts = 60; // 60 attempts * 1s = 60s timeout

    while (!completed && attempts < maxAttempts) {
        sleep(1);
        attempts++;

        const statusRes = http.get(
            `${BASE_URL}/api/admin/geoetl/runs/${runId}`,
            { headers }
        );

        if (statusRes.status === 200) {
            const status = statusRes.json();

            if (status.status === 'completed') {
                completed = true;
                const duration = Date.now() - executeStartTime;
                workflowDuration.add(duration);
                workflowSuccessRate.add(1);

                check(status, {
                    'workflow completed successfully': (s) => s.status === 'completed',
                    'all nodes completed': (s) => s.nodeRuns.every(n => n.status === 'completed'),
                });
            } else if (status.status === 'failed') {
                workflowSuccessRate.add(0);
                workflowErrors.add(1);
                break;
            }
        }
    }

    if (!completed) {
        console.log(`Workflow ${runId} did not complete within timeout`);
        workflowSuccessRate.add(0);
        workflowErrors.add(1);
    }

    sleep(1);
}

export function handleSummary(data) {
    return {
        'load-test-results.json': JSON.stringify(data, null, 2),
        stdout: textSummary(data, { indent: ' ', enableColors: true }),
    };
}

function textSummary(data, options) {
    const indent = options.indent || '';
    const lines = [];

    lines.push(`${indent}========== GeoETL Load Test Results ==========`);
    lines.push('');

    // Overall metrics
    lines.push(`${indent}Test Duration: ${(data.state.testRunDurationMs / 1000).toFixed(2)}s`);
    lines.push(`${indent}Total Requests: ${data.metrics.http_reqs.values.count}`);
    lines.push(`${indent}Request Rate: ${data.metrics.http_reqs.values.rate.toFixed(2)}/s`);
    lines.push('');

    // Workflow metrics
    if (data.metrics.workflow_success_rate) {
        lines.push(`${indent}Workflow Success Rate: ${(data.metrics.workflow_success_rate.values.rate * 100).toFixed(2)}%`);
    }

    if (data.metrics.workflow_duration) {
        lines.push(`${indent}Workflow Duration (p95): ${data.metrics.workflow_duration.values['p(95)'].toFixed(2)}ms`);
        lines.push(`${indent}Workflow Duration (avg): ${data.metrics.workflow_duration.values.avg.toFixed(2)}ms`);
    }

    if (data.metrics.workflow_errors) {
        lines.push(`${indent}Workflow Errors: ${data.metrics.workflow_errors.values.count}`);
    }

    lines.push('');

    // HTTP metrics
    lines.push(`${indent}HTTP Request Duration (p95): ${data.metrics.http_req_duration.values['p(95)'].toFixed(2)}ms`);
    lines.push(`${indent}HTTP Request Duration (avg): ${data.metrics.http_req_duration.values.avg.toFixed(2)}ms`);
    lines.push(`${indent}HTTP Request Failed: ${(data.metrics.http_req_failed.values.rate * 100).toFixed(2)}%`);
    lines.push('');

    // Threshold results
    lines.push(`${indent}Threshold Results:`);
    for (const [name, threshold] of Object.entries(data.thresholds || {})) {
        const status = threshold.ok ? '✓' : '✗';
        lines.push(`${indent}  ${status} ${name}`);
    }

    lines.push('');
    lines.push(`${indent}=============================================`);

    return lines.join('\n');
}

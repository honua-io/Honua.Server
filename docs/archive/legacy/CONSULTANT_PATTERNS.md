# Consultant Deployment Patterns

The consultant now supports retrieving human‑approved deployment patterns during planning. Patterns are stored in the vector search knowledge store and can be ingested from JSON files.

## JSON schema

The ingestion command expects an array of objects matching the `DeploymentPattern` model:

```json
{
  "id": "aws-zero-downtime",
  "name": "AWS Zero-Downtime Honua",
  "cloudProvider": "aws",
  "dataVolumeMin": 50,
  "dataVolumeMax": 200,
  "concurrentUsersMin": 50,
  "concurrentUsersMax": 300,
  "successRate": 0.97,
  "deploymentCount": 24,
  "configuration": { "database": { "engine": "Aurora" } }
}
```

A sample file is available at `data/consultant/deployment-patterns.sample.json`.

## Ingesting patterns

```bash
honua consultant-patterns --file data/consultant/deployment-patterns.sample.json
```

Use `--dry-run` to validate the file without indexing, and `--approved-by` to override the approver recorded in metadata.

## Configuration

Vector search defaults to the in-memory provider, which is used automatically for local development and test runs. You can override the provider through configuration (for example, `HONUA__VectorSearch__Provider=AzureAiSearch`).

To switch to Azure AI Search, add the following block to `appsettings.json` (or `appsettings.Azure.json`) and provide your service details:

```json
"VectorSearch": {
  "Provider": "AzureAiSearch",
  "Azure": {
    "Endpoint": "https://<search-name>.search.windows.net",
    "ApiKey": "<search-admin-key>",
    "IndexPrefix": "honua",
    "Dimensions": 1536
  }
}
```

You can set the same values via environment variables when running in CI/CD:

```bash
export HONUA__VectorSearch__Provider=AzureAiSearch
export HONUA__VectorSearch__Azure__Endpoint="https://<search-name>.search.windows.net"
export HONUA__VectorSearch__Azure__ApiKey="<search-admin-key>"
export HONUA__VectorSearch__Azure__IndexPrefix="honua"
export HONUA__VectorSearch__Azure__Dimensions=1536
```

## Planner integration

When deployment patterns are available, the semantic consultant automatically retrieves the top matches (based on inferred cloud provider, data volume, and user counts) and includes a summary in the planning prompt.

Each run also emits:

- A Markdown session log (e.g. `~/.honua/logs/consultant-YYYYMMDD.md`).
- For multi-agent sessions, a JSON transcript (`consultant-YYYYMMDD-multi-<guid>.json`) containing the agent steps and orchestration history.

The CLI prints both paths unless `--no-log` is used. Add `--verbose` to the consultant commands if you want the agent step summary echoed directly in the console.

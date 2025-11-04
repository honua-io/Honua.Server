# Ollama Local LLM Setup Guide

This guide covers how to set up and use Ollama for local LLM inference with the Honua AI Consultant.

## Overview

**Ollama** is a lightweight local LLM runtime that makes it easy to run large language models on your own hardware. It provides:

- **Zero-cost inference**: No API keys or cloud costs required
- **Privacy**: All data stays on your machine
- **Offline capability**: Works without internet connection
- **OpenAI-compatible API**: Easy integration with existing tools
- **Model library**: Access to dozens of open-source models (Llama, Mistral, Phi, CodeLlama, etc.)

## Installation

### macOS

```bash
# Install via Homebrew
brew install ollama

# Or download from https://ollama.com/download
```

### Linux

```bash
# Install via curl
curl -fsSL https://ollama.com/install.sh | sh
```

### Windows

Download and install from: https://ollama.com/download/windows

### Docker

```bash
# Run Ollama in a container
docker run -d -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama
```

## Starting Ollama

```bash
# Start the Ollama server
ollama serve
```

The server will start on `http://localhost:11434` by default.

## Downloading Models

Before using Ollama with Honua, you need to download at least one model:

### Recommended Models

| Model | Size | Use Case | Download Command |
|-------|------|----------|------------------|
| **llama3.2** | 2 GB | General purpose, fast | `ollama pull llama3.2` |
| **llama3.1** | 4.7 GB | Advanced reasoning | `ollama pull llama3.1` |
| **mistral** | 4.1 GB | Coding, technical | `ollama pull mistral` |
| **codellama** | 3.8 GB | Code generation | `ollama pull codellama` |
| **phi3** | 2.3 GB | Small, efficient | `ollama pull phi3` |
| **gemma2:2b** | 1.6 GB | Fastest, minimal | `ollama pull gemma2:2b` |

### Example: Download Llama 3.2

```bash
# Download the default recommended model
ollama pull llama3.2

# Expected output:
# pulling manifest
# pulling 8eeb52dfb3bb... 100% ▕████████████████▏ 2.0 GB
# pulling 73b313b5552d... 100% ▕████████████████▏  11 KB
# pulling 0ba8f0e314b4... 100% ▕████████████████▏  12 KB
# pulling 56bb8bd477a5... 100% ▕████████████████▏  96 B
# pulling 1a4c3c319823... 100% ▕████████████████▏ 485 B
# verifying sha256 digest
# writing manifest
# success
```

### List Available Models

```bash
# See all downloaded models
ollama list

# Example output:
# NAME             ID              SIZE    MODIFIED
# llama3.2:latest  a80c4f17acd5    2.0 GB  2 minutes ago
# mistral:latest   61e88e884507    4.1 GB  5 hours ago
```

### Remove Models

```bash
# Free up disk space by removing models
ollama rm mistral
```

## Configuration

### 1. Configure Honua to Use Ollama

Edit `appsettings.json` or create `appsettings.Development.json`:

```json
{
  "LlmProvider": {
    "Provider": "Ollama",
    "TimeoutSeconds": 300,
    "Ollama": {
      "EndpointUrl": "http://localhost:11434",
      "DefaultModel": "llama3.2"
    }
  }
}
```

### 2. Environment Variables (Alternative)

```bash
# Set via environment variables
export LlmProvider__Provider=Ollama
export LlmProvider__Ollama__EndpointUrl=http://localhost:11434
export LlmProvider__Ollama__DefaultModel=llama3.2
```

### 3. Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `Provider` | - | Set to `"Ollama"` to use local inference |
| `Ollama.EndpointUrl` | `http://localhost:11434` | Ollama server URL |
| `Ollama.DefaultModel` | `llama3.2` | Model to use for inference |
| `TimeoutSeconds` | 120 | Request timeout (increase for slower hardware) |

## Usage

### Basic Example

```bash
# Start Honua CLI with Ollama
honua ai ask "Explain what Honua does"
```

### Verify Ollama Health

```bash
# Check if Ollama is running and models are available
curl http://localhost:11434/api/tags

# Expected response:
# {
#   "models": [
#     {
#       "name": "llama3.2:latest",
#       "modified_at": "2024-01-15T10:30:00Z",
#       "size": 2000000000
#     }
#   ]
# }
```

### Test Inference Directly

```bash
# Test Ollama directly
curl http://localhost:11434/api/generate -d '{
  "model": "llama3.2",
  "prompt": "Why is the sky blue?",
  "stream": false
}'
```

## Performance Tuning

### Hardware Requirements

| Model Size | Minimum RAM | Recommended RAM | GPU |
|------------|-------------|-----------------|-----|
| Small (2 GB) | 8 GB | 16 GB | Optional |
| Medium (4 GB) | 16 GB | 32 GB | Recommended |
| Large (7 GB+) | 32 GB | 64 GB | Highly recommended |

### GPU Acceleration

Ollama automatically uses GPU acceleration if available:

- **NVIDIA GPUs**: CUDA support (RTX 3060 or better recommended)
- **Apple Silicon**: Metal acceleration (M1/M2/M3)
- **AMD GPUs**: ROCm support on Linux

### Configuration for Slow Hardware

If inference is too slow, try these optimizations:

```json
{
  "LlmProvider": {
    "Provider": "Ollama",
    "TimeoutSeconds": 600,
    "DefaultMaxTokens": 1024,
    "Ollama": {
      "EndpointUrl": "http://localhost:11434",
      "DefaultModel": "gemma2:2b"
    }
  }
}
```

**Tips:**
- Use smaller models (gemma2:2b, phi3)
- Reduce `DefaultMaxTokens` to generate shorter responses
- Increase `TimeoutSeconds` to avoid timeouts
- Close other applications to free up RAM

## Troubleshooting

### Ollama Not Running

**Symptom**: `Cannot connect to Ollama at http://localhost:11434`

**Solution**:
```bash
# Check if Ollama is running
curl http://localhost:11434/api/tags

# If not running, start it
ollama serve
```

### Model Not Found

**Symptom**: `Configured model 'llama3.2' is not available`

**Solution**:
```bash
# Download the model
ollama pull llama3.2

# Verify it's installed
ollama list
```

### Slow Inference

**Symptom**: Requests timeout or take several minutes

**Solutions**:
1. **Use a smaller model**:
   ```bash
   ollama pull gemma2:2b
   ```

2. **Increase timeout**:
   ```json
   {
     "LlmProvider": {
       "TimeoutSeconds": 600
     }
   }
   ```

3. **Check GPU utilization**:
   ```bash
   # NVIDIA
   nvidia-smi

   # macOS
   sudo powermetrics --samplers gpu_power
   ```

### Out of Memory

**Symptom**: Ollama crashes or system becomes unresponsive

**Solutions**:
1. **Use a smaller model**
2. **Reduce concurrent requests**
3. **Add swap space** (Linux):
   ```bash
   sudo fallocate -l 8G /swapfile
   sudo chmod 600 /swapfile
   sudo mkswap /swapfile
   sudo swapon /swapfile
   ```

### Port Already in Use

**Symptom**: `Error: listen tcp :11434: bind: address already in use`

**Solution**:
```bash
# Find the process using port 11434
lsof -i :11434

# Kill the process
kill -9 <PID>

# Or use a different port
OLLAMA_HOST=0.0.0.0:11435 ollama serve
```

Then update configuration:
```json
{
  "LlmProvider": {
    "Ollama": {
      "EndpointUrl": "http://localhost:11435"
    }
  }
}
```

## Advanced Configuration

### Custom Ollama Host

For remote Ollama instances:

```json
{
  "LlmProvider": {
    "Ollama": {
      "EndpointUrl": "http://192.168.1.100:11434",
      "DefaultModel": "llama3.2"
    }
  }
}
```

### Multiple Models

You can switch models at runtime (if implemented in Honua):

```bash
# Use different models for different tasks
honua ai ask "Write code to parse JSON" --model codellama
honua ai ask "Explain quantum physics" --model llama3.1
```

### Model Customization

Create custom model variants with specific parameters:

```bash
# Create a custom model with specific settings
ollama create my-assistant -f Modelfile
```

Example `Modelfile`:
```dockerfile
FROM llama3.2

# Set custom parameters
PARAMETER temperature 0.7
PARAMETER top_p 0.9
PARAMETER num_predict 2048

# Set custom system prompt
SYSTEM You are a helpful Honua AI assistant specialized in geospatial data.
```

### Docker Compose Integration

For integrated deployments with Honua:

```yaml
version: '3.8'

services:
  ollama:
    image: ollama/ollama
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    environment:
      - OLLAMA_HOST=0.0.0.0:11434
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]

  honua:
    image: honua/honua-cli-ai
    depends_on:
      - ollama
    environment:
      - LlmProvider__Provider=Ollama
      - LlmProvider__Ollama__EndpointUrl=http://ollama:11434
      - LlmProvider__Ollama__DefaultModel=llama3.2

volumes:
  ollama_data:
```

## Model Comparison

### Performance Benchmarks

Based on typical hardware (16 GB RAM, no GPU):

| Model | Size | Tokens/sec | Quality | Best For |
|-------|------|------------|---------|----------|
| gemma2:2b | 1.6 GB | 80-100 | Good | Quick queries, resource-constrained |
| phi3 | 2.3 GB | 60-80 | Very Good | General use, coding |
| llama3.2 | 2.0 GB | 50-70 | Excellent | Recommended default |
| mistral | 4.1 GB | 30-50 | Excellent | Technical, coding |
| llama3.1 | 4.7 GB | 25-40 | Superior | Complex reasoning |
| codellama | 3.8 GB | 30-50 | Excellent | Code generation |

### Quality vs Speed Tradeoff

```
Speed:   gemma2:2b > phi3 > llama3.2 > codellama > mistral > llama3.1
Quality: llama3.1 > mistral > llama3.2 > codellama > phi3 > gemma2:2b
```

## Best Practices

### 1. Model Selection
- **Development**: Use `llama3.2` for best balance
- **Production**: Use `mistral` or `llama3.1` for quality
- **CI/CD**: Use `gemma2:2b` for speed
- **Coding tasks**: Use `codellama` or `mistral`

### 2. Resource Management
- Download only models you need
- Remove unused models with `ollama rm`
- Monitor disk usage (models are stored in `~/.ollama/models`)

### 3. Deployment
- Use Docker for consistent environments
- Pre-pull models in container images
- Configure health checks to verify Ollama availability
- Set appropriate timeouts based on hardware

### 4. Security
- Keep Ollama updated: `brew upgrade ollama` or `curl -fsSL https://ollama.com/install.sh | sh`
- Don't expose Ollama port to public internet without authentication
- Use firewall rules to restrict access

## Comparison with Cloud Providers

| Feature | Ollama | OpenAI | Azure OpenAI |
|---------|--------|--------|--------------|
| **Cost** | Free | $0.03-0.12/1K tokens | $0.03-0.12/1K tokens |
| **Privacy** | Complete | Limited | Limited |
| **Internet Required** | No | Yes | Yes |
| **Setup Complexity** | Medium | Low | Medium |
| **Quality** | Good-Excellent | Excellent | Excellent |
| **Speed** | Depends on hardware | Fast | Fast |
| **Model Selection** | 50+ models | 10+ models | 10+ models |
| **Fine-tuning** | Yes (custom models) | Limited | Yes |

### When to Use Ollama

**Use Ollama when**:
- Privacy and data sovereignty are critical
- Working with sensitive/confidential information
- Internet connectivity is unreliable
- Cost optimization is important
- You need full control over the inference pipeline

**Use Cloud Providers when**:
- Need highest quality responses (GPT-4o, Claude 3.5)
- Don't want to manage infrastructure
- Have inconsistent workloads (pay-per-use)
- Need enterprise support

## Additional Resources

- **Ollama Documentation**: https://github.com/ollama/ollama/blob/main/docs/README.md
- **Model Library**: https://ollama.com/library
- **API Reference**: https://github.com/ollama/ollama/blob/main/docs/api.md
- **Community Discord**: https://discord.gg/ollama
- **GitHub Issues**: https://github.com/ollama/ollama/issues

## Health Check Integration

Honua includes built-in health checks for Ollama:

```bash
# Check Honua health including Ollama
curl http://localhost:5000/health

# Example response:
# {
#   "status": "Healthy",
#   "checks": {
#     "ollama": {
#       "status": "Healthy",
#       "description": "Ollama is running with 3 model(s). Model 'llama3.2' is available.",
#       "data": {
#         "endpoint": "http://localhost:11434",
#         "configured_model": "llama3.2",
#         "model_count": 3,
#         "available_models": ["llama3.2:latest", "mistral:latest", "phi3:latest"]
#       }
#     }
#   }
# }
```

## Support

If you encounter issues with Ollama integration:

1. Check Ollama logs: `journalctl -u ollama` (Linux) or check Console.app (macOS)
2. Verify model availability: `ollama list`
3. Test Ollama directly: `curl http://localhost:11434/api/tags`
4. Check Honua logs for detailed error messages
5. Report issues at: https://github.com/HonuaIO/Honua/issues

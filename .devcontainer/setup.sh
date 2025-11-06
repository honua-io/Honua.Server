#!/bin/bash
set -e

echo "Setting up Honua Server development environment..."

# Install .NET tools
dotnet tool install --global dotnet-ef 2>/dev/null || true

# Setup Python virtual environment
cd /workspace/tests/python
python3 -m venv .venv
source .venv/bin/activate
pip install --upgrade pip setuptools wheel
pip install -r requirements.txt

# Start test environment
cd /workspace/tests
bash start-shared-test-env.sh start

echo "Development environment setup complete!"
echo "Run tests with: cd tests/python && source .venv/bin/activate && pytest -n auto"

#!/bin/bash
# Temporary debug script to run a single WFS test and capture output

cd /home/mike/projects/Honua.Server

dotnet test tests/Honua.Server.Integration.Tests/Honua.Server.Integration.Tests.csproj \
  --filter "FullyQualifiedName~WfsTests.DescribeFeatureType_ReturnsSchema" \
  --logger "console;verbosity=detailed" \
  2>&1 | tail -200

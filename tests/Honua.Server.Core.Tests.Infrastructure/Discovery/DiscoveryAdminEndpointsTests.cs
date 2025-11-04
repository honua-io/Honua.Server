// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Honua.Server.Host.Discovery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Discovery;

/// <summary>
/// Tests for Discovery Admin API endpoints.
/// Note: These are simplified tests. Full integration tests are in separate files.
/// </summary>
public sealed class DiscoveryAdminEndpointsTests
{
    [Fact]
    public void DiscoveryStatus_Properties_SetCorrectly()
    {
        // Arrange & Act
        var status = new DiscoveryStatus
        {
            Enabled = true,
            ODataDiscoveryEnabled = true,
            OgcDiscoveryEnabled = true,
            CacheDuration = TimeSpan.FromMinutes(5),
            MaxTables = 100,
            RequireSpatialIndex = false,
            PostGisDataSourceCount = 1,
            ConfiguredDataSourceId = "test-datasource",
            BackgroundRefreshEnabled = true,
            BackgroundRefreshInterval = TimeSpan.FromMinutes(5)
        };

        // Assert
        Assert.True(status.Enabled);
        Assert.True(status.ODataDiscoveryEnabled);
        Assert.True(status.OgcDiscoveryEnabled);
        Assert.Equal(TimeSpan.FromMinutes(5), status.CacheDuration);
        Assert.Equal(100, status.MaxTables);
        Assert.False(status.RequireSpatialIndex);
        Assert.Equal(1, status.PostGisDataSourceCount);
        Assert.Equal("test-datasource", status.ConfiguredDataSourceId);
        Assert.True(status.BackgroundRefreshEnabled);
    }

    [Fact]
    public void DiscoveredTablesResponse_Properties_SetCorrectly()
    {
        // Arrange & Act
        var response = new DiscoveredTablesResponse
        {
            Tables = Array.Empty<Core.Discovery.DiscoveredTable>(),
            TotalCount = 0,
            DataSourceId = "test-datasource",
            Message = "No tables found"
        };

        // Assert
        Assert.Empty(response.Tables);
        Assert.Equal(0, response.TotalCount);
        Assert.Equal("test-datasource", response.DataSourceId);
        Assert.Equal("No tables found", response.Message);
    }

    [Fact]
    public void RefreshResult_Properties_SetCorrectly()
    {
        // Arrange & Act
        var result = new RefreshResult
        {
            Success = true,
            Message = "Cache refreshed",
            TablesDiscovered = 5,
            DataSourceId = "test-datasource"
        };

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Cache refreshed", result.Message);
        Assert.Equal(5, result.TablesDiscovered);
        Assert.Equal("test-datasource", result.DataSourceId);
    }

    [Fact]
    public void ClearCacheResult_Properties_SetCorrectly()
    {
        // Arrange & Act
        var result = new ClearCacheResult
        {
            Success = true,
            Message = "Cache cleared"
        };

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Cache cleared", result.Message);
    }

    // Note: Full integration tests with actual HTTP requests would require setting up
    // a complete test server with authentication, which is better suited for
    // integration test projects. The tests above verify the DTO structures work correctly.
}

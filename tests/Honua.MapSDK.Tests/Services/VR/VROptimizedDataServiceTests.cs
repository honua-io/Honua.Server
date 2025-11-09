using System;
using System.Collections.Generic;
using System.Linq;
using Honua.MapSDK.Services.VR;
using Xunit;

namespace Honua.MapSDK.Tests.Services.VR
{
    public class VROptimizedDataServiceTests
    {
        [Fact]
        public void ApplyLevelOfDetail_NearFeatures_ShouldReturnFullDetail()
        {
            // Arrange
            var service = new VROptimizedDataService();
            var viewpoint = new VRViewpoint
            {
                Position = new Position3D { X = 0, Y = 0, Z = 0 }
            };

            var features = new List<VRFeature>
            {
                new VRFeature
                {
                    Id = "feature1",
                    Position = new Position3D { X = 5, Y = 0, Z = 0 } // 5m away
                }
            };

            // Act
            var optimized = service.ApplyLevelOfDetail(features, viewpoint, "medium");

            // Assert
            Assert.Single(optimized);
            Assert.Equal(LODLevel.Full, optimized[0].LODLevel);
        }

        [Fact]
        public void ApplyLevelOfDetail_DistantFeatures_ShouldReduceDetail()
        {
            // Arrange
            var service = new VROptimizedDataService();
            var viewpoint = new VRViewpoint
            {
                Position = new Position3D { X = 0, Y = 0, Z = 0 }
            };

            var features = new List<VRFeature>
            {
                new VRFeature
                {
                    Id = "feature1",
                    Position = new Position3D { X = 500, Y = 0, Z = 0 } // 500m away
                }
            };

            // Act
            var optimized = service.ApplyLevelOfDetail(features, viewpoint, "medium");

            // Assert
            Assert.Single(optimized);
            Assert.Equal(LODLevel.Medium, optimized[0].LODLevel);
        }

        [Fact]
        public void ApplyLevelOfDetail_VeryDistantFeatures_ShouldCull()
        {
            // Arrange
            var service = new VROptimizedDataService();
            var viewpoint = new VRViewpoint
            {
                Position = new Position3D { X = 0, Y = 0, Z = 0 }
            };

            var features = new List<VRFeature>
            {
                new VRFeature
                {
                    Id = "feature1",
                    Position = new Position3D { X = 15000, Y = 0, Z = 0 } // 15km away
                }
            };

            // Act
            var optimized = service.ApplyLevelOfDetail(features, viewpoint, "medium");

            // Assert
            Assert.Empty(optimized); // Should be culled
        }

        [Fact]
        public void ApplyLevelOfDetail_HighQuality_ShouldIncreaseRenderDistance()
        {
            // Arrange
            var service = new VROptimizedDataService();
            var viewpoint = new VRViewpoint
            {
                Position = new Position3D { X = 0, Y = 0, Z = 0 }
            };

            var features = new List<VRFeature>
            {
                new VRFeature
                {
                    Id = "feature1",
                    Position = new Position3D { X = 15, Y = 0, Z = 0 }
                }
            };

            // Act - High quality
            var optimizedHigh = service.ApplyLevelOfDetail(features, viewpoint, "high");

            // Act - Low quality
            var optimizedLow = service.ApplyLevelOfDetail(features, viewpoint, "low");

            // Assert
            Assert.Equal(LODLevel.Full, optimizedHigh[0].LODLevel);
            Assert.Equal(LODLevel.High, optimizedLow[0].LODLevel);
        }

        [Fact]
        public void CalculateTileLevel_HighViewpoint_ShouldReturnLowerZoom()
        {
            // Arrange
            var service = new VROptimizedDataService();
            var lowViewpoint = new VRViewpoint
            {
                Position = new Position3D { X = 0, Y = 10, Z = 0 }
            };
            var highViewpoint = new VRViewpoint
            {
                Position = new Position3D { X = 0, Y = 1000, Z = 0 }
            };

            // Act
            var lowZoom = service.CalculateTileLevel(lowViewpoint, 100);
            var highZoom = service.CalculateTileLevel(highViewpoint, 100);

            // Assert
            Assert.True(highZoom < lowZoom, "Higher viewpoint should have lower zoom level");
        }

        [Fact]
        public void CalculateTileLevel_ShouldClampToValidRange()
        {
            // Arrange
            var service = new VROptimizedDataService();
            var viewpoint = new VRViewpoint
            {
                Position = new Position3D { X = 0, Y = 10000, Z = 0 }
            };

            // Act
            var zoom = service.CalculateTileLevel(viewpoint, 10000);

            // Assert
            Assert.InRange(zoom, 8, 18);
        }

        [Fact]
        public void OptimizeGeometry_FullLOD_ShouldRetainAllVertices()
        {
            // Arrange
            var service = new VROptimizedDataService();
            var geometry = new VRGeometry
            {
                Id = "geom1",
                Vertices = new List<float> { 1, 2, 3, 4, 5, 6, 7, 8, 9 },
                TriangleCount = 3
            };

            // Act
            var optimized = service.OptimizeGeometry(geometry, LODLevel.Full);

            // Assert
            Assert.Equal(geometry.Vertices.Count, optimized.Vertices.Count);
            Assert.Equal(geometry.TriangleCount, optimized.TriangleCount);
        }

        [Fact]
        public void OptimizeGeometry_MediumLOD_ShouldReduceVertices()
        {
            // Arrange
            var service = new VROptimizedDataService();
            var geometry = new VRGeometry
            {
                Id = "geom1",
                Vertices = Enumerable.Range(0, 1000).Select(i => (float)i).ToList(),
                TriangleCount = 333
            };

            // Act
            var optimized = service.OptimizeGeometry(geometry, LODLevel.Medium);

            // Assert
            Assert.True(optimized.Vertices.Count < geometry.Vertices.Count);
            Assert.Equal(166, optimized.TriangleCount); // 50% of original
        }

        [Fact]
        public void OptimizeGeometry_Billboard_ShouldCreateQuad()
        {
            // Arrange
            var service = new VROptimizedDataService();
            var geometry = new VRGeometry
            {
                Id = "geom1",
                Vertices = Enumerable.Range(0, 1000).Select(i => (float)i).ToList(),
                TriangleCount = 333
            };

            // Act
            var optimized = service.OptimizeGeometry(geometry, LODLevel.Billboard);

            // Assert
            Assert.Equal(12, optimized.Vertices.Count); // 4 vertices * 3 coords
            Assert.Equal(2, optimized.TriangleCount); // 2 triangles for quad
        }

        [Fact]
        public void BatchForInstancing_ShouldGroupBySameTypeAndLOD()
        {
            // Arrange
            var service = new VROptimizedDataService();
            var features = new List<VRFeature>
            {
                new VRFeature { Id = "f1", Type = "building", LODLevel = LODLevel.Full },
                new VRFeature { Id = "f2", Type = "building", LODLevel = LODLevel.Full },
                new VRFeature { Id = "f3", Type = "building", LODLevel = LODLevel.Medium },
                new VRFeature { Id = "f4", Type = "road", LODLevel = LODLevel.Full }
            };

            // Act
            var batches = service.BatchForInstancing(features);

            // Assert
            Assert.Equal(3, batches.Count); // building_Full, building_Medium, road_Full
            Assert.Equal(2, batches["building_Full"].Count);
            Assert.Single(batches["building_Medium"]);
            Assert.Single(batches["road_Full"]);
        }

        [Fact]
        public void EstimateMemoryUsage_ShouldCalculateApproximateSize()
        {
            // Arrange
            var service = new VROptimizedDataService();
            var features = new List<VRFeature>
            {
                new VRFeature
                {
                    Id = "f1",
                    Geometry = new VRGeometry
                    {
                        Vertices = Enumerable.Range(0, 1000).Select(i => (float)i).ToList()
                    }
                }
            };

            // Act
            var memoryUsage = service.EstimateMemoryUsage(features);

            // Assert
            Assert.True(memoryUsage > 0);
            // Each vertex is 12 bytes (3 floats), plus overhead
            var expectedMin = 1000 * 12;
            Assert.True(memoryUsage >= expectedMin);
        }

        [Fact]
        public void EstimateMemoryUsage_EmptyFeatures_ShouldReturnZero()
        {
            // Arrange
            var service = new VROptimizedDataService();
            var features = new List<VRFeature>();

            // Act
            var memoryUsage = service.EstimateMemoryUsage(features);

            // Assert
            Assert.Equal(0, memoryUsage);
        }

        [Fact]
        public void VRFeature_Clone_ShouldCreateDeepCopy()
        {
            // Arrange
            var original = new VRFeature
            {
                Id = "feature1",
                Type = "building",
                Position = new Position3D { X = 1, Y = 2, Z = 3 },
                Properties = new Dictionary<string, object> { { "height", 50 } },
                LODLevel = LODLevel.Full,
                SimplificationTolerance = 0.1
            };

            // Act
            var clone = original.Clone();

            // Assert
            Assert.Equal(original.Id, clone.Id);
            Assert.Equal(original.Type, clone.Type);
            Assert.NotSame(original.Position, clone.Position);
            Assert.NotSame(original.Properties, clone.Properties);
            Assert.Equal(original.LODLevel, clone.LODLevel);
        }
    }
}

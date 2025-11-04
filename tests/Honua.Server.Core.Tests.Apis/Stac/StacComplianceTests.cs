using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Docker.DotNet;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using DotNet.Testcontainers.Configurations;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Honua.Server.Host.Stac;
using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Stac;

[CollectionDefinition(CollectionName)]
[Trait("Category", "Integration")]
public sealed class StacComplianceCollection : ICollectionFixture<StacComplianceFixture>
{
    public const string CollectionName = "stac-compliance";
}

[Collection(StacComplianceCollection.CollectionName)]
public sealed class StacComplianceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly object GdalSync = new();
    private static bool _gdalConfigured;

    private readonly StacComplianceFixture _fixture;

    public StacComplianceTests(StacComplianceFixture fixture)
    {
        _fixture = fixture;
        EnsureGdalConfigured();
    }

    [Fact]
    public async Task GeneratedCatalog_PassesValidator_And_RasterioReadsCog()
    {
        if (!StacComplianceFixture.IsEnabled)
        {
            return;
        }

        const string bucketName = "honua-stac-test";
        const string objectKey = "imagery/urban-cog.tif";
        const string thumbnailKey = "imagery/urban-thumb.png";

        await _fixture.EnsureBucketAsync(bucketName);

        using var workspace = new TempWorkspace();
        var sourcePath = Path.Combine(workspace.Path, "source.tif");
        var cogPath = Path.Combine(workspace.Path, "urban-cog.tif");
        CreateSampleGeoTiff(sourcePath);
        ConvertToCog(sourcePath, cogPath);

        await _fixture.UploadFileAsync(bucketName, objectKey, cogPath);
        await _fixture.UploadBytesAsync(bucketName, thumbnailKey, SampleThumbnailPng, "image/png");

        var endpoint = _fixture.PublicEndpoint;
        var assetHref = new UriBuilder(endpoint)
        {
            Path = $"{bucketName}/{objectKey}"
        }.Uri.ToString();
        var thumbnailHref = new UriBuilder(endpoint)
        {
            Path = $"{bucketName}/{thumbnailKey}"
        }.Uri.ToString();

        var snapshot = BuildMetadataSnapshot(assetHref, thumbnailHref);
        var builder = new RasterStacCatalogBuilder();
        var dataset = snapshot.RasterDatasets.Single();
        var (collectionRecord, items) = builder.Build(dataset, snapshot);
        var baseUri = new Uri("http://localhost/stac");

        var collectionResponse = StacApiMapper.BuildCollection(collectionRecord, baseUri);
        var itemResponse = StacApiMapper.BuildItem(items[0], baseUri);

        var stacDir = Path.Combine(workspace.Path, "stac");
        Directory.CreateDirectory(stacDir);
        var collectionPath = Path.Combine(stacDir, "collection.json");
        var itemPath = Path.Combine(stacDir, "item.json");
        File.WriteAllText(collectionPath, JsonSerializer.Serialize(collectionResponse, JsonOptions));
        File.WriteAllText(itemPath, JsonSerializer.Serialize(itemResponse, JsonOptions));

        await _fixture.ValidateStacAsync(collectionPath);
        await _fixture.ValidateStacAsync(itemPath);

        await _fixture.ValidateRasterAsync(bucketName, objectKey);
    }

    private static MetadataSnapshot BuildMetadataSnapshot(string assetHref, string thumbnailHref)
    {
        var catalog = new CatalogDefinition
        {
            Id = "catalog",
            Title = "Compliance Catalog",
            Version = "1.1.0",
            License = new CatalogLicenseDefinition
            {
                Name = "proprietary"
            }
        };

        var now = DateTimeOffset.UtcNow;
        var datasetExtent = new LayerExtentDefinition
        {
            Bbox = new[] { new[] { 0d, -64d, 64d, 0d } },
            Temporal = new[]
            {
                new TemporalIntervalDefinition
                {
                    Start = now.AddDays(-1),
                    End = now
                }
            }
        };

        var folder = new FolderDefinition
        {
            Id = "root",
            Title = "Root"
        };

        var dataSource = new DataSourceDefinition
        {
            Id = "primary",
            Provider = "sqlite",
            ConnectionString = "Data Source=:memory:"
        };

        var layer = new LayerDefinition
        {
            Id = "imagery",
            ServiceId = "imagery-service",
            Title = "Imagery Layer",
            GeometryType = "polygon",
            IdField = "id",
            GeometryField = "geom"
        };

        var service = new ServiceDefinition
        {
            Id = "imagery-service",
            Title = "Imagery Service",
            FolderId = folder.Id,
            ServiceType = "raster",
            DataSourceId = dataSource.Id,
            Layers = new[] { layer }
        };

        var dataset = new RasterDatasetDefinition
        {
            Id = "urban-imagery",
            Title = "Urban Imagery",
            ServiceId = service.Id,
            LayerId = layer.Id,
            Extent = datasetExtent,
            Source = new RasterSourceDefinition
            {
                Type = "cog",
                Uri = assetHref,
                MediaType = "image/tiff; application=geotiff; profile=cloud-optimized"
            },
            Catalog = new CatalogEntryDefinition
            {
                Thumbnail = thumbnailHref,
                Keywords = new List<string> { "urban", "imagery" },
                Themes = new List<string> { "environment" },
                Summary = "Urban sample imagery"
            },
            Keywords = new List<string> { "imagery" },
            Styles = new RasterStyleDefinition
            {
                DefaultStyleId = "natural-color",
                StyleIds = new List<string> { "natural-color" }
            }
        };

        var naturalColorStyle = new StyleDefinition
        {
            Id = "natural-color",
            Renderer = "simple",
            GeometryType = "polygon",
            Simple = new SimpleStyleDefinition { FillColor = "#ffffff" }
        };

        return new MetadataSnapshot(
            catalog,
            new[] { folder },
            new[] { dataSource },
            new[] { service },
            new[] { layer },
            new[] { dataset },
            new[] { naturalColorStyle });
    }

    private static void CreateSampleGeoTiff(string path)
    {
        var driver = Gdal.GetDriverByName("GTiff");
        using var dataset = driver.Create(path, 64, 64, 1, DataType.GDT_Byte, new[] { "TILED=YES", "BLOCKXSIZE=32", "BLOCKYSIZE=32" });
        dataset.SetGeoTransform(new double[] { 0, 1, 0, 0, 0, -1 });
        dataset.SetProjection("EPSG:4326");

        using var band = dataset.GetRasterBand(1);
        var buffer = new byte[64 * 64];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)(i % 255);
        }

        band.WriteRaster(0, 0, 64, 64, buffer, 64, 64, 0, 0);
        band.FlushCache();
        dataset.FlushCache();
    }

    private static void ConvertToCog(string sourcePath, string destinationPath)
    {
        using var source = Gdal.Open(sourcePath, Access.GA_ReadOnly);
        using var translateOptions = new GDALTranslateOptions(new[] { "-of", "COG", "-co", "COMPRESS=LZW" });
        Gdal.wrapper_GDALTranslate(destinationPath, source, translateOptions, null, null);
    }

    private static byte[] SampleThumbnailPng => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNgYGD4DwABBAEA" +
        "AelS1gAAAABJRU5ErkJggg==");

    private static void EnsureGdalConfigured()
    {
        lock (GdalSync)
        {
            if (_gdalConfigured)
            {
                return;
            }

            GdalBase.ConfigureAll();
            Gdal.AllRegister();
            _gdalConfigured = true;
        }
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "honua-stac-", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                try
                {
                    Directory.Delete(Path, true);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}

public sealed class StacComplianceFixture : IAsyncLifetime
{
    internal const string ComplianceEnvVar = "HONUA_RUN_STAC_COMPLIANCE";

    private const string AwsKey = "test";
    private const string AwsSecret = "test";

    private INetwork? _network;
    private IContainer? _localStack;
    private DockerClient? _dockerClient;
    private string? _localStackContainerName;
    private string? _internalEndpoint;

    public AmazonS3Client? S3Client { get; private set; }
    public Uri PublicEndpoint { get; private set; } = null!;

    public static bool IsEnabled
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(ComplianceEnvVar);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task InitializeAsync()
    {
        if (!IsEnabled)
        {
            return;
        }

        TestcontainersSettings.ResourceReaperEnabled = false;

        var authConfig = TestcontainersSettings.OS.DockerEndpointAuthConfig;
        _dockerClient = new DockerClientConfiguration(authConfig.Endpoint).CreateClient();

        foreach (var image in new[] { "localstack/localstack:latest", "python:3.11-slim", "osgeo/gdal:ubuntu-small-3.6.3" })
        {
            await EnsureImageAsync(_dockerClient, image).ConfigureAwait(false);
        }

        _network = new NetworkBuilder()
            .WithName($"stac-net-{Guid.NewGuid():N}")
            .Build();
        await _network.CreateAsync().ConfigureAwait(false);

        _localStackContainerName = $"stac-localstack-{Guid.NewGuid():N}";
        _internalEndpoint = $"http://{_localStackContainerName}:4566";

        _localStack = new ContainerBuilder()
            .WithImage("localstack/localstack:latest")
            .WithNetwork(_network)
            .WithHostname(_localStackContainerName)
            .WithName(_localStackContainerName)
            .WithEnvironment("SERVICES", "s3")
            .WithEnvironment("AWS_ACCESS_KEY_ID", AwsKey)
            .WithEnvironment("AWS_SECRET_ACCESS_KEY", AwsSecret)
            .WithEnvironment("AWS_DEFAULT_REGION", "us-east-1")
            .WithPortBinding(4566, true)
            .Build();

        await _localStack.StartAsync().ConfigureAwait(false);

        var mappedPort = _localStack.GetMappedPublicPort(4566);
        PublicEndpoint = new Uri($"http://localhost:{mappedPort}");

        var config = new AmazonS3Config
        {
            ServiceURL = $"http://localhost:{mappedPort}",
            ForcePathStyle = true,
            UseHttp = true
        };
        S3Client = new AmazonS3Client(AwsKey, AwsSecret, config);

        await WaitForS3Async().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (S3Client is not null)
        {
            S3Client.Dispose();
            S3Client = null;
        }

        if (_localStack is not null)
        {
            await _localStack.DisposeAsync().ConfigureAwait(false);
        }

        if (_network is not null)
        {
            await _network.DeleteAsync().ConfigureAwait(false);
        }

        if (_dockerClient is not null)
        {
            _dockerClient.Dispose();
            _dockerClient = null;
        }

        _localStack = null;
        _network = null;
        _localStackContainerName = null;
        _internalEndpoint = null;
    }

    public async Task EnsureBucketAsync(string bucketName)
    {
        if (S3Client is null)
        {
            throw new InvalidOperationException("Fixture not initialized.");
        }

        try
        {
            await S3Client.PutBucketAsync(bucketName).ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // bucket already exists
        }
    }

    public async Task UploadFileAsync(string bucketName, string key, string filePath)
    {
        if (S3Client is null)
        {
            throw new InvalidOperationException();
        }

        using var stream = File.OpenRead(filePath);
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = stream
        };
        await S3Client.PutObjectAsync(request).ConfigureAwait(false);
    }

    public async Task UploadBytesAsync(string bucketName, string key, byte[] data, string contentType)
    {
        if (S3Client is null)
        {
            throw new InvalidOperationException();
        }

        using var stream = new MemoryStream(data);
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType
        };
        await S3Client.PutObjectAsync(request).ConfigureAwait(false);
    }


    private async Task WaitForS3Async()
    {
        if (S3Client is null)
        {
            return;
        }

        const int retryCount = 10;
        for (var attempt = 0; attempt < retryCount; attempt++)
        {
            try
            {
                await S3Client.ListBucketsAsync().ConfigureAwait(false);
                return;
            }
            catch
            {
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("LocalStack S3 failed to start within timeout.");
    }


    public async Task ValidateStacAsync(string path)
    {
        var fileName = Path.GetFileName(path);
        var hostDirectory = Path.GetDirectoryName(path)!;

        var validatorCommand = $"pip install --no-cache-dir stac-validator > /tmp/pip.log 2>&1 && stac-validator /data/{fileName} > /tmp/output 2>&1";

        await using var validator = new ContainerBuilder()
            .WithImage("python:3.11-slim")
            .WithCommand("sh", "-c", validatorCommand)
            .WithEnvironment("PIP_DISABLE_PIP_VERSION_CHECK", "1")
            .WithBindMount(hostDirectory, "/data", AccessMode.ReadOnly)
            .Build();

        await validator.StartAsync().ConfigureAwait(false);
        var exitCode = await validator.GetExitCodeAsync().ConfigureAwait(false);

        var logs = new StringBuilder();
        try
        {
            var logBytes = await validator.ReadFileAsync("/tmp/output").ConfigureAwait(false);
            logs.AppendLine(Encoding.UTF8.GetString(logBytes));
        }
        catch
        {
            // ignore missing logs
        }

        try
        {
            var pipLogBytes = await validator.ReadFileAsync("/tmp/pip.log").ConfigureAwait(false);
            logs.AppendLine(Encoding.UTF8.GetString(pipLogBytes));
        }
        catch
        {
            // ignore missing logs
        }

        exitCode.Should().Be(0, logs.ToString());
    }

    public async Task ValidateRasterAsync(string bucketName, string objectKey)
    {
        var internalEndpoint = _internalEndpoint ?? throw new InvalidOperationException("LocalStack endpoint not initialized.");
        var endpointUri = new Uri(internalEndpoint);
        var gdalEndpoint = $"{endpointUri.Host}:{endpointUri.Port}";
        var gdalCommand = $"set -euo pipefail; gdalinfo /vsis3/{bucketName}/{objectKey} > /tmp/output 2>&1";

        await using var rasterio = new ContainerBuilder()
            .WithImage("osgeo/gdal:ubuntu-small-3.6.3")
            .WithCommand("bash", "-lc", gdalCommand)
            .WithEnvironment("AWS_ACCESS_KEY_ID", AwsKey)
            .WithEnvironment("AWS_SECRET_ACCESS_KEY", AwsSecret)
            .WithEnvironment("AWS_SESSION_TOKEN", string.Empty)
            .WithEnvironment("AWS_DEFAULT_REGION", "us-east-1")
            .WithEnvironment("AWS_REGION", "us-east-1")
            .WithEnvironment("AWS_S3_ENDPOINT", gdalEndpoint)
            .WithEnvironment("AWS_S3_FORCE_PATH_STYLE", "true")
            .WithEnvironment("AWS_ACCESS_KEY", AwsKey)
            .WithEnvironment("AWS_SECRET_KEY", AwsSecret)
            .WithEnvironment("AWS_S3_ALLOW_UNSAFE_RENAME", "true")
            .WithEnvironment("AWS_REQUEST_PAYER", "requester")
            .WithEnvironment("AWS_VIRTUAL_HOSTING", "false")
            .WithEnvironment("AWS_HTTPS", "NO")
            .WithEnvironment("CPL_VSIL_CURL_ALLOWED_EXTENSIONS", ".tif")
            .WithNetwork(_network!)
            .Build();

        await rasterio.StartAsync().ConfigureAwait(false);
        var exitCode = await rasterio.GetExitCodeAsync().ConfigureAwait(false);

        var logs = new StringBuilder();
        try
        {
            var logBytes = await rasterio.ReadFileAsync("/tmp/output").ConfigureAwait(false);
            logs.AppendLine(Encoding.UTF8.GetString(logBytes));
        }
        catch
        {
            // ignore missing logs
        }

        exitCode.Should().Be(0, logs.ToString());
    }

    private static async Task EnsureImageAsync(DockerClient client, string image, CancellationToken ct = default)
    {
        try
        {
            await client.Images.InspectImageAsync(image, ct).ConfigureAwait(false);
        }
        catch (DockerImageNotFoundException)
        {
            var (repository, tag) = ParseImage(image);
            var parameters = new ImagesCreateParameters
            {
                FromImage = repository,
                Tag = tag
            };

            var progress = new Progress<JSONMessage>(_ => { });
            await client.Images.CreateImageAsync(parameters, null, progress, ct).ConfigureAwait(false);
        }
    }

    private static (string Repository, string Tag) ParseImage(string image)
    {
        var lastSlash = image.LastIndexOf('/');
        var lastColon = image.LastIndexOf(':');

        if (lastColon > -1 && lastColon > lastSlash)
        {
            return (image[..lastColon], image[(lastColon + 1)..]);
        }

        return (image, "latest");
    }
}

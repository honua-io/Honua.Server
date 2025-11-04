// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Configuration;

public sealed class ConfigurationLoader
{
    // Use centralized JsonHelper for consistent configuration loading
    private static readonly JsonSerializerOptions SerializerOptions = Utilities.JsonHelper.DefaultOptions;

    public HonuaConfiguration Load(string path)
    {
        if (path.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Configuration path must be provided", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Configuration file not found at '{path}'", path);
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();

        HonuaConfiguration config;
        if (extension == ".yaml" || extension == ".yml")
        {
            config = LoadYaml(path);
        }
        else if (extension == ".json")
        {
            config = LoadJson(path);
        }
        else
        {
            throw new ArgumentException($"Unsupported configuration file format: {extension}. Expected .json, .yaml, or .yml", nameof(path));
        }

        if (config.Metadata is null)
        {
            throw new InvalidDataException("Configuration must include metadata settings.");
        }

        var configDirectory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory();
        return Normalize(config, configDirectory);
    }

    private HonuaConfiguration LoadJson(string path)
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        if (!document.RootElement.TryGetProperty("honua", out var honuaElement))
        {
            throw new InvalidDataException("Configuration is missing required 'honua' section.");
        }

        return honuaElement.Deserialize<HonuaConfiguration>(SerializerOptions)
               ?? throw new InvalidDataException("Unable to deserialize Honua configuration.");
    }

    private HonuaConfiguration LoadYaml(string path)
    {
        var yamlContent = File.ReadAllText(path);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var data = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

        if (!data.ContainsKey("honua"))
        {
            throw new InvalidDataException("Configuration is missing required 'honua' section.");
        }

        // Convert YAML data to JSON and then deserialize to HonuaConfiguration
        var honuaData = data["honua"];
        var json = JsonSerializer.Serialize(honuaData);

        return JsonSerializer.Deserialize<HonuaConfiguration>(json, SerializerOptions)
               ?? throw new InvalidDataException("Unable to deserialize Honua configuration from YAML.");
    }

    public HonuaConfiguration Load(IConfiguration configuration, string? basePath = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var metadataSection = configuration.GetSection("metadata");
        if (!metadataSection.Exists())
        {
            throw new InvalidDataException("Configuration must include 'metadata' section.");
        }

        var metadata = new MetadataConfiguration
        {
            Provider = metadataSection["provider"] ?? string.Empty,
            Path = metadataSection["path"] ?? string.Empty
        };

        var odataSection = configuration.GetSection("odata");
        var odataDefaults = ODataConfiguration.Default;
        var odata = new ODataConfiguration
        {
            Enabled = odataSection.GetValue("enabled", odataDefaults.Enabled),
            AllowWrites = odataSection.GetValue("allowWrites", odataDefaults.AllowWrites),
            DefaultPageSize = odataSection.GetValue("defaultPageSize", odataDefaults.DefaultPageSize),
            MaxPageSize = odataSection.GetValue("maxPageSize", odataDefaults.MaxPageSize),
            EmitWktShadowProperties = odataSection.GetValue("emitWktShadowProperties", odataDefaults.EmitWktShadowProperties)
        };

        var servicesSection = configuration.GetSection("services");
        var wfsSection = servicesSection.GetSection("wfs");
        var wfsDefaults = WfsConfiguration.Default;
        var rasterSection = servicesSection.GetSection("rasterTiles");
        var rasterDefaults = RasterTileCacheConfiguration.Default;
        var fileSystemSection = rasterSection.GetSection("fileSystem");
        var s3Section = rasterSection.GetSection("s3");
        var azureSection = rasterSection.GetSection("azure");
        var preseedSection = rasterSection.GetSection("preseed");
        var geometrySection = servicesSection.GetSection("geometry");
        var geometryDefaults = GeometryServiceConfiguration.Default;
        var printSection = servicesSection.GetSection("print");
        var printDefaults = PrintServiceConfiguration.Default;

        var services = new ServicesConfiguration
        {
            OData = odata,
            Wfs = new WfsConfiguration
            {
                Enabled = wfsSection.GetValue("enabled", wfsDefaults.Enabled)
            },
            RasterTiles = new RasterTileCacheConfiguration
            {
                Enabled = rasterSection.GetValue("enabled", rasterDefaults.Enabled),
                Provider = rasterSection.GetValue("provider", rasterDefaults.Provider) ?? rasterDefaults.Provider,
                FileSystem = new RasterTileFileSystemConfiguration
                {
                    RootPath = fileSystemSection.GetValue("rootPath", rasterDefaults.FileSystem.RootPath)
                },
                S3 = new RasterTileS3Configuration
                {
                    BucketName = s3Section.GetValue<string?>("bucketName"),
                    Prefix = s3Section.GetValue<string?>("prefix"),
                    Region = s3Section.GetValue<string?>("region"),
                    ServiceUrl = s3Section.GetValue<string?>("serviceUrl"),
                    AccessKeyId = s3Section.GetValue<string?>("accessKeyId"),
                    SecretAccessKey = s3Section.GetValue<string?>("secretAccessKey"),
                    EnsureBucket = s3Section.GetValue("ensureBucket", rasterDefaults.S3.EnsureBucket),
                    ForcePathStyle = s3Section.GetValue("forcePathStyle", rasterDefaults.S3.ForcePathStyle)
                },
                Azure = new RasterTileAzureConfiguration
                {
                    ConnectionString = azureSection.GetValue<string?>("connectionString"),
                    ContainerName = azureSection.GetValue<string?>("containerName"),
                    EnsureContainer = azureSection.GetValue("ensureContainer", rasterDefaults.Azure.EnsureContainer)
                },
                Preseed = new RasterTilePreseedConfiguration
                {
                    BatchSize = preseedSection.GetValue("batchSize", rasterDefaults.Preseed.BatchSize),
                    MaxDegreeOfParallelism = preseedSection.GetValue("maxDegreeOfParallelism", rasterDefaults.Preseed.MaxDegreeOfParallelism)
                },
            },
            Geometry = new GeometryServiceConfiguration
            {
                Enabled = geometrySection.GetValue("enabled", geometryDefaults.Enabled),
                MaxGeometries = geometrySection.GetValue("maxGeometries", geometryDefaults.MaxGeometries),
                MaxCoordinateCount = geometrySection.GetValue("maxCoordinateCount", geometryDefaults.MaxCoordinateCount),
                EnableGdalOperations = geometrySection.GetValue("enableGdalOperations", geometryDefaults.EnableGdalOperations),
                AllowedSrids = geometrySection.GetSection("allowedSrids")
                    .GetChildren()
                    .Select(child => child.Get<int?>())
                    .Where(value => value.HasValue && value.Value > 0)
                    .Select(value => value!.Value)
                    .ToArray()
            },
            Print = new PrintServiceConfiguration
            {
                Enabled = printSection.GetValue("enabled", printDefaults.Enabled),
                Provider = printSection.GetValue("provider", printDefaults.Provider) ?? printDefaults.Provider,
                ConfigurationPath = printSection.GetValue<string?>("configurationPath")
            }
        };

        var attachments = BuildAttachmentConfiguration(configuration.GetSection("attachments"));

        var config = new HonuaConfiguration
        {
            Metadata = metadata,
            Services = services,
            Attachments = attachments
        };

        var root = basePath ?? Directory.GetCurrentDirectory();
        return Normalize(config, root);
    }

    private static AttachmentConfiguration BuildAttachmentConfiguration(IConfigurationSection? section)
    {
        var defaults = AttachmentConfiguration.Default;
        if (section is null || !section.Exists())
        {
            return defaults;
        }

        var profilesSection = section.GetSection("profiles");
        var profiles = new Dictionary<string, AttachmentStorageProfileConfiguration>(StringComparer.OrdinalIgnoreCase);
        foreach (var profileSection in profilesSection.GetChildren())
        {
            if (profileSection.Key.IsNullOrWhiteSpace())
            {
                continue;
            }

            var fileSystemSection = profileSection.GetSection("fileSystem");
            var s3Section = profileSection.GetSection("s3");
            var databaseSection = profileSection.GetSection("database");

            profiles[profileSection.Key] = new AttachmentStorageProfileConfiguration
            {
                Provider = profileSection.GetValue<string>("provider") ?? AttachmentStorageProfileConfiguration.Default.Provider,
                FileSystem = new AttachmentFileSystemStorageConfiguration
                {
                    RootPath = fileSystemSection.GetValue("rootPath", AttachmentFileSystemStorageConfiguration.Default.RootPath)
                },
                S3 = new AttachmentS3StorageConfiguration
                {
                    BucketName = s3Section.GetValue<string?>("bucketName"),
                    Prefix = s3Section.GetValue<string?>("prefix"),
                    Region = s3Section.GetValue<string?>("region"),
                    ServiceUrl = s3Section.GetValue<string?>("serviceUrl"),
                    AccessKeyId = s3Section.GetValue<string?>("accessKeyId"),
                    SecretAccessKey = s3Section.GetValue<string?>("secretAccessKey"),
                    ForcePathStyle = s3Section.GetValue("forcePathStyle", AttachmentS3StorageConfiguration.Default.ForcePathStyle),
                    UseInstanceProfile = s3Section.GetValue("useInstanceProfile", AttachmentS3StorageConfiguration.Default.UseInstanceProfile),
                    PresignExpirySeconds = s3Section.GetValue("presignExpirySeconds", AttachmentS3StorageConfiguration.Default.PresignExpirySeconds)
                },
                Database = new AttachmentDatabaseStorageConfiguration
                {
                    Provider = databaseSection.GetValue("provider", AttachmentDatabaseStorageConfiguration.Default.Provider) ?? AttachmentDatabaseStorageConfiguration.Default.Provider,
                    ConnectionString = databaseSection.GetValue<string?>("connectionString"),
                    Schema = databaseSection.GetValue<string?>("schema"),
                    TableName = databaseSection.GetValue<string?>("tableName"),
                    AttachmentIdColumn = databaseSection.GetValue("attachmentIdColumn", AttachmentDatabaseStorageConfiguration.Default.AttachmentIdColumn),
                    ContentColumn = databaseSection.GetValue("contentColumn", AttachmentDatabaseStorageConfiguration.Default.ContentColumn),
                    FileNameColumn = databaseSection.GetValue<string?>("fileNameColumn")
                }
            };
        }

        return new AttachmentConfiguration
        {
            DefaultMaxSizeMiB = section.GetValue("defaultMaxSizeMiB", defaults.DefaultMaxSizeMiB),
            Profiles = profiles
        };
    }

    private static HonuaConfiguration Normalize(HonuaConfiguration config, string basePath)
    {
        var normalizedProvider = config.Metadata.Provider?.Trim();
        if (normalizedProvider.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException("Metadata provider must be specified.");
        }

        var metadataPath = config.Metadata.Path ?? string.Empty;
        if (metadataPath.HasValue())
        {
            var providerIsFileBased = IsFileBasedMetadataProvider(normalizedProvider);

            if (providerIsFileBased)
            {
                metadataPath = Path.IsPathRooted(metadataPath)
                    ? Path.GetFullPath(metadataPath)
                    : Path.GetFullPath(Path.Combine(basePath, metadataPath));
            }
            else
            {
                metadataPath = metadataPath.Trim();
            }
        }

        return new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                Provider = normalizedProvider,
                Path = metadataPath
            },
            Services = NormalizeServices(config.Services, basePath),
            Attachments = NormalizeAttachments(config.Attachments, basePath)
        };
    }

    private static bool IsFileBasedMetadataProvider(string provider) =>
        provider.Equals("json", StringComparison.OrdinalIgnoreCase) ||
        provider.Equals("yaml", StringComparison.OrdinalIgnoreCase);

    private static ODataConfiguration NormalizeOData(ODataConfiguration? odata)
    {
        var odataDefaults = ODataConfiguration.Default;
        odata ??= odataDefaults;

        var maxPageSize = odata.MaxPageSize > 0 ? odata.MaxPageSize : odataDefaults.MaxPageSize;
        var defaultPageSize = odata.DefaultPageSize > 0 ? odata.DefaultPageSize : odataDefaults.DefaultPageSize;
        if (defaultPageSize > maxPageSize)
        {
            defaultPageSize = maxPageSize;
        }

        return new ODataConfiguration
        {
            Enabled = odata.Enabled,
            AllowWrites = odata.AllowWrites,
            DefaultPageSize = defaultPageSize,
            MaxPageSize = maxPageSize,
            EmitWktShadowProperties = odata.EmitWktShadowProperties
        };
    }

    private static ServicesConfiguration NormalizeServices(ServicesConfiguration? services, string basePath)
    {
        services ??= ServicesConfiguration.Default;

        return new ServicesConfiguration
        {
            OData = NormalizeOData(services.OData),
            Wfs = NormalizeWfs(services.Wfs),
            RasterTiles = NormalizeRasterTiles(services.RasterTiles, basePath),
            Stac = NormalizeStac(services.Stac, basePath),
            Geometry = NormalizeGeometry(services.Geometry),
            Print = NormalizePrint(services.Print, basePath)
        };
    }

    private static AttachmentConfiguration NormalizeAttachments(AttachmentConfiguration? attachments, string basePath)
    {
        attachments ??= AttachmentConfiguration.Default;

        var defaultMaxSize = attachments.DefaultMaxSizeMiB > 0
            ? attachments.DefaultMaxSizeMiB
            : AttachmentConfiguration.Default.DefaultMaxSizeMiB;

        var profiles = new Dictionary<string, AttachmentStorageProfileConfiguration>(StringComparer.OrdinalIgnoreCase);
        if (attachments.Profiles is not null)
        {
            foreach (var kvp in attachments.Profiles)
            {
                if (kvp.Key.IsNullOrWhiteSpace())
                {
                    continue;
                }

                profiles[kvp.Key] = NormalizeAttachmentProfile(kvp.Value, basePath);
            }
        }

        return new AttachmentConfiguration
        {
            DefaultMaxSizeMiB = defaultMaxSize,
            Profiles = profiles
        };
    }

    private static AttachmentStorageProfileConfiguration NormalizeAttachmentProfile(AttachmentStorageProfileConfiguration? profile, string basePath)
    {
        profile ??= AttachmentStorageProfileConfiguration.Default;

        var provider = profile.Provider.IsNullOrWhiteSpace()
            ? AttachmentStorageProfileConfiguration.Default.Provider
            : profile.Provider.Trim();

        return new AttachmentStorageProfileConfiguration
        {
            Provider = provider,
            FileSystem = NormalizeAttachmentFileSystem(profile.FileSystem, basePath),
            S3 = NormalizeAttachmentS3(profile.S3),
            Database = NormalizeAttachmentDatabase(profile.Database)
        };
    }

    private static AttachmentFileSystemStorageConfiguration NormalizeAttachmentFileSystem(AttachmentFileSystemStorageConfiguration? fileSystem, string basePath)
    {
        fileSystem ??= AttachmentFileSystemStorageConfiguration.Default;

        var rootPath = fileSystem.RootPath;
        if (rootPath.IsNullOrWhiteSpace())
        {
            rootPath = AttachmentFileSystemStorageConfiguration.Default.RootPath;
        }

        if (!Path.IsPathRooted(rootPath))
        {
            rootPath = Path.GetFullPath(Path.Combine(basePath, rootPath));
        }

        return new AttachmentFileSystemStorageConfiguration
        {
            RootPath = rootPath
        };
    }

    private static AttachmentS3StorageConfiguration NormalizeAttachmentS3(AttachmentS3StorageConfiguration? s3)
    {
        s3 ??= AttachmentS3StorageConfiguration.Default;

        var presignExpiry = s3.PresignExpirySeconds > 0
            ? s3.PresignExpirySeconds
            : AttachmentS3StorageConfiguration.Default.PresignExpirySeconds;

        return new AttachmentS3StorageConfiguration
        {
            BucketName = s3.BucketName.IsNullOrWhiteSpace() ? null : s3.BucketName.Trim(),
            Prefix = s3.Prefix.IsNullOrWhiteSpace() ? null : s3.Prefix.Trim().Trim('/'),
            Region = s3.Region.IsNullOrWhiteSpace() ? null : s3.Region.Trim(),
            ServiceUrl = s3.ServiceUrl.IsNullOrWhiteSpace() ? null : s3.ServiceUrl.Trim(),
            AccessKeyId = s3.AccessKeyId.IsNullOrWhiteSpace() ? null : s3.AccessKeyId.Trim(),
            SecretAccessKey = s3.SecretAccessKey.IsNullOrWhiteSpace() ? null : s3.SecretAccessKey.Trim(),
            ForcePathStyle = s3.ForcePathStyle,
            UseInstanceProfile = s3.UseInstanceProfile,
            PresignExpirySeconds = presignExpiry
        };
    }

    private static AttachmentDatabaseStorageConfiguration NormalizeAttachmentDatabase(AttachmentDatabaseStorageConfiguration? database)
    {
        database ??= AttachmentDatabaseStorageConfiguration.Default;

        return new AttachmentDatabaseStorageConfiguration
        {
            Provider = database.Provider.IsNullOrWhiteSpace() ? AttachmentDatabaseStorageConfiguration.Default.Provider : database.Provider.Trim(),
            ConnectionString = database.ConnectionString.IsNullOrWhiteSpace() ? null : database.ConnectionString.Trim(),
            Schema = database.Schema.IsNullOrWhiteSpace() ? null : database.Schema.Trim(),
            TableName = database.TableName.IsNullOrWhiteSpace() ? null : database.TableName.Trim(),
            AttachmentIdColumn = database.AttachmentIdColumn.IsNullOrWhiteSpace() ? AttachmentDatabaseStorageConfiguration.Default.AttachmentIdColumn : database.AttachmentIdColumn.Trim(),
            ContentColumn = database.ContentColumn.IsNullOrWhiteSpace() ? AttachmentDatabaseStorageConfiguration.Default.ContentColumn : database.ContentColumn.Trim(),
            FileNameColumn = database.FileNameColumn.IsNullOrWhiteSpace() ? null : database.FileNameColumn.Trim()
        };
    }

    private static PrintServiceConfiguration NormalizePrint(PrintServiceConfiguration? print, string basePath)
    {
        print ??= PrintServiceConfiguration.Default;

        var provider = print.Provider.IsNullOrWhiteSpace()
            ? PrintServiceConfiguration.Default.Provider
            : print.Provider.Trim();

        string? configurationPath = null;
        if (print.ConfigurationPath.HasValue())
        {
            configurationPath = print.ConfigurationPath.Trim();
            if (!Path.IsPathRooted(configurationPath))
            {
                configurationPath = Path.GetFullPath(Path.Combine(basePath, configurationPath));
            }
        }

        return new PrintServiceConfiguration
        {
            Enabled = print.Enabled,
            Provider = provider,
            ConfigurationPath = configurationPath
        };
    }

    private static WfsConfiguration NormalizeWfs(WfsConfiguration? wfs)
    {
        wfs ??= WfsConfiguration.Default;

        return new WfsConfiguration
        {
            Enabled = wfs.Enabled
        };
    }

    private static RasterTileCacheConfiguration NormalizeRasterTiles(RasterTileCacheConfiguration? rasterTiles, string basePath)
    {
        rasterTiles ??= RasterTileCacheConfiguration.Default;

        var provider = rasterTiles.Provider.IsNullOrWhiteSpace()
            ? RasterTileCacheConfiguration.Default.Provider
            : rasterTiles.Provider.Trim();

        var fileSystem = rasterTiles.FileSystem ?? RasterTileFileSystemConfiguration.Default;
        var rootPath = fileSystem.RootPath;
        if (rootPath.IsNullOrWhiteSpace())
        {
            rootPath = RasterTileFileSystemConfiguration.Default.RootPath;
        }

        if (!Path.IsPathRooted(rootPath))
        {
            rootPath = Path.GetFullPath(Path.Combine(basePath, rootPath));
        }

        var s3 = rasterTiles.S3 ?? RasterTileS3Configuration.Default;
        var azure = rasterTiles.Azure ?? RasterTileAzureConfiguration.Default;
        var preseed = rasterTiles.Preseed ?? RasterTilePreseedConfiguration.Default;

        return new RasterTileCacheConfiguration
        {
            Enabled = rasterTiles.Enabled,
            Provider = provider,
            FileSystem = new RasterTileFileSystemConfiguration
            {
                RootPath = rootPath
            },
            S3 = new RasterTileS3Configuration
            {
                BucketName = s3.BucketName,
                Prefix = s3.Prefix,
                Region = s3.Region,
                ServiceUrl = s3.ServiceUrl,
                AccessKeyId = s3.AccessKeyId,
                SecretAccessKey = s3.SecretAccessKey,
                EnsureBucket = s3.EnsureBucket,
                ForcePathStyle = s3.ForcePathStyle
            },
            Azure = new RasterTileAzureConfiguration
            {
                ConnectionString = azure.ConnectionString,
                ContainerName = azure.ContainerName.IsNullOrWhiteSpace() ? "raster-tiles" : azure.ContainerName,
                EnsureContainer = azure.EnsureContainer
            },
            Preseed = new RasterTilePreseedConfiguration
            {
                BatchSize = preseed.BatchSize > 0 ? preseed.BatchSize : RasterTilePreseedConfiguration.Default.BatchSize,
                MaxDegreeOfParallelism = preseed.MaxDegreeOfParallelism > 0 ? preseed.MaxDegreeOfParallelism : RasterTilePreseedConfiguration.Default.MaxDegreeOfParallelism
            }
        };
    }

    private static GeometryServiceConfiguration NormalizeGeometry(GeometryServiceConfiguration? geometry)
    {
        geometry ??= GeometryServiceConfiguration.Default;

        var maxGeometries = geometry.MaxGeometries > 0
            ? geometry.MaxGeometries
            : GeometryServiceConfiguration.Default.MaxGeometries;

        var maxCoordinateCount = geometry.MaxCoordinateCount > 0
            ? geometry.MaxCoordinateCount
            : GeometryServiceConfiguration.Default.MaxCoordinateCount;

        var allowedSrids = geometry.AllowedSrids is null
            ? Array.Empty<int>()
            : geometry.AllowedSrids
                .Where(srid => srid > 0)
                .Distinct()
                .OrderBy(srid => srid)
                .ToArray();

        return new GeometryServiceConfiguration
        {
            Enabled = geometry.Enabled,
            MaxGeometries = maxGeometries,
            MaxCoordinateCount = maxCoordinateCount,
            AllowedSrids = allowedSrids,
            EnableGdalOperations = geometry.EnableGdalOperations
        };
    }

    private static StacCatalogConfiguration NormalizeStac(StacCatalogConfiguration? stac, string basePath)
    {
        stac ??= StacCatalogConfiguration.Default;

        var provider = stac.Provider.IsNullOrWhiteSpace()
            ? StacCatalogConfiguration.Default.Provider
            : stac.Provider.Trim();

        string? resolvedConnectionString = stac.ConnectionString;
        string? resolvedFilePath = stac.FilePath;

        if (provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            if (resolvedFilePath.IsNullOrWhiteSpace())
            {
                resolvedFilePath = Path.Combine(basePath, "data", "stac-catalog.db");
            }
            else if (!Path.IsPathRooted(resolvedFilePath))
            {
                resolvedFilePath = Path.GetFullPath(Path.Combine(basePath, resolvedFilePath));
            }

            if (resolvedConnectionString.IsNullOrWhiteSpace())
            {
                resolvedConnectionString = $"Data Source={resolvedFilePath};Cache=Shared;Pooling=true";
            }
        }

        return new StacCatalogConfiguration
        {
            Enabled = stac.Enabled,
            Provider = provider,
            ConnectionString = resolvedConnectionString,
            FilePath = resolvedFilePath
        };
    }
}

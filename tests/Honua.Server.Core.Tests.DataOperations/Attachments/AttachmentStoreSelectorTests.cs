using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Attachments;

[Trait("Category", "Unit")]
public sealed class AttachmentStoreSelectorTests
{
    [Fact]
    public void Resolve_ShouldUseProviderRegisteredForProfile()
    {
        var configuration = CreateConfiguration();
        var configurationService = new TestConfigurationService(configuration);

        var filesystemProvider = new StubAttachmentStoreProvider(
            AttachmentStoreProviderKeys.FileSystem,
            (_, _) => new StubAttachmentStore("filesystem"));
        var s3Provider = new StubAttachmentStoreProvider(
            AttachmentStoreProviderKeys.S3,
            (_, profile) =>
            {
                profile.S3!.BucketName.Should().Be("attachments-bucket");
                return new StubAttachmentStore("s3");
            });
        var azureProvider = new StubAttachmentStoreProvider(
            AttachmentStoreProviderKeys.AzureBlob,
            (_, profile) =>
            {
                profile.Azure!.ContainerName.Should().Be("attachments");
                return new StubAttachmentStore("azure");
            });

        var selector = new AttachmentStoreSelector(
            configurationService,
            new IAttachmentStoreProvider[] { filesystemProvider, s3Provider, azureProvider },
            NullLogger<AttachmentStoreSelector>.Instance);

        var filesystemStoreFirst = selector.Resolve("local-files");
        filesystemStoreFirst.Should().BeOfType<StubAttachmentStore>()
            .Which.Provider.Should().Be("filesystem");
        filesystemProvider.CreateCallCount.Should().Be(1);

        // Cached instance should be reused
        selector.Resolve("local-files").Should().BeSameAs(filesystemStoreFirst);
        filesystemProvider.CreateCallCount.Should().Be(1);

        selector.Resolve("s3-sync").Should().BeOfType<StubAttachmentStore>()
            .Which.Provider.Should().Be("s3");
        s3Provider.CreateCallCount.Should().Be(1);

        selector.Resolve("azure-media").Should().BeOfType<StubAttachmentStore>()
            .Which.Provider.Should().Be("azure");
        azureProvider.CreateCallCount.Should().Be(1);
    }

    [Fact]
    public void Resolve_ShouldRebuildStores_AfterConfigurationChange()
    {
        var configuration = CreateConfiguration();
        var configurationService = new TestConfigurationService(configuration);

        var filesystemProvider = new StubAttachmentStoreProvider(
            AttachmentStoreProviderKeys.FileSystem,
            (_, _) => new StubAttachmentStore(Guid.NewGuid().ToString()));

        var selector = new AttachmentStoreSelector(
            configurationService,
            new IAttachmentStoreProvider[] { filesystemProvider },
            NullLogger<AttachmentStoreSelector>.Instance);

        var first = selector.Resolve("local-files");
        filesystemProvider.CreateCallCount.Should().Be(1);

        configuration.Attachments.Profiles["local-files"] = new AttachmentStorageProfileConfiguration
        {
            Provider = AttachmentStoreProviderKeys.FileSystem,
            FileSystem = new AttachmentFileSystemStorageConfiguration { RootPath = "data/attachments-v2" }
        };
        configurationService.Update(configuration);

        var second = selector.Resolve("local-files");
        filesystemProvider.CreateCallCount.Should().Be(2);
        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public void Resolve_ShouldThrow_WhenProviderNotRegistered()
    {
        var configuration = CreateConfiguration();
        configuration.Attachments.Profiles["database-store"] = new AttachmentStorageProfileConfiguration
        {
            Provider = AttachmentStoreProviderKeys.Database
        };

        var configurationService = new TestConfigurationService(configuration);
        var selector = new AttachmentStoreSelector(
            configurationService,
            new[] { new StubAttachmentStoreProvider(AttachmentStoreProviderKeys.FileSystem, (_, _) => new StubAttachmentStore("filesystem")) },
            NullLogger<AttachmentStoreSelector>.Instance);

        Action act = () => selector.Resolve("database-store");
        act.Should().Throw<AttachmentStoreNotFoundException>();
    }

    private static HonuaConfiguration CreateConfiguration()
    {
        return new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                Provider = "json",
                Path = "metadata.json"
            },
            Attachments = new AttachmentConfiguration
            {
                Profiles = new Dictionary<string, AttachmentStorageProfileConfiguration>(StringComparer.OrdinalIgnoreCase)
                {
                    ["local-files"] = new AttachmentStorageProfileConfiguration
                    {
                        Provider = AttachmentStoreProviderKeys.FileSystem,
                        FileSystem = new AttachmentFileSystemStorageConfiguration { RootPath = "data/attachments" }
                    },
                    ["s3-sync"] = new AttachmentStorageProfileConfiguration
                    {
                        Provider = AttachmentStoreProviderKeys.S3,
                        S3 = new AttachmentS3StorageConfiguration
                        {
                            BucketName = "attachments-bucket",
                            UseInstanceProfile = false,
                            AccessKeyId = "access",
                            SecretAccessKey = "secret"
                        }
                    },
                    ["azure-media"] = new AttachmentStorageProfileConfiguration
                    {
                        Provider = AttachmentStoreProviderKeys.AzureBlob,
                        Azure = new AttachmentAzureBlobStorageConfiguration
                        {
                            ConnectionString = "UseDevelopmentStorage=true",
                            ContainerName = "attachments"
                        }
                    }
                }
            }
        };
    }

    private sealed class StubAttachmentStoreProvider : IAttachmentStoreProvider
    {
        private readonly Func<string, AttachmentStorageProfileConfiguration, IAttachmentStore> _factory;

        public StubAttachmentStoreProvider(string providerKey, Func<string, AttachmentStorageProfileConfiguration, IAttachmentStore> factory)
        {
            ProviderKey = providerKey;
            _factory = factory;
        }

        public string ProviderKey { get; }
        public int CreateCallCount { get; private set; }

        public IAttachmentStore Create(string profileId, AttachmentStorageProfileConfiguration profileConfiguration)
        {
            CreateCallCount++;
            return _factory(profileId, profileConfiguration);
        }
    }

    private sealed class StubAttachmentStore : IAttachmentStore
    {
        public StubAttachmentStore(string provider)
        {
            Provider = provider;
        }

        public string Provider { get; }

        public Task<AttachmentStoreWriteResult> PutAsync(Stream content, AttachmentStorePutRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AttachmentReadResult?> TryGetAsync(AttachmentPointer pointer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(AttachmentPointer pointer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<AttachmentPointer> ListAsync(string? prefix = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestConfigurationService : IHonuaConfigurationService
    {
        private HonuaConfiguration _configuration;
        private CancellationTokenSource _cts = new();

        public TestConfigurationService(HonuaConfiguration configuration)
        {
            _configuration = configuration;
        }

        public HonuaConfiguration Current => _configuration;

        public IChangeToken GetChangeToken()
        {
            return new CancellationChangeToken(_cts.Token);
        }

        public void Update(HonuaConfiguration configuration)
        {
            _configuration = configuration;
            var previous = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
            previous.Cancel();
            previous.Dispose();
        }
    }
}

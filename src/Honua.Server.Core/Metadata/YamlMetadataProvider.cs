// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Provides metadata from a YAML file with optional file watching for hot-reload.
/// </summary>
public sealed class YamlMetadataProvider : IReloadableMetadataProvider, IDisposable
{
    private readonly string _metadataPath;
    private readonly FileSystemWatcher? _watcher;
    private readonly bool _watchForChanges;

    public bool SupportsChangeNotifications => _watchForChanges;
    public event EventHandler<MetadataChangedEventArgs>? MetadataChanged;

    public YamlMetadataProvider(string metadataPath, bool watchForChanges = false)
    {
        if (metadataPath.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Metadata path must be provided", nameof(metadataPath));
        }

        _metadataPath = Path.GetFullPath(metadataPath);
        _watchForChanges = watchForChanges;

        if (_watchForChanges && File.Exists(_metadataPath))
        {
            var directory = Path.GetDirectoryName(_metadataPath);
            var fileName = Path.GetFileName(_metadataPath);

            if (!directory.IsNullOrEmpty() && !fileName.IsNullOrEmpty())
            {
                _watcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
                };

                _watcher.Changed += OnFileChanged;
                _watcher.Created += OnFileChanged;
                _watcher.Renamed += OnFileChanged;
                _watcher.EnableRaisingEvents = true;
            }
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        MetadataChanged?.Invoke(this, new MetadataChangedEventArgs("file-watcher"));
    }

    public async Task<MetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        var yaml = await FileOperationHelper.SafeReadAllTextAsync(_metadataPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        return Parse(yaml);
    }

    public static MetadataSnapshot Parse(string yaml)
    {
        if (yaml.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException("Metadata payload is empty.");
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            // Deserialize YAML to the same document structure as JSON
            var yamlObject = deserializer.Deserialize(yaml);

            // Convert to JSON and use JsonMetadataProvider's parser
            var serializer = new SerializerBuilder()
                .JsonCompatible()
                .Build();

            var json = serializer.Serialize(yamlObject);
            return JsonMetadataProvider.Parse(json);
        }
        catch (YamlDotNet.Core.YamlException yamlEx)
        {
            throw new InvalidDataException($"Metadata file contains invalid YAML: {yamlEx.Message}", yamlEx);
        }
    }

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        // Trigger a metadata changed event to force reload
        MetadataChanged?.Invoke(this, new MetadataChangedEventArgs("manual-reload"));
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_watcher is not null)
        {
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileChanged;
            _watcher.Renamed -= OnFileChanged;
            _watcher.Dispose();
        }
    }
}

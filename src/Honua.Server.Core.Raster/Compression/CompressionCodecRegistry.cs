// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Raster.Compression;

/// <summary>
/// Registry for compression codecs used in raster data formats.
/// Manages available codecs and provides lookup by name.
/// </summary>
public sealed class CompressionCodecRegistry
{
    private readonly Dictionary<string, ICompressionCodec> codecs;
    private readonly ILogger<CompressionCodecRegistry> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompressionCodecRegistry"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public CompressionCodecRegistry(ILogger<CompressionCodecRegistry> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.codecs = new Dictionary<string, ICompressionCodec>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Registers a compression codec.
    /// </summary>
    /// <param name="codec">The codec to register.</param>
    /// <exception cref="ArgumentNullException">If codec is null.</exception>
    /// <exception cref="InvalidOperationException">If a codec with the same name is already registered.</exception>
    public void Register(ICompressionCodec codec)
    {
        Guard.NotNull(codec);

        var codecName = codec.CodecName.ToLowerInvariant();

        if (this.codecs.ContainsKey(codecName))
        {
            throw new InvalidOperationException($"Codec '{codecName}' is already registered");
        }

        this.codecs[codecName] = codec;
        this.logger.LogInformation("Registered compression codec: {CodecName} (Support: {SupportLevel})",
            codec.CodecName, codec.GetSupportLevel());
    }

    /// <summary>
    /// Gets a codec by name.
    /// </summary>
    /// <param name="codecName">The codec name (case-insensitive).</param>
    /// <returns>The codec instance.</returns>
    /// <exception cref="NotSupportedException">If the codec is not registered.</exception>
    public ICompressionCodec GetCodec(string codecName)
    {
        if (string.IsNullOrWhiteSpace(codecName) || codecName.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Codec name cannot be null or empty", nameof(codecName));
        }

        var key = codecName.ToLowerInvariant();

        if (!this.codecs.TryGetValue(key, out var codec))
        {
            var availableCodecs = string.Join(", ", this.codecs.Keys);
            throw new NotSupportedException(
                $"Compression codec '{codecName}' is not supported. Available codecs: {availableCodecs}");
        }

        return codec;
    }

    /// <summary>
    /// Checks if a codec is registered.
    /// </summary>
    /// <param name="codecName">The codec name (case-insensitive).</param>
    /// <returns>True if the codec is registered, false otherwise.</returns>
    public bool IsSupported(string codecName)
    {
        if (string.IsNullOrWhiteSpace(codecName) || codecName.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return true; // No compression
        }

        return this.codecs.ContainsKey(codecName.ToLowerInvariant());
    }

    /// <summary>
    /// Gets all registered codec names.
    /// </summary>
    public IReadOnlyCollection<string> GetRegisteredCodecs()
    {
        return this.codecs.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets detailed information about all registered codecs.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetCodecSupportLevels()
    {
        return this.codecs.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetSupportLevel(),
            StringComparer.OrdinalIgnoreCase);
    }
}

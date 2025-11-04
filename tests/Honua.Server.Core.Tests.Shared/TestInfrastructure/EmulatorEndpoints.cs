using System;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Provides emulator endpoint URLs that work both in devcontainer and local environments.
/// </summary>
public static class EmulatorEndpoints
{
    /// <summary>
    /// LocalStack (S3) endpoint URL.
    /// Uses service name in devcontainer, localhost otherwise.
    /// </summary>
    public static string LocalStack => Environment.GetEnvironmentVariable("LOCALSTACK_ENDPOINT") ?? "http://localhost:4566";

    /// <summary>
    /// Azurite (Azure Blob) endpoint URL.
    /// Uses service name in devcontainer, localhost otherwise.
    /// </summary>
    public static string Azurite => Environment.GetEnvironmentVariable("AZURITE_BLOB_ENDPOINT") ?? "http://localhost:10000";

    /// <summary>
    /// GCS emulator endpoint URL.
    /// Uses service name in devcontainer, localhost otherwise.
    /// </summary>
    public static string GcsEmulator => Environment.GetEnvironmentVariable("GCS_EMULATOR_ENDPOINT") ?? "http://localhost:4443";

    /// <summary>
    /// AWS credentials for LocalStack.
    /// </summary>
    public static class Aws
    {
        public static string AccessKeyId => Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "test";
        public static string SecretAccessKey => Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "test";
        public static string Region => Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ?? "us-east-1";
    }

    /// <summary>
    /// Azure Storage emulator connection string for Azurite.
    /// </summary>
    public static string AzuriteConnectionString
    {
        get
        {
            var endpoint = Azurite.Replace("http://", "").Replace("https://", "");
            return $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint={Azurite}/devstoreaccount1;";
        }
    }

    /// <summary>
    /// Checks if running inside devcontainer by looking for emulator environment variables.
    /// </summary>
    public static bool IsDevContainer => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOCALSTACK_ENDPOINT"));
}

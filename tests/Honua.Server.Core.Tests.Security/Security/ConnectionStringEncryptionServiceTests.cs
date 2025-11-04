using System;
using System.Threading.Tasks;
using Honua.Server.Core.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Security;

[Trait("Category", "Integration")]
public sealed class ConnectionStringEncryptionServiceTests
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<ConnectionStringEncryptionService> _logger;

    public ConnectionStringEncryptionServiceTests()
    {
        _dataProtectionProvider = DataProtectionProvider.Create("Honua.Tests");
        _logger = new LoggerFactory().CreateLogger<ConnectionStringEncryptionService>();
    }

    [Fact]
    public async Task EncryptAsync_ValidConnectionString_ReturnsEncryptedValue()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);
        var plainText = "Server=localhost;Database=test;User Id=admin;Password=secret;";

        // Act
        var encrypted = await service.EncryptAsync(plainText);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEqual(plainText, encrypted);
        Assert.StartsWith("ENC:", encrypted);
    }

    [Fact]
    public async Task DecryptAsync_EncryptedValue_ReturnsOriginalValue()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);
        var plainText = "Server=localhost;Database=test;User Id=admin;Password=secret;";

        // Act
        var encrypted = await service.EncryptAsync(plainText);
        var decrypted = await service.DecryptAsync(encrypted);

        // Assert
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public async Task DecryptAsync_UnencryptedValue_ReturnsOriginalValue()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);
        var plainText = "Server=localhost;Database=test;";

        // Act
        var result = await service.DecryptAsync(plainText);

        // Assert
        Assert.Equal(plainText, result);
    }

    [Fact]
    public void IsEncrypted_EncryptedValue_ReturnsTrue()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);
        var encryptedValue = "ENC:c29tZWVuY3J5cHRlZGRhdGE=";

        // Act
        var result = service.IsEncrypted(encryptedValue);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsEncrypted_PlainTextValue_ReturnsFalse()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);
        var plainText = "Server=localhost;Database=test;";

        // Act
        var result = service.IsEncrypted(plainText);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsEncrypted_NullOrEmpty_ReturnsFalse()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);

        // Act & Assert
        Assert.False(service.IsEncrypted(null!));
        Assert.False(service.IsEncrypted(string.Empty));
        Assert.False(service.IsEncrypted("   "));
    }

    [Fact]
    public async Task EncryptAsync_AlreadyEncrypted_ReturnsAsIs()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);
        var plainText = "Server=localhost;Database=test;";
        var encrypted = await service.EncryptAsync(plainText);

        // Act
        var reencrypted = await service.EncryptAsync(encrypted);

        // Assert
        Assert.Equal(encrypted, reencrypted);
    }

    [Fact]
    public async Task EncryptAsync_WhenDisabled_ReturnsPlainText()
    {
        // Arrange
        var options = CreateOptions(enabled: false);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);
        var plainText = "Server=localhost;Database=test;";

        // Act
        var result = await service.EncryptAsync(plainText);

        // Assert
        Assert.Equal(plainText, result);
    }

    [Fact]
    public async Task DecryptAsync_WhenDisabledWithEncryptedValue_ThrowsException()
    {
        // Arrange
        var enabledOptions = CreateOptions(enabled: true);
        var enabledService = new ConnectionStringEncryptionService(_dataProtectionProvider, enabledOptions, _logger);
        var plainText = "Server=localhost;Database=test;";
        var encrypted = await enabledService.EncryptAsync(plainText);

        var disabledOptions = CreateOptions(enabled: false);
        var disabledService = new ConnectionStringEncryptionService(_dataProtectionProvider, disabledOptions, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => disabledService.DecryptAsync(encrypted));
    }

    [Fact]
    public async Task RotateKeyAsync_ValidEncryptedValue_ReturnsReencryptedValue()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);
        var plainText = "Server=localhost;Database=test;";
        var encrypted = await service.EncryptAsync(plainText);

        // Act
        var rotated = await service.RotateKeyAsync(encrypted);

        // Assert
        Assert.NotNull(rotated);
        Assert.StartsWith("ENC:", rotated);

        // Verify the rotated value can be decrypted to original
        var decrypted = await service.DecryptAsync(rotated);
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public async Task RotateKeyAsync_PlainTextValue_ThrowsException()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);
        var plainText = "Server=localhost;Database=test;";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.RotateKeyAsync(plainText));
    }

    [Fact]
    public async Task RotateKeyAsync_WhenDisabled_ThrowsException()
    {
        // Arrange
        var enabledOptions = CreateOptions(enabled: true);
        var enabledService = new ConnectionStringEncryptionService(_dataProtectionProvider, enabledOptions, _logger);
        var plainText = "Server=localhost;Database=test;";
        var encrypted = await enabledService.EncryptAsync(plainText);

        var disabledOptions = CreateOptions(enabled: false);
        var disabledService = new ConnectionStringEncryptionService(_dataProtectionProvider, disabledOptions, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => disabledService.RotateKeyAsync(encrypted));
    }

    [Fact]
    public async Task EncryptAsync_NullOrEmpty_ThrowsException()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.EncryptAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => service.EncryptAsync(string.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => service.EncryptAsync("   "));
    }

    [Fact]
    public async Task DecryptAsync_NullOrEmpty_ThrowsException()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.DecryptAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => service.DecryptAsync(string.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => service.DecryptAsync("   "));
    }

    [Fact]
    public async Task EncryptDecrypt_ComplexConnectionString_PreservesValue()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);
        var complexConnectionString = "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;Trusted_Connection=False;Encrypt=True;";

        // Act
        var encrypted = await service.EncryptAsync(complexConnectionString);
        var decrypted = await service.DecryptAsync(encrypted);

        // Assert
        Assert.Equal(complexConnectionString, decrypted);
    }

    [Fact]
    public async Task EncryptDecrypt_PostgresConnectionString_PreservesValue()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);
        var postgresConnectionString = "Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=secret123;";

        // Act
        var encrypted = await service.EncryptAsync(postgresConnectionString);
        var decrypted = await service.DecryptAsync(encrypted);

        // Assert
        Assert.Equal(postgresConnectionString, decrypted);
    }

    [Fact]
    public async Task EncryptDecrypt_MySqlConnectionString_PreservesValue()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = new ConnectionStringEncryptionService(_dataProtectionProvider, options, _logger);
        var mysqlConnectionString = "Server=myServerAddress;Database=myDataBase;Uid=myUsername;Pwd=myPassword;";

        // Act
        var encrypted = await service.EncryptAsync(mysqlConnectionString);
        var decrypted = await service.DecryptAsync(encrypted);

        // Assert
        Assert.Equal(mysqlConnectionString, decrypted);
    }

    private static IOptions<ConnectionStringEncryptionOptions> CreateOptions(bool enabled)
    {
        var options = new ConnectionStringEncryptionOptions
        {
            Enabled = enabled,
            KeyStorageProvider = "FileSystem",
            ApplicationName = "Honua.Tests"
        };
        return Options.Create(options);
    }
}

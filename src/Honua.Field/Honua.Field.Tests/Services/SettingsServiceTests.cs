// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using HonuaField.Services;
using Xunit;

namespace HonuaField.Tests.Services;

/// <summary>
/// Unit tests for SettingsService
/// Tests secure storage and preferences management
/// </summary>
public class SettingsServiceTests
{
	private readonly SettingsService _settingsService;

	public SettingsServiceTests()
	{
		_settingsService = new SettingsService();
	}

	[Fact]
	public async Task SetAsync_AndGetAsync_String_ShouldWorkCorrectly()
	{
		// Arrange
		var key = "test_string_key";
		var value = "test_value";

		// Act
		await _settingsService.SetAsync(key, value);
		var result = await _settingsService.GetAsync<string>(key);

		// Assert
		result.Should().Be(value);

		// Cleanup
		await _settingsService.RemoveAsync(key);
	}

	[Fact]
	public async Task SetAsync_AndGetAsync_Int_ShouldWorkCorrectly()
	{
		// Arrange
		var key = "test_int_key";
		var value = 42;

		// Act
		await _settingsService.SetAsync(key, value);
		var result = await _settingsService.GetAsync<int>(key);

		// Assert
		result.Should().Be(value);

		// Cleanup
		await _settingsService.RemoveAsync(key);
	}

	[Fact]
	public async Task SetAsync_AndGetAsync_Bool_ShouldWorkCorrectly()
	{
		// Arrange
		var key = "test_bool_key";
		var value = true;

		// Act
		await _settingsService.SetAsync(key, value);
		var result = await _settingsService.GetAsync<bool>(key);

		// Assert
		result.Should().Be(value);

		// Cleanup
		await _settingsService.RemoveAsync(key);
	}

	[Fact]
	public async Task GetAsync_ShouldReturnDefaultValue_WhenKeyDoesNotExist()
	{
		// Arrange
		var key = "non_existent_key";
		var defaultValue = "default";

		// Act
		var result = await _settingsService.GetAsync(key, defaultValue);

		// Assert
		result.Should().Be(defaultValue);
	}

	[Fact]
	public async Task RemoveAsync_ShouldRemoveKey()
	{
		// Arrange
		var key = "test_remove_key";
		var value = "test_value";
		await _settingsService.SetAsync(key, value);

		// Act
		await _settingsService.RemoveAsync(key);
		var result = await _settingsService.GetAsync<string>(key);

		// Assert
		result.Should().BeNullOrEmpty();
	}

	[Fact]
	public async Task SetAsync_AndGetAsync_ComplexObject_ShouldWorkCorrectly()
	{
		// Arrange
		var key = "test_object_key";
		var value = new TestObject
		{
			Id = 1,
			Name = "Test",
			IsActive = true
		};

		// Act
		await _settingsService.SetAsync(key, value);
		var result = await _settingsService.GetAsync<TestObject>(key);

		// Assert
		result.Should().NotBeNull();
		result!.Id.Should().Be(value.Id);
		result.Name.Should().Be(value.Name);
		result.IsActive.Should().Be(value.IsActive);

		// Cleanup
		await _settingsService.RemoveAsync(key);
	}

	[Fact]
	public async Task ClearAllAsync_ShouldRemoveAllSettings()
	{
		// Arrange
		var key1 = "test_key_1";
		var key2 = "test_key_2";
		await _settingsService.SetAsync(key1, "value1");
		await _settingsService.SetAsync(key2, "value2");

		// Act
		await _settingsService.ClearAllAsync();

		// Assert
		var result1 = await _settingsService.GetAsync<string>(key1);
		var result2 = await _settingsService.GetAsync<string>(key2);
		result1.Should().BeNullOrEmpty();
		result2.Should().BeNullOrEmpty();
	}

	// Test object for complex object serialization tests
	private class TestObject
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public bool IsActive { get; set; }
	}
}

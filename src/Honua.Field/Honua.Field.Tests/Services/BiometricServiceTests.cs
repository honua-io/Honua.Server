// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using HonuaField.Services;
using Xunit;

namespace HonuaField.Tests.Services;

/// <summary>
/// Unit tests for BiometricService
/// Tests biometric authentication interface and error handling
/// </summary>
public class BiometricServiceTests
{
	private readonly BiometricService _biometricService;

	public BiometricServiceTests()
	{
		_biometricService = new BiometricService();
	}

	[Fact]
	public async Task IsBiometricAvailableAsync_ShouldNotThrow()
	{
		// Act
		Func<Task> act = async () => await _biometricService.IsBiometricAvailableAsync();

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task GetBiometricTypeAsync_ShouldReturnValidType()
	{
		// Act
		var result = await _biometricService.GetBiometricTypeAsync();

		// Assert
		result.Should().BeOneOf(
			BiometricType.None,
			BiometricType.Fingerprint,
			BiometricType.FaceId,
			BiometricType.TouchId,
			BiometricType.Iris,
			BiometricType.Voice
		);
	}

	[Fact]
	public async Task IsBiometricEnrolledAsync_ShouldNotThrow()
	{
		// Act
		Func<Task> act = async () => await _biometricService.IsBiometricEnrolledAsync();

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task AuthenticateAsync_ShouldReturnBiometricResult()
	{
		// Act
		var result = await _biometricService.AuthenticateAsync("Test authentication");

		// Assert
		result.Should().NotBeNull();
		result.Should().BeOfType<BiometricResult>();
	}

	[Fact]
	public async Task AuthenticateAsync_ShouldReturnFailed_WhenBiometricNotAvailable()
	{
		// Arrange
		var isAvailable = await _biometricService.IsBiometricAvailableAsync();

		// Act
		var result = await _biometricService.AuthenticateAsync("Test authentication");

		// Assert
		if (!isAvailable)
		{
			result.Success.Should().BeFalse();
			result.ErrorType.Should().Be(BiometricErrorType.NotAvailable);
		}
	}

	[Fact]
	public void BiometricResult_Successful_ShouldCreateSuccessResult()
	{
		// Act
		var result = BiometricResult.Successful();

		// Assert
		result.Success.Should().BeTrue();
		result.ErrorMessage.Should().BeNull();
		result.ErrorType.Should().Be(BiometricErrorType.Unknown);
	}

	[Fact]
	public void BiometricResult_Failed_ShouldCreateFailureResult()
	{
		// Arrange
		var errorMessage = "Authentication failed";
		var errorType = BiometricErrorType.Failed;

		// Act
		var result = BiometricResult.Failed(errorMessage, errorType);

		// Assert
		result.Success.Should().BeFalse();
		result.ErrorMessage.Should().Be(errorMessage);
		result.ErrorType.Should().Be(errorType);
	}

	[Theory]
	[InlineData(BiometricErrorType.NotAvailable)]
	[InlineData(BiometricErrorType.NotEnrolled)]
	[InlineData(BiometricErrorType.Locked)]
	[InlineData(BiometricErrorType.UserCanceled)]
	[InlineData(BiometricErrorType.Failed)]
	public void BiometricResult_Failed_ShouldHandleAllErrorTypes(BiometricErrorType errorType)
	{
		// Act
		var result = BiometricResult.Failed("Test error", errorType);

		// Assert
		result.Success.Should().BeFalse();
		result.ErrorType.Should().Be(errorType);
	}

	[Fact]
	public void BiometricType_ShouldHaveExpectedValues()
	{
		// Assert
		Enum.IsDefined(typeof(BiometricType), BiometricType.None).Should().BeTrue();
		Enum.IsDefined(typeof(BiometricType), BiometricType.Fingerprint).Should().BeTrue();
		Enum.IsDefined(typeof(BiometricType), BiometricType.FaceId).Should().BeTrue();
		Enum.IsDefined(typeof(BiometricType), BiometricType.TouchId).Should().BeTrue();
		Enum.IsDefined(typeof(BiometricType), BiometricType.Iris).Should().BeTrue();
		Enum.IsDefined(typeof(BiometricType), BiometricType.Voice).Should().BeTrue();
	}

	[Fact]
	public void BiometricErrorType_ShouldHaveExpectedValues()
	{
		// Assert
		Enum.IsDefined(typeof(BiometricErrorType), BiometricErrorType.Unknown).Should().BeTrue();
		Enum.IsDefined(typeof(BiometricErrorType), BiometricErrorType.NotAvailable).Should().BeTrue();
		Enum.IsDefined(typeof(BiometricErrorType), BiometricErrorType.NotEnrolled).Should().BeTrue();
		Enum.IsDefined(typeof(BiometricErrorType), BiometricErrorType.Locked).Should().BeTrue();
		Enum.IsDefined(typeof(BiometricErrorType), BiometricErrorType.UserCanceled).Should().BeTrue();
		Enum.IsDefined(typeof(BiometricErrorType), BiometricErrorType.SystemCanceled).Should().BeTrue();
		Enum.IsDefined(typeof(BiometricErrorType), BiometricErrorType.Failed).Should().BeTrue();
		Enum.IsDefined(typeof(BiometricErrorType), BiometricErrorType.PasscodeFallback).Should().BeTrue();
	}
}

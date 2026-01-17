using PRAgent.Models;
using PRAgent.Validators;

namespace PRAgent.Tests;

public class ConfigValidatorTests
{
    [Fact]
    public void ValidateAISettings_ValidSettings_ReturnsTrue()
    {
        // Arrange
        var settings = new AISettings
        {
            Endpoint = "https://api.openai.com/v1",
            ApiKey = "test-key",
            ModelId = "gpt-4o-mini"
        };
        var errors = new List<string>();

        // Act
        var result = ConfigValidator.ValidateAISettings(settings, errors);

        // Assert
        Assert.True(result);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData("invalid-url", "key", "model")]
    public void ValidateAISettings_InvalidSettings_ReturnsFalse(string endpoint, string apiKey, string modelId)
    {
        // Arrange
        var settings = new AISettings
        {
            Endpoint = endpoint,
            ApiKey = apiKey,
            ModelId = modelId
        };
        var errors = new List<string>();

        // Act
        var result = ConfigValidator.ValidateAISettings(settings, errors);

        // Assert
        Assert.False(result);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ValidateAISettings_MissingEndpoint_ReturnsFalse()
    {
        // Arrange
        var settings = new AISettings
        {
            Endpoint = "",
            ApiKey = "test-key",
            ModelId = "gpt-4o-mini"
        };
        var errors = new List<string>();

        // Act
        var result = ConfigValidator.ValidateAISettings(settings, errors);

        // Assert
        Assert.False(result);
        Assert.Contains("AI Endpoint is required.", errors);
    }

    [Fact]
    public void ValidateAISettings_InvalidEndpointUri_ReturnsFalse()
    {
        // Arrange
        var settings = new AISettings
        {
            Endpoint = "not-a-valid-uri",
            ApiKey = "test-key",
            ModelId = "gpt-4o-mini"
        };
        var errors = new List<string>();

        // Act
        var result = ConfigValidator.ValidateAISettings(settings, errors);

        // Assert
        Assert.False(result);
        // Uri.TryCreate may accept relative URIs, so we check if validation fails
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ValidatePRSettings_ValidSettings_ReturnsTrue()
    {
        // Arrange
        var settings = new PRSettings
        {
            GitHubToken = "ghp_test_token"
        };
        var errors = new List<string>();

        // Act
        var result = ConfigValidator.ValidatePRSettings(settings, errors);

        // Assert
        Assert.True(result);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidatePRSettings_MissingToken_ReturnsFalse()
    {
        // Arrange
        var settings = new PRSettings
        {
            GitHubToken = ""
        };
        var errors = new List<string>();

        // Act
        var result = ConfigValidator.ValidatePRSettings(settings, errors);

        // Assert
        Assert.False(result);
        Assert.Contains("GitHub Token is required.", errors);
    }

    [Theory]
    [InlineData("critical", true)]
    [InlineData("major", true)]
    [InlineData("minor", true)]
    [InlineData("none", true)]
    [InlineData("invalid", false)]
    [InlineData("", false)]
    public void ValidateApprovalThreshold_ValidInput_ReturnsExpected(string threshold, bool expectedValid)
    {
        // Arrange
        var errors = new List<string>();

        // Act
        var result = ConfigValidator.ValidateApprovalThreshold(threshold, errors);

        // Assert
        Assert.Equal(expectedValid, result);
    }
}

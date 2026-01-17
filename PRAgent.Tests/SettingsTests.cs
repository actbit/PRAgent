using PRAgent.Models;

namespace PRAgent.Tests;

public class SettingsTests
{
    [Fact]
    public void AISettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new AISettings();

        // Assert
        Assert.Equal("https://api.openai.com/v1", settings.Endpoint);
        Assert.Equal("gpt-4o-mini", settings.ModelId);
        Assert.Equal(4000, settings.MaxTokens);
        Assert.Equal(0.7, settings.Temperature);
    }

    [Fact]
    public void AISettings_CanSetProperties()
    {
        // Arrange
        var settings = new AISettings();

        // Act
        settings.Endpoint = "https://custom.endpoint.com";
        settings.ApiKey = "test-key-123";
        settings.ModelId = "gpt-4o";
        settings.MaxTokens = 8000;
        settings.Temperature = 0.5;

        // Assert
        Assert.Equal("https://custom.endpoint.com", settings.Endpoint);
        Assert.Equal("test-key-123", settings.ApiKey);
        Assert.Equal("gpt-4o", settings.ModelId);
        Assert.Equal(8000, settings.MaxTokens);
        Assert.Equal(0.5, settings.Temperature);
    }

    [Fact]
    public void PRSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new PRSettings();

        // Assert
        Assert.Equal(string.Empty, settings.GitHubToken);
        Assert.Equal(string.Empty, settings.DefaultOwner);
        Assert.Equal(string.Empty, settings.DefaultRepo);
    }

    [Fact]
    public void PRSettings_CanSetProperties()
    {
        // Arrange
        var settings = new PRSettings();

        // Act
        settings.GitHubToken = "ghp_test_token";
        settings.DefaultOwner = "test-owner";
        settings.DefaultRepo = "test-repo";

        // Assert
        Assert.Equal("ghp_test_token", settings.GitHubToken);
        Assert.Equal("test-owner", settings.DefaultOwner);
        Assert.Equal("test-repo", settings.DefaultRepo);
    }

    [Fact]
    public void AISettings_SectionName_IsCorrect()
    {
        // Act & Assert
        Assert.Equal("AISettings", AISettings.SectionName);
    }

    [Fact]
    public void PRSettings_SectionName_IsCorrect()
    {
        // Act & Assert
        Assert.Equal("PRSettings", PRSettings.SectionName);
    }
}

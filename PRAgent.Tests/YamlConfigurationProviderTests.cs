using PRAgent.Configuration;

namespace PRAgent.Tests;

public class YamlConfigurationProviderTests
{
    [Fact]
    public void Deserialize_ValidYaml_ReturnsConfig()
    {
        // Arrange
        var yaml = """
            pragent:
              enabled: true
              system_prompt: "Test prompt"
              review:
                enabled: true
                auto_post: false
              summary:
                enabled: true
                post_as_comment: true
              approve:
                enabled: true
                auto_approve_threshold: "minor"
                require_review_first: true
              ignore_paths:
                - "*.min.js"
                - "dist/**"
            """;

        // Act
        var result = YamlConfigurationProvider.Deserialize<PRAgent.Models.PRAgentYmlConfig>(yaml);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.PRAgent);
        Assert.True(result.PRAgent.Enabled);
        Assert.Equal("Test prompt", result.PRAgent.SystemPrompt);
        Assert.True(result.PRAgent.Review?.Enabled);
        Assert.False(result.PRAgent.Review?.AutoPost);
        Assert.True(result.PRAgent.Summary?.Enabled);
        Assert.True(result.PRAgent.Summary?.PostAsComment);
        Assert.True(result.PRAgent.Approve?.Enabled);
        Assert.Equal("minor", result.PRAgent.Approve?.AutoApproveThreshold);
        Assert.True(result.PRAgent.Approve?.RequireReviewFirst);
        Assert.NotNull(result.PRAgent.IgnorePaths);
        Assert.Equal(2, result.PRAgent.IgnorePaths?.Count);
    }

    [Fact]
    public void Deserialize_EmptyYaml_ReturnsNull()
    {
        // Arrange
        var yaml = "";

        // Act
        var result = YamlConfigurationProvider.Deserialize<PRAgent.Models.PRAgentYmlConfig>(yaml);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Deserialize_NullYaml_ReturnsNull()
    {
        // Act
        var result = YamlConfigurationProvider.Deserialize<PRAgent.Models.PRAgentYmlConfig>(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void IsValidYaml_ValidYaml_ReturnsTrue()
    {
        // Arrange
        var yaml = """
            pragent:
              enabled: true
            """;

        // Act
        var result = YamlConfigurationProvider.IsValidYaml(yaml);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidYaml_InvalidYaml_ReturnsFalse()
    {
        // Arrange
        var yaml = """
            pragent:
              enabled: [unclosed bracket
            """;

        // Act
        var result = YamlConfigurationProvider.IsValidYaml(yaml);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidYaml_EmptyString_ReturnsFalse()
    {
        // Act
        var result = YamlConfigurationProvider.IsValidYaml("");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Deserialize_MinimalYaml_ReturnsConfigWithDefaults()
    {
        // Arrange
        var yaml = """
            pragent:
              enabled: true
            """;

        // Act
        var result = YamlConfigurationProvider.Deserialize<PRAgent.Models.PRAgentYmlConfig>(yaml);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.PRAgent);
        Assert.True(result.PRAgent.Enabled);
        Assert.Null(result.PRAgent.SystemPrompt);
        Assert.Null(result.PRAgent.Review);
        Assert.Null(result.PRAgent.Summary);
        Assert.Null(result.PRAgent.Approve);
        Assert.Null(result.PRAgent.IgnorePaths);
    }
}

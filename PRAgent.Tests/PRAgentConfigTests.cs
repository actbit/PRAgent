using PRAgent.Models;

namespace PRAgent.Tests;

public class PRAgentConfigTests
{
    [Fact]
    public void PRAgentConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new PRAgentConfig();

        // Assert
        Assert.True(config.Enabled);
        Assert.Null(config.SystemPrompt);
        Assert.Null(config.Review);
        Assert.Null(config.Summary);
        Assert.Null(config.Approve);
        Assert.Null(config.IgnorePaths);
    }

    [Fact]
    public void ReviewConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new ReviewConfig();

        // Assert
        Assert.True(config.Enabled);
        Assert.False(config.AutoPost);
        Assert.Null(config.CustomPrompt);
    }

    [Fact]
    public void SummaryConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new SummaryConfig();

        // Assert
        Assert.True(config.Enabled);
        Assert.True(config.PostAsComment);
        Assert.Null(config.CustomPrompt);
    }

    [Fact]
    public void ApproveConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new ApproveConfig();

        // Assert
        Assert.True(config.Enabled);
        Assert.Equal("minor", config.AutoApproveThreshold);
        Assert.True(config.RequireReviewFirst);
    }

    [Theory]
    [InlineData("critical", ApprovalThreshold.Critical)]
    [InlineData("major", ApprovalThreshold.Major)]
    [InlineData("minor", ApprovalThreshold.Minor)]
    [InlineData("none", ApprovalThreshold.None)]
    public void ApprovalThreshold_HasCorrectValues(string value, ApprovalThreshold expected)
    {
        // Act
        var result = Enum.Parse<ApprovalThreshold>(value, true);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ApprovalThreshold_Values_AreSequential()
    {
        // Arrange
        var values = Enum.GetValues<ApprovalThreshold>().Cast<ApprovalThreshold>().ToList();

        // Assert
        Assert.Equal(4, values.Count);
        Assert.Contains(ApprovalThreshold.Critical, values);
        Assert.Contains(ApprovalThreshold.Major, values);
        Assert.Contains(ApprovalThreshold.Minor, values);
        Assert.Contains(ApprovalThreshold.None, values);
    }
}

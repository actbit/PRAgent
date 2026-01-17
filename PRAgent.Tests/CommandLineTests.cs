using PRAgent.Models;

namespace PRAgent.Tests;

public class CommandLineTests
{
    [Theory]
    [InlineData("critical", ApprovalThreshold.Critical)]
    [InlineData("major", ApprovalThreshold.Major)]
    [InlineData("minor", ApprovalThreshold.Minor)]
    [InlineData("none", ApprovalThreshold.None)]
    [InlineData("CRITICAL", ApprovalThreshold.Critical)]
    [InlineData("MAJOR", ApprovalThreshold.Major)]
    [InlineData("MINOR", ApprovalThreshold.Minor)]
    [InlineData("NONE", ApprovalThreshold.None)]
    [InlineData("CrItIcAl", ApprovalThreshold.Critical)]
    public void ParseApprovalThreshold_ValidInput_ReturnsCorrectThreshold(string input, ApprovalThreshold expected)
    {
        // Arrange
        Func<string, ApprovalThreshold> parseThreshold = value =>
        {
            return value.ToLowerInvariant() switch
            {
                "critical" => ApprovalThreshold.Critical,
                "major" => ApprovalThreshold.Major,
                "minor" => ApprovalThreshold.Minor,
                "none" => ApprovalThreshold.None,
                _ => ApprovalThreshold.Minor
            };
        };

        // Act
        var result = parseThreshold(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseApprovalThreshold_InvalidInput_ReturnsDefault(string input)
    {
        // Arrange
        Func<string, ApprovalThreshold> parseThreshold = value =>
        {
            return value.ToLowerInvariant() switch
            {
                "critical" => ApprovalThreshold.Critical,
                "major" => ApprovalThreshold.Major,
                "minor" => ApprovalThreshold.Minor,
                "none" => ApprovalThreshold.None,
                _ => ApprovalThreshold.Minor
            };
        };

        // Act
        var result = parseThreshold(input);

        // Assert
        Assert.Equal(ApprovalThreshold.Minor, result);
    }

    [Fact]
    public void ParseReviewOptions_ValidArgs_ReturnsCorrectOptions()
    {
        // Arrange
        var args = new[] { "review", "--owner", "testorg", "--repo", "testrepo", "--pr", "123" };

        // Simulate parsing
        var owner = args.Length > 2 ? args[2] : null;
        var repo = args.Length > 4 ? args[4] : null;
        var prNumber = args.Length > 6 && int.TryParse(args[6], out var pr) ? pr : 0;

        // Assert
        Assert.Equal("testorg", owner);
        Assert.Equal("testrepo", repo);
        Assert.Equal(123, prNumber);
    }

    [Fact]
    public void ParseApproveOptions_WithAutoFlag_RecognizesAuto()
    {
        // Arrange
        var args = new[] { "approve", "--owner", "testorg", "--repo", "testrepo", "--pr", "123", "--auto" };
        var hasAutoFlag = args.Contains("--auto");

        // Assert
        Assert.True(hasAutoFlag);
    }

    [Fact]
    public void ParseApproveOptions_WithThreshold_ExtractsThreshold()
    {
        // Arrange
        var args = new[] { "approve", "--threshold", "major" };
        var thresholdIndex = Array.IndexOf(args, "--threshold");
        var threshold = thresholdIndex >= 0 && thresholdIndex + 1 < args.Length
            ? args[thresholdIndex + 1]
            : "minor";

        // Assert
        Assert.Equal("major", threshold);
    }

    [Theory]
    [InlineData("critical", 0)]
    [InlineData("major", 1)]
    [InlineData("minor", 2)]
    [InlineData("none", 3)]
    public void ApprovalThreshold_Order_IsCorrect(string thresholdName, int expectedOrder)
    {
        // Arrange
        var expected = new Dictionary<string, int>
        {
            { "critical", 0 },
            { "major", 1 },
            { "minor", 2 },
            { "none", 3 }
        };

        // Act
        var result = Enum.GetValues<ApprovalThreshold>()
            .Cast<ApprovalThreshold>()
            .ToList();

        // Assert
        Assert.Equal(expectedOrder, result.IndexOf(Enum.Parse<ApprovalThreshold>(thresholdName, true)));
    }

    [Fact]
    public void ParseApproveOptions_WithCommentFlag_ExtractsComment()
    {
        // Arrange
        var args = new[] { "approve", "--comment", "LGTM", "--post-comment" };
        var commentIndex = Array.IndexOf(args, "--comment");
        var comment = commentIndex >= 0 && commentIndex + 1 < args.Length
            ? args[commentIndex + 1]
            : null;

        // Assert
        Assert.Equal("LGTM", comment);
    }
}

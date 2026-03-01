using PRAgent.Models;
using Xunit;

namespace PRAgent.Tests;

public class CommentCommandTests
{
    [Fact]
    public void Parse_CommentCommandWithSingleComment_ReturnsCorrectOptions()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--repo", "testrepo", "--pr", "123", "@150", "Test comment" };

        // Act
        var options = CommentCommandOptions.Parse(args);

        // Assert
        Assert.Equal("testowner", options.Owner);
        Assert.Equal("testrepo", options.Repo);
        Assert.Equal(123, options.PrNumber);
        Assert.Single(options.Comments);
        Assert.Equal(150, options.Comments[0].LineNumber);
        Assert.Equal("src/index.cs", options.Comments[0].FilePath);
        Assert.Equal("Test comment", options.Comments[0].CommentText);
        Assert.Null(options.Comments[0].SuggestionText);
    }

    [Fact]
    public void Parse_CommentCommandWithMultipleComments_ReturnsCorrectOptions()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--repo", "testrepo", "--pr", "123", "@100", "Comment1", "@200", "Comment2" };

        // Act
        var options = CommentCommandOptions.Parse(args);

        // Assert
        Assert.Equal("testowner", options.Owner);
        Assert.Equal("testrepo", options.Repo);
        Assert.Equal(123, options.PrNumber);
        Assert.Equal(2, options.Comments.Count);
        Assert.Equal(100, options.Comments[0].LineNumber);
        Assert.Equal("Comment1", options.Comments[0].CommentText);
        Assert.Equal(200, options.Comments[1].LineNumber);
        Assert.Equal("Comment2", options.Comments[1].CommentText);
    }

    [Fact]
    public void Parse_CommentCommandWithSuggestion_ReturnsCorrectOptions()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--repo", "testrepo", "--pr", "123", "@200", "Test comment", "--suggestion", "Suggested code" };

        // Act
        var options = CommentCommandOptions.Parse(args);

        // Assert
        Assert.Equal("testowner", options.Owner);
        Assert.Equal("testrepo", options.Repo);
        Assert.Equal(123, options.PrNumber);
        Assert.Single(options.Comments);
        Assert.Equal(200, options.Comments[0].LineNumber);
        Assert.Equal("src/index.cs", options.Comments[0].FilePath);
        Assert.Equal("Test comment", options.Comments[0].CommentText);
        Assert.Equal("Suggested code", options.Comments[0].SuggestionText);
    }

    [Fact]
    public void Parse_CommentCommandWithShortOptions_ReturnsCorrectOptions()
    {
        // Arrange
        string[] args = { "comment", "-o", "testowner", "-r", "testrepo", "-p", "123", "@150", "Test comment" };

        // Act
        var options = CommentCommandOptions.Parse(args);

        // Assert
        Assert.Equal("testowner", options.Owner);
        Assert.Equal("testrepo", options.Repo);
        Assert.Equal(123, options.PrNumber);
        Assert.Single(options.Comments);
        Assert.Equal(150, options.Comments[0].LineNumber);
        Assert.Equal("Test comment", options.Comments[0].CommentText);
    }

    [Fact]
    public void Parse_CommentCommandWithoutOwner_ReturnsError()
    {
        // Arrange
        string[] args = { "comment", "--repo", "testrepo", "--pr", "123", "@150", "Test comment" };

        // Act
        var options = CommentCommandOptions.Parse(args);
        var isValid = options.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains("--owner is required", errors);
    }

    [Fact]
    public void Parse_CommentCommandWithoutRepo_ReturnsError()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--pr", "123", "@150", "Test comment" };

        // Act
        var options = CommentCommandOptions.Parse(args);
        var isValid = options.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains("--repo is required", errors);
    }

    [Fact]
    public void Parse_CommentCommandWithoutPrNumber_ReturnsError()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--repo", "testrepo", "@150", "Test comment" };

        // Act
        var options = CommentCommandOptions.Parse(args);
        var isValid = options.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains("--pr is required and must be a positive number", errors);
    }

    [Fact]
    public void Parse_CommentCommandWithoutComments_ReturnsError()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--repo", "testrepo", "--pr", "123" };

        // Act
        var options = CommentCommandOptions.Parse(args);
        var isValid = options.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        // コメントが指定されていない場合はエラー
        Assert.Contains("No valid comments specified", errors);
    }

    [Fact]
    public void Parse_CommentCommandWithInvalidLineNumber_IsIgnored()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--repo", "testrepo", "--pr", "123", "@-1", "Test comment" };

        // Act
        var options = CommentCommandOptions.Parse(args);

        // Assert
        // 無効な行番号のコメントはパース段階で除外される
        Assert.Empty(options.Comments);
    }

    [Fact]
    public void Parse_CommentCommandWithEmptyCommentText_IsIgnored()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--repo", "testrepo", "--pr", "123", "@150", "" };

        // Act
        var options = CommentCommandOptions.Parse(args);

        // Assert
        // 空のコメントはパース段階で除外される
        Assert.Empty(options.Comments);
    }

    [Fact]
    public void Parse_CommentCommandWithWhitespaceOnlyCommentText_IsIgnored()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--repo", "testrepo", "--pr", "123", "@150", "   \t\n  " };

        // Act
        var options = CommentCommandOptions.Parse(args);

        // Assert
        // 空白のみのコメントはパース段階で除外される
        Assert.Empty(options.Comments);
    }

    [Fact]
    public void Parse_CommentCommandWithEmptySuggestion_IgnoresSuggestion()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--repo", "testrepo", "--pr", "123", "@200", "Test comment", "--suggestion", "" };

        // Act
        var options = CommentCommandOptions.Parse(args);

        // Assert
        Assert.Single(options.Comments);
        Assert.Equal(200, options.Comments[0].LineNumber);
        Assert.Equal("Test comment", options.Comments[0].CommentText);
        Assert.Null(options.Comments[0].SuggestionText);
    }

    [Fact]
    public void Parse_CommentCommandWithWhitespaceOnlySuggestion_IgnoresSuggestion()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--repo", "testrepo", "--pr", "123", "@200", "Test comment", "--suggestion", "   \t\n  " };

        // Act
        var options = CommentCommandOptions.Parse(args);

        // Assert
        Assert.Single(options.Comments);
        Assert.Equal(200, options.Comments[0].LineNumber);
        Assert.Equal("Test comment", options.Comments[0].CommentText);
        Assert.Null(options.Comments[0].SuggestionText);
    }

    [Fact]
    public void Parse_CommentCommandWithFilePath_ReturnsCorrectOptions()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--repo", "testrepo", "--pr", "123", "src/file.cs@123", "Test comment" };

        // Act
        var options = CommentCommandOptions.Parse(args);

        // Assert
        Assert.Equal("testowner", options.Owner);
        Assert.Equal("testrepo", options.Repo);
        Assert.Equal(123, options.PrNumber);
        Assert.Single(options.Comments);
        Assert.Equal(123, options.Comments[0].LineNumber);
        Assert.Equal("src/file.cs", options.Comments[0].FilePath);
        Assert.Equal("Test comment", options.Comments[0].CommentText);
        Assert.Null(options.Comments[0].SuggestionText);
    }

    [Fact]
    public void Parse_CommentCommandWithApprove_ReturnsCorrectOptions()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--repo", "testrepo", "--pr", "123", "@150", "Test comment", "--approve" };

        // Act
        var options = CommentCommandOptions.Parse(args);

        // Assert
        Assert.Equal("testowner", options.Owner);
        Assert.Equal("testrepo", options.Repo);
        Assert.Equal(123, options.PrNumber);
        Assert.Single(options.Comments);
        Assert.Equal(150, options.Comments[0].LineNumber);
        Assert.Equal("Test comment", options.Comments[0].CommentText);
        Assert.True(options.Approve);
        Assert.Null(options.Comments[0].SuggestionText);
    }

    [Fact]
    public void Parse_CommentCommandWithApproveAndSuggestion_ReturnsCorrectOptions()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--repo", "testrepo", "--pr", "123", "@200", "Test comment", "--suggestion", "Fixed code", "--approve" };

        // Act
        var options = CommentCommandOptions.Parse(args);

        // Assert
        Assert.Equal("testowner", options.Owner);
        Assert.Equal("testrepo", options.Repo);
        Assert.Equal(123, options.PrNumber);
        Assert.Single(options.Comments);
        Assert.Equal(200, options.Comments[0].LineNumber);
        Assert.Equal("Test comment", options.Comments[0].CommentText);
        Assert.Equal("Fixed code", options.Comments[0].SuggestionText);
        Assert.True(options.Approve);
    }

    [Fact]
    public void Parse_CommentCommandWithMultipleCommentsAndApprove_ReturnsCorrectOptions()
    {
        // Arrange
        string[] args = { "comment", "--owner", "testowner", "--repo", "testrepo", "--pr", "123", "@100", "Comment1", "@200", "Comment2", "--approve" };

        // Act
        var options = CommentCommandOptions.Parse(args);

        // Assert
        Assert.Equal("testowner", options.Owner);
        Assert.Equal("testrepo", options.Repo);
        Assert.Equal(123, options.PrNumber);
        Assert.Equal(2, options.Comments.Count);
        Assert.Equal(100, options.Comments[0].LineNumber);
        Assert.Equal("Comment1", options.Comments[0].CommentText);
        Assert.Equal(200, options.Comments[1].LineNumber);
        Assert.Equal("Comment2", options.Comments[1].CommentText);
        Assert.True(options.Approve);
    }

    [Fact]
    public void Parse_CommentCommandWithShortApprove_ReturnsCorrectOptions()
    {
        // Arrange
        string[] args = { "comment", "-o", "testowner", "-r", "testrepo", "-p", "123", "@150", "Test comment", "--approve" };

        // Act
        var options = CommentCommandOptions.Parse(args);

        // Assert
        Assert.Equal("testowner", options.Owner);
        Assert.Equal("testrepo", options.Repo);
        Assert.Equal(123, options.PrNumber);
        Assert.Single(options.Comments);
        Assert.Equal(150, options.Comments[0].LineNumber);
        Assert.Equal("Test comment", options.Comments[0].CommentText);
        Assert.True(options.Approve);
    }
}
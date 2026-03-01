using Octokit;
using Xunit;

namespace PRAgent.Tests;

/// <summary>
/// DraftPullRequestReviewCommentの引数順序が正しいことを確認するテスト
/// </summary>
public class DraftPullRequestReviewCommentTests
{
    [Fact]
    public void Constructor_ParameterOrder_ShouldBeBodyPathPosition()
    {
        // Arrange
        var body = "This is a test comment";
        var path = "src/test/File.cs";
        var position = 42;

        // Act
        var comment = new DraftPullRequestReviewComment(body, path, position);

        // Assert - Octokitの仕様通り (body, path, position) の順序であることを確認
        Assert.Equal(body, comment.Body);
        Assert.Equal(path, comment.Path);
        Assert.Equal(position, comment.Position);
    }

    [Fact]
    public void Constructor_WithJapaneseBody_ShouldWork()
    {
        // Arrange
        var body = "これはテストコメントです。修正が必要です。";
        var path = "src/test/File.cs";
        var position = 10;

        // Act
        var comment = new DraftPullRequestReviewComment(body, path, position);

        // Assert
        Assert.Equal(body, comment.Body);
        Assert.Equal(path, comment.Path);
        Assert.Equal(position, comment.Position);
    }

    [Fact]
    public void Constructor_WithSuggestionInBody_ShouldWork()
    {
        // Arrange
        var body = "Please fix this\n```suggestion\nvar fixed = true;\n```";
        var path = "src/test/File.cs";
        var position = 15;

        // Act
        var comment = new DraftPullRequestReviewComment(body, path, position);

        // Assert
        Assert.Equal(body, comment.Body);
        Assert.Contains("```suggestion", comment.Body);
    }

    [Fact]
    public void Constructor_WithNullBody_ShouldThrow()
    {
        // Arrange
        string? body = null;
        var path = "src/test/File.cs";
        var position = 1;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DraftPullRequestReviewComment(body!, path, position));
    }

    [Fact]
    public void Constructor_WithNullPath_ShouldThrow()
    {
        // Arrange
        var body = "Test comment";
        string? path = null;
        var position = 1;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DraftPullRequestReviewComment(body, path!, position));
    }

    [Fact]
    public void Constructor_WithEmptyBody_ShouldThrow()
    {
        // Arrange
        var body = "";
        var path = "src/test/File.cs";
        var position = 1;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new DraftPullRequestReviewComment(body, path, position));
    }

    [Fact]
    public void Constructor_WithEmptyPath_ShouldThrow()
    {
        // Arrange
        var body = "Test comment";
        var path = "";
        var position = 1;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new DraftPullRequestReviewComment(body, path, position));
    }
}

using Moq;
using Octokit;
using PRAgent.Services;
using Xunit;

namespace PRAgent.Tests;

/// <summary>
/// GitHubServiceの行コメント機能のテスト
/// </summary>
public class GitHubServiceLineCommentTests
{
    /// <summary>
    /// DraftPullRequestReviewCommentのリストが正しく作成されることを確認するテスト
    /// </summary>
    [Fact]
    public void DraftPullRequestReviewComment_CreatesCorrectComment_ForSingleLine()
    {
        // Arrange
        var body = "This needs to be fixed";
        var path = "src/Services/GitHubService.cs";
        var position = 42;

        // Act
        var comment = new DraftPullRequestReviewComment(body, path, position);

        // Assert
        Assert.Equal(body, comment.Body);
        Assert.Equal(path, comment.Path);
        Assert.Equal(position, comment.Position);
    }

    /// <summary>
    /// 複数行コメントのデータ構造が正しく作成されることを確認するテスト
    /// </summary>
    [Fact]
    public void MultipleLineComments_CreatesCorrectDataStructure()
    {
        // Arrange & Act
        var comments = new List<(string FilePath, int? LineNumber, int? StartLine, int? EndLine, string Comment, string? Suggestion)>
        {
            ("src/File1.cs", 10, null, null, "Fix this issue", null),
            ("src/File2.cs", 20, null, null, "Another issue", "var x = 1;"),
            ("src/File3.cs", null, 30, 35, "Multi-line issue", null)
        };

        // Assert
        Assert.Equal(3, comments.Count);
        Assert.Equal("src/File1.cs", comments[0].FilePath);
        Assert.Equal(10, comments[0].LineNumber);
        Assert.Equal("Fix this issue", comments[0].Comment);

        Assert.Equal("src/File2.cs", comments[1].FilePath);
        Assert.Equal(20, comments[1].LineNumber);
        Assert.Equal("var x = 1;", comments[1].Suggestion);

        Assert.Equal("src/File3.cs", comments[2].FilePath);
        Assert.Null(comments[2].LineNumber);
        Assert.Equal(30, comments[2].StartLine);
        Assert.Equal(35, comments[2].EndLine);
    }

    /// <summary>
    /// DraftPullRequestReviewCommentのリストが正しく作成されることを確認するテスト
    /// </summary>
    [Fact]
    public void DraftPullRequestReviewComment_CreatesCorrectList()
    {
        // Arrange
        var commentsData = new List<(string FilePath, int? LineNumber, int? StartLine, int? EndLine, string Comment, string? Suggestion)>
        {
            ("src/File1.cs", 10, null, null, "Issue 1", null),
            ("src/File2.cs", 20, null, null, "Issue 2", "var x = 1;")
        };

        // Act
        var draftComments = commentsData.Select(c =>
        {
            var commentBody = c.Suggestion != null ? $"{c.Comment}\n```suggestion\n{c.Suggestion}\n```" : c.Comment;

            if (c.LineNumber.HasValue)
            {
                return new DraftPullRequestReviewComment(commentBody, c.FilePath, c.LineNumber.Value);
            }
            else if (c.StartLine.HasValue)
            {
                return new DraftPullRequestReviewComment(commentBody, c.FilePath, c.StartLine.Value);
            }
            else
            {
                throw new ArgumentException($"Comment must have either LineNumber or StartLine: {c.FilePath}");
            }
        }).ToList();

        // Assert
        Assert.Equal(2, draftComments.Count);

        Assert.Equal("Issue 1", draftComments[0].Body);
        Assert.Equal("src/File1.cs", draftComments[0].Path);
        Assert.Equal(10, draftComments[0].Position);

        Assert.Contains("Issue 2", draftComments[1].Body);
        Assert.Contains("```suggestion", draftComments[1].Body);
        Assert.Contains("var x = 1;", draftComments[1].Body);
        Assert.Equal("src/File2.cs", draftComments[1].Path);
        Assert.Equal(20, draftComments[1].Position);
    }

    /// <summary>
    /// サジェスチョン付きコメントのフォーマットが正しいことを確認するテスト
    /// </summary>
    [Fact]
    public void CommentWithSuggestion_FormatsCorrectly()
    {
        // Arrange
        var comment = "Please use var instead";
        var suggestion = "var number = 42;";

        // Act
        var commentBody = $"{comment}\n```suggestion\n{suggestion}\n```";

        // Assert
        Assert.Equal("Please use var instead\n```suggestion\nvar number = 42;\n```", commentBody);
    }

    /// <summary>
    /// 日本語コメントでも正しく動作することを確認するテスト
    /// </summary>
    [Fact]
    public void CommentWithJapanese_WorksCorrectly()
    {
        // Arrange
        var body = "この変数名は分かりにくいです。より説明的な名前に変更することを検討してください。";
        var path = "src/サービス/メイン.cs";
        var position = 100;

        // Act
        var comment = new DraftPullRequestReviewComment(body, path, position);

        // Assert
        Assert.Equal(body, comment.Body);
        Assert.Equal(path, comment.Path);
        Assert.Equal(position, comment.Position);
    }
}

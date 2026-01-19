using Microsoft.SemanticKernel;
using Octokit;
using PRAgent.Services;

namespace PRAgent.Plugins.GitHub;

/// <summary>
/// Semantic Kernel用のプルリクエストコメント投稿機能プラグイン
/// </summary>
public class PostCommentFunction
{
    private readonly IGitHubService _gitHubService;
    private readonly string _owner;
    private readonly string _repo;
    private readonly int _prNumber;

    public PostCommentFunction(
        IGitHubService gitHubService,
        string owner,
        string repo,
        int prNumber)
    {
        _gitHubService = gitHubService;
        _owner = owner;
        _repo = repo;
        _prNumber = prNumber;
    }

    /// <summary>
    /// プルリクエストに全体コメントを投稿します
    /// </summary>
    /// <param name="comment">コメント内容</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>投稿結果のメッセージ</returns>
    [KernelFunction("post_pr_comment")]
    public async Task<string> PostCommentAsync(
        string comment,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return "Error: Comment cannot be empty";
        }

        try
        {
            var result = await _gitHubService.CreateIssueCommentAsync(_owner, _repo, _prNumber, comment);
            return $"Comment posted successfully to PR #{_prNumber}. Comment ID: {result.Id}";
        }
        catch (Exception ex)
        {
            return $"Failed to post comment to PR #{_prNumber}: {ex.Message}";
        }
    }

    /// <summary>
    /// プルリクエストの特定の行にコメントを投稿します
    /// </summary>
    /// <param name="filePath">ファイルパス</param>
    /// <param name="lineNumber">行番号</param>
    /// <param name="comment">コメント内容</param>
    /// <param name="suggestion">提案される変更内容（オプション）</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>投稿結果のメッセージ</returns>
    [KernelFunction("post_line_comment")]
    public async Task<string> PostLineCommentAsync(
        string filePath,
        int lineNumber,
        string comment,
        string? suggestion = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "Error: File path cannot be empty";
        }

        if (string.IsNullOrWhiteSpace(comment))
        {
            return "Error: Comment cannot be empty";
        }

        try
        {
            var result = await _gitHubService.CreateLineCommentAsync(
                _owner, _repo, _prNumber, filePath, lineNumber, comment, suggestion);
            return $"Line comment posted successfully to {filePath}:{lineNumber} in PR #{_prNumber}";
        }
        catch (Exception ex)
        {
            return $"Failed to post line comment to PR #{_prNumber}: {ex.Message}";
        }
    }

    /// <summary>
    /// 複数の行コメントを一度に投稿します
    /// </summary>
    /// <param name="comments">コメントリスト（ファイルパス、行番号、コメント、提案）</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>投稿結果のメッセージ</returns>
    [KernelFunction("post_multiple_line_comments")]
    public async Task<string> PostMultipleLineCommentsAsync(
        string commentsJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commentsJson))
        {
            return "Error: Comments data cannot be empty";
        }

        try
        {
            // JSON形式のコメントデータをパース
            var comments = System.Text.Json.JsonSerializer.Deserialize<List<(string FilePath, int LineNumber, string Comment, string? Suggestion)>>(
                commentsJson,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (comments == null || comments.Count == 0)
            {
                return "Error: No valid comments found in the provided data";
            }

            var result = await _gitHubService.CreateMultipleLineCommentsAsync(_owner, _repo, _prNumber, comments);
            return $"Successfully posted {comments.Count} line comments to PR #{_prNumber}";
        }
        catch (Exception ex)
        {
            return $"Failed to post multiple line comments to PR #{_prNumber}: {ex.Message}";
        }
    }

    /// <summary>
    /// レビューコメントとして投稿します
    /// </summary>
    /// <param name="reviewBody">レビュー本文</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>投稿結果のメッセージ</returns>
    [KernelFunction("post_review_comment")]
    public async Task<string> PostReviewCommentAsync(
        string reviewBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reviewBody))
        {
            return "Error: Review body cannot be empty";
        }

        try
        {
            var result = await _gitHubService.CreateReviewCommentAsync(_owner, _repo, _prNumber, reviewBody);
            return $"Review comment posted successfully to PR #{_prNumber}. Review ID: {result.Id}";
        }
        catch (Exception ex)
        {
            return $"Failed to post review comment to PR #{_prNumber}: {ex.Message}";
        }
    }

    /// <summary>
    /// KernelFunctionとして使用するためのファクトリメソッド（PRコメント）
    /// </summary>
    public static KernelFunction PostCommentAsyncFunction(
        IGitHubService gitHubService,
        string owner,
        string repo,
        int prNumber)
    {
        var functionPlugin = new PostCommentFunction(gitHubService, owner, repo, prNumber);
        return KernelFunctionFactory.CreateFromMethod(
            (string comment, CancellationToken ct) => functionPlugin.PostCommentAsync(comment, ct),
            functionName: "post_pr_comment",
            description: "Posts a general comment to a pull request",
            parameters: new[]
            {
                new KernelParameterMetadata("comment")
                {
                    Description = "The comment content to post",
                    IsRequired = true
                }
            });
    }

    /// <summary>
    /// KernelFunctionとして使用するためのファクトリメソッド（行コメント）
    /// </summary>
    public static KernelFunction PostLineCommentAsyncFunction(
        IGitHubService gitHubService,
        string owner,
        string repo,
        int prNumber)
    {
        var functionPlugin = new PostCommentFunction(gitHubService, owner, repo, prNumber);
        return KernelFunctionFactory.CreateFromMethod(
            (string filePath, int lineNumber, string comment, string? suggestion, CancellationToken ct) =>
                functionPlugin.PostLineCommentAsync(filePath, lineNumber, comment, suggestion, ct),
            functionName: "post_line_comment",
            description: "Posts a comment on a specific line in a pull request file",
            parameters: new[]
            {
                new KernelParameterMetadata("filePath")
                {
                    Description = "The path to the file in the repository",
                    IsRequired = true
                },
                new KernelParameterMetadata("lineNumber")
                {
                    Description = "The line number to comment on",
                    IsRequired = true
                },
                new KernelParameterMetadata("comment")
                {
                    Description = "The comment content",
                    IsRequired = true
                },
                new KernelParameterMetadata("suggestion")
                {
                    Description = "Optional suggestion for the change",
                    IsRequired = false,
                    DefaultValue = null
                }
            });
    }

    /// <summary>
    /// KernelFunctionとして使用するためのファクトリメソッド（レビューコメント）
    /// </summary>
    public static KernelFunction PostReviewCommentAsyncFunction(
        IGitHubService gitHubService,
        string owner,
        string repo,
        int prNumber)
    {
        var functionPlugin = new PostCommentFunction(gitHubService, owner, repo, prNumber);
        return KernelFunctionFactory.CreateFromMethod(
            (string reviewBody, CancellationToken ct) => functionPlugin.PostReviewCommentAsync(reviewBody, ct),
            functionName: "post_review_comment",
            description: "Posts a review comment to a pull request",
            parameters: new[]
            {
                new KernelParameterMetadata("reviewBody")
                {
                    Description = "The review content to post",
                    IsRequired = true
                }
            });
    }
}

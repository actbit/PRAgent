using Microsoft.SemanticKernel;
using PRAgent.Models;

namespace PRAgent.Plugins.GitHub;

/// <summary>
/// Semantic Kernel用のプルリクエストコメント投稿機能プラグイン（バッファリング版）
/// </summary>
public class PostCommentFunction
{
    private readonly PRActionBuffer _buffer;

    public PostCommentFunction(PRActionBuffer buffer)
    {
        _buffer = buffer;
    }

    /// <summary>
    /// プルリクエストに全体コメントを追加します（バッファに追加）
    /// </summary>
    /// <param name="comment">コメント内容</param>
    /// <returns>コメントがバッファに追加されたことを示すメッセージ</returns>
    [KernelFunction("post_pr_comment")]
    public string PostCommentAsync(string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return "Error: Comment cannot be empty";
        }

        _buffer.SetGeneralComment(comment);
        return $"General comment has been added to buffer. Length: {comment.Length} characters";
    }

    /// <summary>
    /// プルリクエストの特定の行にコメントを追加します（バッファに追加）
    /// </summary>
    /// <param name="filePath">ファイルパス</param>
    /// <param name="lineNumber">行番号</param>
    /// <param name="comment">コメント内容</param>
    /// <param name="suggestion">提案される変更内容（オプション）</param>
    /// <returns>行コメントがバッファに追加されたことを示すメッセージ</returns>
    [KernelFunction("post_line_comment")]
    public string PostLineCommentAsync(
        string filePath,
        int lineNumber,
        string comment,
        string? suggestion = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "Error: File path cannot be empty";
        }

        if (string.IsNullOrWhiteSpace(comment))
        {
            return "Error: Comment cannot be empty";
        }

        _buffer.AddLineComment(filePath, lineNumber, comment, suggestion);
        return $"Line comment has been added to buffer for {filePath}:{lineNumber}";
    }

    /// <summary>
    /// プルリクエストの特定の範囲にコメントを追加します（バッファに追加）
    /// </summary>
    /// <param name="filePath">ファイルパス</param>
    /// <param name="startLine">開始行番号</param>
    /// <param name="endLine">終了行番号</param>
    /// <param name="comment">コメント内容</param>
    /// <param name="suggestion">提案される変更内容（オプション）</param>
    /// <returns>範囲コメントがバッファに追加されたことを示すメッセージ</returns>
    [KernelFunction("post_range_comment")]
    public string PostRangeCommentAsync(
        string filePath,
        int startLine,
        int endLine,
        string comment,
        string? suggestion = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "Error: File path cannot be empty";
        }

        if (startLine <= 0 || endLine <= 0)
        {
            return "Error: Line numbers must be positive";
        }

        if (startLine > endLine)
        {
            return "Error: startLine must be less than or equal to endLine";
        }

        if (string.IsNullOrWhiteSpace(comment))
        {
            return "Error: Comment cannot be empty";
        }

        _buffer.AddRangeComment(filePath, startLine, endLine, comment, suggestion);
        return $"Range comment has been added to buffer for {filePath}:{startLine}-{endLine}";
    }

    /// <summary>
    /// レビューコメントを追加します（バッファに追加）
    /// </summary>
    /// <param name="reviewBody">レビュー本文</param>
    /// <returns>レビューコメントがバッファに追加されたことを示すメッセージ</returns>
    [KernelFunction("post_review_comment")]
    public string PostReviewCommentAsync(string reviewBody)
    {
        if (string.IsNullOrWhiteSpace(reviewBody))
        {
            return "Error: Review body cannot be empty";
        }

        _buffer.AddReviewComment(reviewBody);
        return $"Review comment has been added to buffer. Length: {reviewBody.Length} characters";
    }

    /// <summary>
    /// KernelFunctionとして使用するためのファクトリメソッド（PRコメント）
    /// </summary>
    public static KernelFunction PostCommentAsyncFunction(PRActionBuffer buffer)
    {
        var functionPlugin = new PostCommentFunction(buffer);
        return KernelFunctionFactory.CreateFromMethod(
            (string comment) => functionPlugin.PostCommentAsync(comment),
            functionName: "post_pr_comment",
            description: "Adds a general comment to the buffer for posting to a pull request",
            parameters: new[]
            {
                new KernelParameterMetadata("comment")
                {
                    Description = "The comment content to add to buffer",
                    IsRequired = true
                }
            });
    }

    /// <summary>
    /// KernelFunctionとして使用するためのファクトリメソッド（行コメント）
    /// </summary>
    public static KernelFunction PostLineCommentAsyncFunction(PRActionBuffer buffer)
    {
        var functionPlugin = new PostCommentFunction(buffer);
        return KernelFunctionFactory.CreateFromMethod(
            (string filePath, int lineNumber, string comment, string? suggestion) =>
                functionPlugin.PostLineCommentAsync(filePath, lineNumber, comment, suggestion),
            functionName: "post_line_comment",
            description: "Adds a line comment to the buffer for posting to a specific line in a pull request file",
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
    public static KernelFunction PostReviewCommentAsyncFunction(PRActionBuffer buffer)
    {
        var functionPlugin = new PostCommentFunction(buffer);
        return KernelFunctionFactory.CreateFromMethod(
            (string reviewBody) => functionPlugin.PostReviewCommentAsync(reviewBody),
            functionName: "post_review_comment",
            description: "Adds a review comment to the buffer for posting to a pull request",
            parameters: new[]
            {
                new KernelParameterMetadata("reviewBody")
                {
                    Description = "The review content to add to buffer",
                    IsRequired = true
                }
            });
    }
}

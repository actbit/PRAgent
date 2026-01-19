using Microsoft.SemanticKernel;
using PRAgent.Models;

namespace PRAgent.Plugins;

/// <summary>
/// PRアクションを蓄積してまとめて投稿するための関数プラグイン
/// エージェントはこれらの関数を呼び出してアクションを追加し、最後にcommit_actionsでまとめて投稿する
/// </summary>
public class PRActionFunctions
{
    private readonly PRActionBuffer _buffer;

    public PRActionFunctions(PRActionBuffer buffer)
    {
        _buffer = buffer;
    }

    /// <summary>
    /// 特定の行にコメントを追加します
    /// </summary>
    [KernelFunction("add_line_comment")]
    public string AddLineComment(
        string filePath,
        int lineNumber,
        string comment,
        string? suggestion = null)
    {
        _buffer.AddLineComment(filePath, lineNumber, comment, suggestion);
        var suggestionText = !string.IsNullOrEmpty(suggestion) ? $" (提案: {suggestion})" : "";
        return $"行コメントを追加しました: {filePath}:{lineNumber} - {comment}{suggestionText}";
    }

    /// <summary>
    /// レビューコメントを追加します
    /// </summary>
    [KernelFunction("add_review_comment")]
    public string AddReviewComment(string comment)
    {
        _buffer.AddReviewComment(comment);
        return $"レビューコメントを追加しました: {comment}";
    }

    /// <summary>
    /// サマリーを追加します
    /// </summary>
    [KernelFunction("add_summary")]
    public string AddSummary(string summary)
    {
        _buffer.AddSummary(summary);
        return $"サマリーを追加しました: {summary.Substring(0, Math.Min(50, summary.Length))}...";
    }

    /// <summary>
    /// 全体コメントを設定します
    /// </summary>
    [KernelFunction("set_general_comment")]
    public string SetGeneralComment(string comment)
    {
        _buffer.SetGeneralComment(comment);
        return $"全体コメントを設定しました: {comment.Substring(0, Math.Min(50, comment.Length))}...";
    }

    /// <summary>
    /// 承認をマークします
    /// </summary>
    [KernelFunction("mark_for_approval")]
    public string MarkForApproval(string? comment = null)
    {
        _buffer.MarkForApproval(comment);
        var commentText = !string.IsNullOrEmpty(comment) ? $" (コメント: {comment})" : "";
        return $"承認をマークしました{commentText}";
    }

    /// <summary>
    /// 現在のバッファ状態を取得します
    /// </summary>
    [KernelFunction("get_buffer_state")]
    public string GetBufferState()
    {
        var state = _buffer.GetState();
        return $"""
            現在のバッファ状態:
            - 行コメント: {state.LineCommentCount}件
            - レビューコメント: {state.ReviewCommentCount}件
            - サマリー: {state.SummaryCount}件
            - 全体コメント: {(state.HasGeneralComment ? "あり" : "なし")}
            - 承認フラグ: {(state.ShouldApprove ? "オン" : "オフ")}
            """;
    }

    /// <summary>
    /// バッファをクリアします
    /// </summary>
    [KernelFunction("clear_buffer")]
    public string ClearBuffer()
    {
        _buffer.Clear();
        return "バッファをクリアしました";
    }

    /// <summary>
    /// アクションのコミット準備が完了したことを示します
    /// 実際のコミットは呼び出し元で行います
    /// </summary>
    [KernelFunction("ready_to_commit")]
    public string ReadyToCommit()
    {
        var state = _buffer.GetState();
        var totalActions = state.LineCommentCount + state.ReviewCommentCount + state.SummaryCount +
                          (state.HasGeneralComment ? 1 : 0) +
                          (state.ShouldApprove ? 1 : 0);

        if (totalActions == 0)
        {
            return "コミットするアクションがありません。";
        }

        return $"""
            {totalActions}件のアクションをコミット準備完了:
            - 行コメント: {state.LineCommentCount}件
            - レビューコメント: {state.ReviewCommentCount}件
            - サマリー: {state.SummaryCount}件
            - 全体コメント: {(state.HasGeneralComment ? "あり" : "なし")}
            - 承認: {(state.ShouldApprove ? "あり" : "なし")}

            これらのアクションをGitHubに投稿します。
            """;
    }
}

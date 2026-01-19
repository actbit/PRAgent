using Microsoft.SemanticKernel;
using PRAgent.Models;

namespace PRAgent.Plugins.GitHub;

/// <summary>
/// Semantic Kernel用のプルリクエスト承認機能プラグイン（バッファリング版）
/// Approve/Changes Request/Comment Onlyを区別
/// </summary>
public class ApprovePRFunction
{
    private readonly PRActionBuffer _buffer;

    public ApprovePRFunction(PRActionBuffer buffer)
    {
        _buffer = buffer;
    }

    /// <summary>
    /// プルリクエストを承認します（バッファに追加）
    /// </summary>
    /// <param name="comment">承認時に追加するコメント（オプション）</param>
    /// <returns>承認アクションが追加されたことを示すメッセージ</returns>
    [KernelFunction("approve_pull_request")]
    public string ApproveAsync(string? comment = null)
    {
        _buffer.MarkForApproval(comment);
        return $"Pull request approval action has been added to buffer.{(!string.IsNullOrEmpty(comment) ? $" Comment: {comment}" : "")}";
    }

    /// <summary>
    /// プルリクエストの変更を依頼します（バッファに追加）
    /// </summary>
    /// <param name="comment">変更依頼時のコメント（オプション）</param>
    /// <returns>変更依頼アクションが追加されたことを示すメッセージ</returns>
    [KernelFunction("request_changes")]
    public string RequestChangesAsync(string? comment = null)
    {
        _buffer.MarkForChangesRequested(comment);
        return $"Pull request changes requested action has been added to buffer.{(!string.IsNullOrEmpty(comment) ? $" Comment: {comment}" : "")}";
    }

    /// <summary>
    /// プルリクエストの承認ステータスを確認します（ダミー実装）
    /// </summary>
    /// <returns>現在のバッファ状態を含むステータス</returns>
    [KernelFunction("get_approval_status")]
    public string GetApprovalStatus()
    {
        var state = _buffer.GetState();
        var statusText = state.ApprovalState switch
        {
            PRApprovalState.None => "No approval action (comments only)",
            PRApprovalState.Approved => "Approved",
            PRApprovalState.ChangesRequested => "Changes Requested",
            _ => "Unknown"
        };
        return $"Current buffer state - ApprovalState: {statusText}, Comments: {state.ReviewCommentCount}";
    }

    /// <summary>
    /// KernelFunctionとして使用するためのファクトリメソッド
    /// </summary>
    public static KernelFunction ApproveAsyncFunction(PRActionBuffer buffer)
    {
        var functionPlugin = new ApprovePRFunction(buffer);
        return KernelFunctionFactory.CreateFromMethod(
            (string? comment) => functionPlugin.ApproveAsync(comment),
            functionName: "approve_pull_request",
            description: "Adds a pull request approval action to the buffer with an optional comment",
            parameters: new[]
            {
                new KernelParameterMetadata("comment")
                {
                    Description = "Optional comment to add when approving",
                    DefaultValue = null,
                    IsRequired = false
                }
            });
    }

    /// <summary>
    /// KernelFunctionとして使用するためのファクトリメソッド（変更依頼）
    /// </summary>
    public static KernelFunction RequestChangesFunction(PRActionBuffer buffer)
    {
        var functionPlugin = new ApprovePRFunction(buffer);
        return KernelFunctionFactory.CreateFromMethod(
            (string? comment) => functionPlugin.RequestChangesAsync(comment),
            functionName: "request_changes",
            description: "Adds a pull request changes requested action to the buffer with an optional comment",
            parameters: new[]
            {
                new KernelParameterMetadata("comment")
                {
                    Description = "Optional comment explaining the changes requested",
                    DefaultValue = null,
                    IsRequired = false
                }
            });
    }

    /// <summary>
    /// KernelFunctionとして使用するためのファクトリメソッド（ステータス取得）
    /// </summary>
    public static KernelFunction GetApprovalStatusFunction(PRActionBuffer buffer)
    {
        var functionPlugin = new ApprovePRFunction(buffer);
        return KernelFunctionFactory.CreateFromMethod(
            () => functionPlugin.GetApprovalStatus(),
            functionName: "get_approval_status",
            description: "Gets the current buffer state including pending approval status"
        );
    }
}

using Microsoft.SemanticKernel;
using PRAgent.Services;

namespace PRAgent.Plugins.GitHub;

/// <summary>
/// Semantic Kernel用のプルリクエスト承認機能プラグイン
/// </summary>
public class ApprovePRFunction
{
    private readonly IGitHubService _gitHubService;
    private readonly string _owner;
    private readonly string _repo;
    private readonly int _prNumber;

    public ApprovePRFunction(
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
    /// プルリクエストを承認します
    /// </summary>
    /// <param name="comment">承認時に追加するコメント（オプション）</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>承認結果のメッセージ</returns>
    [KernelFunction("approve_pull_request")]
    public async Task<string> ApproveAsync(
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _gitHubService.ApprovePullRequestAsync(_owner, _repo, _prNumber, comment);
            return $"Pull request #{_prNumber} has been approved successfully.{(!string.IsNullOrEmpty(comment) ? $" Comment: {comment}" : "")}";
        }
        catch (Exception ex)
        {
            return $"Failed to approve pull request #{_prNumber}: {ex.Message}";
        }
    }

    /// <summary>
    /// プルリクエストの承認ステータスを確認します
    /// </summary>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>現在の承認ステータス</returns>
    [KernelFunction("get_approval_status")]
    public async Task<string> GetApprovalStatusAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pr = await _gitHubService.GetPullRequestAsync(_owner, _repo, _prNumber);
            return $"Pull request #{_prNumber} status: {pr.State}, mergeable: {pr.Mergeable ?? true}";
        }
        catch (Exception ex)
        {
            return $"Failed to get approval status for PR #{_prNumber}: {ex.Message}";
        }
    }

    /// <summary>
    /// KernelFunctionとして使用するためのファクトリメソッド
    /// </summary>
    public static KernelFunction ApproveAsyncFunction(
        IGitHubService gitHubService,
        string owner,
        string repo,
        int prNumber)
    {
        var functionPlugin = new ApprovePRFunction(gitHubService, owner, repo, prNumber);
        return KernelFunctionFactory.CreateFromMethod(
            (string? comment, CancellationToken ct) => functionPlugin.ApproveAsync(comment, ct),
            functionName: "approve_pull_request",
            description: "Approves a pull request with an optional comment",
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
    /// KernelFunctionとして使用するためのファクトリメソッド（ステータス取得）
    /// </summary>
    public static KernelFunction GetApprovalStatusFunction(
        IGitHubService gitHubService,
        string owner,
        string repo,
        int prNumber)
    {
        var functionPlugin = new ApprovePRFunction(gitHubService, owner, repo, prNumber);
        return KernelFunctionFactory.CreateFromMethod(
            (CancellationToken ct) => functionPlugin.GetApprovalStatusAsync(ct),
            functionName: "get_approval_status",
            description: "Gets the current approval status of a pull request"
        );
    }
}

using PRAgent.Models;

namespace PRAgent.Services;

/// <summary>
/// エージェントオーケストレーションサービスのインターフェース
/// </summary>
public interface IAgentOrchestratorService
{
    /// <summary>
    /// プルリクエストのコードレビューを実行します
    /// </summary>
    Task<string> ReviewAsync(string owner, string repo, int prNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// プルリクエストの要約を作成します
    /// </summary>
    Task<string> SummarizeAsync(string owner, string repo, int prNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// レビューと承認を一連のワークフローとして実行します
    /// </summary>
    Task<ApprovalResult> ReviewAndApproveAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold = ApprovalThreshold.Minor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// プルリクエストのコードレビューを実行します（language指定）
    /// </summary>
    Task<string> ReviewAsync(string owner, string repo, int prNumber, string language, CancellationToken cancellationToken = default);

    /// <summary>
    /// プルリクエストの要約を作成します（language指定）
    /// </summary>
    Task<string> SummarizeAsync(string owner, string repo, int prNumber, string language, CancellationToken cancellationToken = default);

    /// <summary>
    /// レビューと承認を一連のワークフローとして実行します（language指定）
    /// </summary>
    Task<ApprovalResult> ReviewAndApproveAsync(
        string owner,
        string repo,
        int prNumber,
        string language,
        ApprovalThreshold threshold = ApprovalThreshold.Minor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// AgentGroupChatを使用したマルチエージェント協調によるレビューと承認
    /// </summary>
    Task<ApprovalResult> ReviewAndApproveWithAgentChatAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold = ApprovalThreshold.Minor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// カスタムワークフローを使用したレビューと承認
    /// </summary>
    Task<ApprovalResult> ReviewAndApproveWithCustomWorkflowAsync(
        string owner,
        string repo,
        int prNumber,
        string workflowType,
        ApprovalThreshold threshold = ApprovalThreshold.Minor,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 承認結果
/// </summary>
public class ApprovalResult
{
    public bool Approved { get; init; }
    public string Review { get; init; } = string.Empty;
    public string Reasoning { get; init; } = string.Empty;
    public string? Comment { get; init; }
    public string? ApprovalUrl { get; init; }
}

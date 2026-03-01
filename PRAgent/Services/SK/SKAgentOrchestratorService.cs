using Microsoft.Extensions.Logging;
using PRAgent.Agents.SK;
using PRAgent.Models;

namespace PRAgent.Services.SK;

/// <summary>
/// Semantic Kernelを使用したエージェントオーケストレーションサービス
/// ReviewAgentを中心に簡素化された構成
/// </summary>
public class SKAgentOrchestratorService : IAgentOrchestratorService
{
    private readonly SKReviewAgent _reviewAgent;
    private readonly IGitHubService _gitHubService;
    private readonly PullRequestDataService _prDataService;
    private readonly PRAgentConfig _config;
    private readonly ILogger<SKAgentOrchestratorService> _logger;

    public SKAgentOrchestratorService(
        SKReviewAgent reviewAgent,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        PRAgentConfig config,
        ILogger<SKAgentOrchestratorService> logger)
    {
        _reviewAgent = reviewAgent;
        _gitHubService = gitHubService;
        _prDataService = prDataService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// プルリクエストのコードレビューを実行します
    /// </summary>
    public async Task<string> ReviewAsync(string owner, string repo, int prNumber, CancellationToken cancellationToken = default)
    {
        // FunctionCalling設定に応じてメソッドを選択
        var useFunctionCalling = _config.AgentFramework?.EnableFunctionCalling ?? false;
        var useSubAgent = _config.AgentFramework?.UseSubAgent ?? true;

        if (useFunctionCalling)
        {
            if (useSubAgent)
            {
                // SubAgentあり: ReviewAgentで概要 → DetailedCommentAgentで詳細
                var (reviewText, actionResult) = await _reviewAgent.ReviewWithLineCommentsAsync(
                    owner, repo, prNumber, language: null, cancellationToken);

                if (actionResult != null)
                {
                    _logger.LogInformation(
                        "Review completed (with SubAgent). Line comments: {LineComments}, Review comments: {ReviewComments}, Approved: {Approved}",
                        actionResult.LineCommentsPosted,
                        actionResult.ReviewCommentsPosted,
                        actionResult.Approved);
                }

                return reviewText;
            }
            else
            {
                // SubAgentなし: ReviewAgentだけで完結
                var (reviewText, actionResult) = await _reviewAgent.ReviewDirectAsync(
                    owner, repo, prNumber, language: null, cancellationToken);

                if (actionResult != null)
                {
                    _logger.LogInformation(
                        "Review completed (direct mode). Line comments: {LineComments}, Review comments: {ReviewComments}, Approved: {Approved}",
                        actionResult.LineCommentsPosted,
                        actionResult.ReviewCommentsPosted,
                        actionResult.Approved);
                }

                return reviewText;
            }
        }

        return await _reviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// プルリクエストのコードレビューを実行します（language指定）
    /// </summary>
    public async Task<string> ReviewAsync(string owner, string repo, int prNumber, string language, CancellationToken cancellationToken = default)
    {
        var useFunctionCalling = _config.AgentFramework?.EnableFunctionCalling ?? false;
        var useSubAgent = _config.AgentFramework?.UseSubAgent ?? true;

        if (useFunctionCalling)
        {
            if (useSubAgent)
            {
                var (reviewText, actionResult) = await _reviewAgent.ReviewWithLineCommentsAsync(
                    owner, repo, prNumber, language, cancellationToken);

                if (actionResult != null)
                {
                    _logger.LogInformation(
                        "Review completed (with SubAgent). Line comments: {LineComments}, Review comments: {ReviewComments}, Approved: {Approved}",
                        actionResult.LineCommentsPosted,
                        actionResult.ReviewCommentsPosted,
                        actionResult.Approved);
                }

                return reviewText;
            }
            else
            {
                var (reviewText, actionResult) = await _reviewAgent.ReviewDirectAsync(
                    owner, repo, prNumber, language, cancellationToken);

                if (actionResult != null)
                {
                    _logger.LogInformation(
                        "Review completed (direct mode). Line comments: {LineComments}, Review comments: {ReviewComments}, Approved: {Approved}",
                        actionResult.LineCommentsPosted,
                        actionResult.ReviewCommentsPosted,
                        actionResult.Approved);
                }

                return reviewText;
            }
        }

        return await _reviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// レビューと承認を一連のワークフローとして実行します
    /// ReviewAgentに統合されたため、ReviewWithLineCommentsAsyncまたはReviewDirectAsyncを使用
    /// </summary>
    public async Task<ApprovalResult> ReviewAndApproveAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold = ApprovalThreshold.Minor,
        CancellationToken cancellationToken = default)
    {
        var useFunctionCalling = _config.AgentFramework?.EnableFunctionCalling ?? false;
        var useSubAgent = _config.AgentFramework?.UseSubAgent ?? true;

        if (useFunctionCalling)
        {
            var (reviewText, actionResult) = useSubAgent
                ? await _reviewAgent.ReviewWithLineCommentsAsync(owner, repo, prNumber, language: null, cancellationToken)
                : await _reviewAgent.ReviewDirectAsync(owner, repo, prNumber, language: null, cancellationToken);

            return new ApprovalResult
            {
                Approved = actionResult?.Approved ?? false,
                Review = reviewText,
                Reasoning = actionResult?.Message ?? string.Empty,
                ApprovalUrl = actionResult?.ApprovalUrl
            };
        }

        // FunctionCalling無効の場合は通常のレビューのみ
        var review = await _reviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken: cancellationToken);
        return new ApprovalResult
        {
            Approved = false,
            Review = review,
            Reasoning = "Function calling is disabled. Manual approval required."
        };
    }

    /// <summary>
    /// レビューと承認を一連のワークフローとして実行します（language指定）
    /// </summary>
    public async Task<ApprovalResult> ReviewAndApproveAsync(
        string owner,
        string repo,
        int prNumber,
        string language,
        ApprovalThreshold threshold = ApprovalThreshold.Minor,
        CancellationToken cancellationToken = default)
    {
        var useFunctionCalling = _config.AgentFramework?.EnableFunctionCalling ?? false;
        var useSubAgent = _config.AgentFramework?.UseSubAgent ?? true;

        if (useFunctionCalling)
        {
            var (reviewText, actionResult) = useSubAgent
                ? await _reviewAgent.ReviewWithLineCommentsAsync(owner, repo, prNumber, language, cancellationToken)
                : await _reviewAgent.ReviewDirectAsync(owner, repo, prNumber, language, cancellationToken);

            return new ApprovalResult
            {
                Approved = actionResult?.Approved ?? false,
                Review = reviewText,
                Reasoning = actionResult?.Message ?? string.Empty,
                ApprovalUrl = actionResult?.ApprovalUrl
            };
        }

        var review = await _reviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken: cancellationToken);
        return new ApprovalResult
        {
            Approved = false,
            Review = review,
            Reasoning = "Function calling is disabled. Manual approval required."
        };
    }

    /// <summary>
    /// AgentGroupChatを使用したマルチエージェント協調によるレビューと承認
    /// 現在はReviewAgentに統合されたため、ReviewAndApproveAsyncと同じ
    /// </summary>
    public async Task<ApprovalResult> ReviewAndApproveWithAgentChatAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold = ApprovalThreshold.Minor,
        CancellationToken cancellationToken = default)
    {
        return await ReviewAndApproveAsync(owner, repo, prNumber, threshold, cancellationToken);
    }

    /// <summary>
    /// カスタムワークフローを使用したレビューと承認
    /// </summary>
    public async Task<ApprovalResult> ReviewAndApproveWithCustomWorkflowAsync(
        string owner,
        string repo,
        int prNumber,
        string workflowType,
        ApprovalThreshold threshold = ApprovalThreshold.Minor,
        CancellationToken cancellationToken = default)
    {
        // すべてのワークフローはReviewAgentに統合
        return await ReviewAndApproveAsync(owner, repo, prNumber, threshold, cancellationToken);
    }
}

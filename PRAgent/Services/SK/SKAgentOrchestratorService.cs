using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using PRAgent.Agents;
using PRAgent.Agents.SK;
using PRAgent.Models;
using PRAgent.Plugins.Agent;
using PRAgent.Plugins.GitHub;
using PRAgentDefinition = PRAgent.Agents.AgentDefinition;

namespace PRAgent.Services.SK;

/// <summary>
/// Semantic Kernel AgentGroupChatを使用したエージェントオーケストレーションサービス
/// </summary>
public class SKAgentOrchestratorService : IAgentOrchestratorService
{
    private readonly PRAgentFactory _agentFactory;
    private readonly SKReviewAgent _reviewAgent;
    private readonly SKSummaryAgent _summaryAgent;
    private readonly SKApprovalAgent _approvalAgent;
    private readonly IGitHubService _gitHubService;
    private readonly PullRequestDataService _prDataService;
    private readonly PRAgentConfig _config;
    private readonly ILogger<SKAgentOrchestratorService> _logger;

    public SKAgentOrchestratorService(
        PRAgentFactory agentFactory,
        SKReviewAgent reviewAgent,
        SKSummaryAgent summaryAgent,
        SKApprovalAgent approvalAgent,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        PRAgentConfig config,
        ILogger<SKAgentOrchestratorService> logger)
    {
        _agentFactory = agentFactory;
        _reviewAgent = reviewAgent;
        _summaryAgent = summaryAgent;
        _approvalAgent = approvalAgent;
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
        return await _reviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// プルリクエストの要約を作成します
    /// </summary>
    public async Task<string> SummarizeAsync(string owner, string repo, int prNumber, CancellationToken cancellationToken = default)
    {
        return await _summaryAgent.SummarizeAsync(owner, repo, prNumber, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// レビューと承認を一連のワークフローとして実行します
    /// </summary>
    public async Task<ApprovalResult> ReviewAndApproveAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold,
        CancellationToken cancellationToken = default)
    {
        // ワークフロー: ReviewAgent → ApprovalAgent
        var review = await _reviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken: cancellationToken);

        var (shouldApprove, reasoning, comment) = await _approvalAgent.DecideAsync(
            owner, repo, prNumber, review, threshold, cancellationToken);

        string? approvalUrl = null;
        if (shouldApprove)
        {
            var result = await _approvalAgent.ApproveAsync(owner, repo, prNumber, comment);
            approvalUrl = result;
        }

        return new ApprovalResult
        {
            Approved = shouldApprove,
            Review = review,
            Reasoning = reasoning,
            Comment = comment,
            ApprovalUrl = approvalUrl
        };
    }

    /// <summary>
    /// プルリクエストのコードレビューを実行します（language指定）
    /// </summary>
    public async Task<string> ReviewAsync(string owner, string repo, int prNumber, string language, CancellationToken cancellationToken = default)
    {
        // languageパラメータは現在のSK実装では使用しない
        return await _reviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// プルリクエストの要約を作成します（language指定）
    /// </summary>
    public async Task<string> SummarizeAsync(string owner, string repo, int prNumber, string language, CancellationToken cancellationToken = default)
    {
        // languageパラメータは現在のSK実装では使用しない
        return await _summaryAgent.SummarizeAsync(owner, repo, prNumber, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// レビューと承認を一連のワークフローとして実行します（language指定）
    /// </summary>
    public async Task<ApprovalResult> ReviewAndApproveAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold,
        string language,
        CancellationToken cancellationToken = default)
    {
        // languageパラメータは現在のSK実装では使用しない
        return await ReviewAndApproveAsync(owner, repo, prNumber, threshold, cancellationToken);
    }

    /// <summary>
    /// AgentGroupChatを使用したマルチエージェント協調によるレビューと承認
    /// </summary>
    public async Task<ApprovalResult> ReviewAndApproveWithAgentChatAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold,
        CancellationToken cancellationToken = default)
    {
        // 設定されたオーケストレーションモードに応じて処理を分岐
        var orchestrationMode = _config.AgentFramework?.OrchestrationMode ?? "sequential";

        return orchestrationMode.ToLower() switch
        {
            "agent_chat" => await ExecuteWithAgentGroupChatAsync(owner, repo, prNumber, threshold, cancellationToken),
            "sequential" => await ReviewAndApproveAsync(owner, repo, prNumber, threshold, cancellationToken),
            _ => await ReviewAndApproveAsync(owner, repo, prNumber, threshold, cancellationToken)
        };
    }

    /// <summary>
    /// AgentGroupChatを使用した実行
    /// 注: Semantic Kernel 1.68.0のAgentGroupChat APIは複雑なため、簡易実装としています
    /// </summary>
    private async Task<ApprovalResult> ExecuteWithAgentGroupChatAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold,
        CancellationToken cancellationToken)
    {
        // 現在はSequentialモードとして実行（将来的に完全なAgentGroupChatを実装）
        return await ReviewAndApproveAsync(owner, repo, prNumber, threshold, cancellationToken);
    }

    /// <summary>
    /// カスタムワークフローを使用したレビューと承認
    /// </summary>
    public async Task<ApprovalResult> ReviewAndApproveWithCustomWorkflowAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold,
        string workflow,
        CancellationToken cancellationToken = default)
    {
        return workflow.ToLower() switch
        {
            "collaborative" => await CollaborativeReviewWorkflowAsync(owner, repo, prNumber, threshold, cancellationToken),
            "parallel" => await ParallelReviewWorkflowAsync(owner, repo, prNumber, threshold, cancellationToken),
            "sequential" => await ReviewAndApproveAsync(owner, repo, prNumber, threshold, cancellationToken),
            _ => await ReviewAndApproveAsync(owner, repo, prNumber, threshold, cancellationToken)
        };
    }

    /// <summary>
    /// 協調型レビューワークフロー - SummaryとReviewの両方を実行
    /// </summary>
    private async Task<ApprovalResult> CollaborativeReviewWorkflowAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold,
        CancellationToken cancellationToken)
    {
        // SummaryとReviewを並行して実行
        var summaryTask = _summaryAgent.SummarizeAsync(owner, repo, prNumber, cancellationToken: cancellationToken);
        var reviewTask = _reviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken: cancellationToken);

        await Task.WhenAll(summaryTask, reviewTask);

        var summary = await summaryTask;
        var review = await reviewTask;

        // 要約を含むレビュー結果を作成
        var enhancedReview = $"""
            {review}

            ## Summary
            {summary}
            """;

        var (shouldApprove, reasoning, comment) = await _approvalAgent.DecideAsync(
            owner, repo, prNumber, enhancedReview, threshold, cancellationToken);

        string? approvalUrl = null;
        if (shouldApprove)
        {
            var result = await _approvalAgent.ApproveAsync(owner, repo, prNumber, comment);
            approvalUrl = result;
        }

        return new ApprovalResult
        {
            Approved = shouldApprove,
            Review = enhancedReview,
            Reasoning = reasoning,
            Comment = comment,
            ApprovalUrl = approvalUrl
        };
    }

    /// <summary>
    /// 並列レビューワークフロー - 複数の視点からレビュー
    /// </summary>
    private async Task<ApprovalResult> ParallelReviewWorkflowAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold,
        CancellationToken cancellationToken)
    {
        // 様々な視点でレビューを実行
        var securityReviewTask = _reviewAgent.ReviewAsync(
            owner, repo, prNumber,
            "Focus specifically on security vulnerabilities, authentication issues, and data protection concerns.",
            cancellationToken);

        var performanceReviewTask = _reviewAgent.ReviewAsync(
            owner, repo, prNumber,
            "Focus specifically on performance implications, scalability concerns, and resource usage.",
            cancellationToken);

        var codeQualityReviewTask = _reviewAgent.ReviewAsync(
            owner, repo, prNumber,
            "Focus specifically on code quality, maintainability, and adherence to best practices.",
            cancellationToken);

        await Task.WhenAll(securityReviewTask, performanceReviewTask, codeQualityReviewTask);

        var securityReview = await securityReviewTask;
        var performanceReview = await performanceReviewTask;
        var codeQualityReview = await codeQualityReviewTask;

        // 統合レビューを作成
        var combinedReview = $"""
            ## Combined Code Review

            ### Security Review
            {securityReview}

            ### Performance Review
            {performanceReview}

            ### Code Quality Review
            {codeQualityReview}
            """;

        var (shouldApprove, reasoning, comment) = await _approvalAgent.DecideAsync(
            owner, repo, prNumber, combinedReview, threshold, cancellationToken);

        string? approvalUrl = null;
        if (shouldApprove)
        {
            var result = await _approvalAgent.ApproveAsync(owner, repo, prNumber, comment);
            approvalUrl = result;
        }

        return new ApprovalResult
        {
            Approved = shouldApprove,
            Review = combinedReview,
            Reasoning = reasoning,
            Comment = comment,
            ApprovalUrl = approvalUrl
        };
    }

    /// <summary>
    /// 関数呼び出し機能を持つApprovalエージェントを作成
    /// </summary>
    private async Task<ChatCompletionAgent> CreateApprovalAgentWithFunctionsAsync(
        string owner, string repo, int prNumber)
    {
        // 設定を反映したカスタムプロンプトを作成
        var customPrompt = _config.AgentFramework?.Agents?.Approval?.CustomSystemPrompt;

        // GitHub操作用のプラグインを作成
        var approveFunction = new ApprovePRFunction(_gitHubService, owner, repo, prNumber);
        var commentFunction = new PostCommentFunction(_gitHubService, owner, repo, prNumber);

        // 関数をKernelFunctionとして登録
        var functions = new List<KernelFunction>
        {
            ApprovePRFunction.ApproveAsyncFunction(_gitHubService, owner, repo, prNumber),
            ApprovePRFunction.GetApprovalStatusFunction(_gitHubService, owner, repo, prNumber),
            PostCommentFunction.PostCommentAsyncFunction(_gitHubService, owner, repo, prNumber),
            PostCommentFunction.PostLineCommentAsyncFunction(_gitHubService, owner, repo, prNumber)
        };

        return await _agentFactory.CreateApprovalAgentAsync(
            owner, repo, prNumber, customPrompt, functions);
    }

    /// <summary>
    /// 設定を反映したReviewエージェントを作成
    /// </summary>
    private async Task<ChatCompletionAgent> CreateConfiguredReviewAgentAsync(
        string owner, string repo, int prNumber)
    {
        var customPrompt = _config.AgentFramework?.Agents?.Review?.CustomSystemPrompt;
        return await _agentFactory.CreateReviewAgentAsync(owner, repo, prNumber, customPrompt);
    }

    /// <summary>
    /// 設定を反映したSummaryエージェントを作成
    /// </summary>
    private async Task<ChatCompletionAgent> CreateConfiguredSummaryAgentAsync(
        string owner, string repo, int prNumber)
    {
        var customPrompt = _config.AgentFramework?.Agents?.Summary?.CustomSystemPrompt;
        return await _agentFactory.CreateSummaryAgentAsync(owner, repo, prNumber, customPrompt);
    }
}

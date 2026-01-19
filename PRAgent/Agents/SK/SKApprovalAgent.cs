using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using PRAgent.Models;
using PRAgent.Services;
using PRAgent.Plugins.GitHub;
using PRAgentDefinition = PRAgent.Agents.AgentDefinition;

namespace PRAgent.Agents.SK;

/// <summary>
/// Semantic Kernel ChatCompletionAgentベースの承認エージェント
/// </summary>
public class SKApprovalAgent
{
    private readonly PRAgentFactory _agentFactory;
    private readonly IGitHubService _gitHubService;
    private readonly PullRequestDataService _prDataService;

    public SKApprovalAgent(
        PRAgentFactory agentFactory,
        IGitHubService gitHubService,
        PullRequestDataService prDataService)
    {
        _agentFactory = agentFactory;
        _gitHubService = gitHubService;
        _prDataService = prDataService;
    }

    /// <summary>
    /// レビュー結果に基づいて承認決定を行います
    /// </summary>
    public async Task<(bool ShouldApprove, string Reasoning, string? Comment)> DecideAsync(
        string owner,
        string repo,
        int prNumber,
        string reviewResult,
        ApprovalThreshold threshold,
        CancellationToken cancellationToken = default)
    {
        // PR情報を取得
        var pr = await _gitHubService.GetPullRequestAsync(owner, repo, prNumber);
        var thresholdDescription = ApprovalThresholdHelper.GetDescription(threshold);

        // プロンプトを作成
        var prompt = $"""
            Based on the code review below, make an approval decision for this pull request.

            ## Pull Request
            - Title: {pr.Title}
            - Author: {pr.User.Login}

            ## Code Review Result
            {reviewResult}

            ## Approval Threshold
            {thresholdDescription}

            Provide your decision in this format:

            DECISION: [APPROVE/REJECT]
            REASONING: [Explain why, listing any issues above the threshold]
            CONDITIONS: [Any conditions for merge, or N/A]
            APPROVAL_COMMENT: [Brief comment if approved, or N/A]

            Be conservative - when in doubt, reject or request additional review.
            """;

        // エージェントを作成して実行
        var agent = await _agentFactory.CreateApprovalAgentAsync(owner, repo, prNumber);

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        var responses = new System.Text.StringBuilder();
        await foreach (var response in agent.InvokeAsync(chatHistory, cancellationToken: cancellationToken))
        {
            responses.Append(response.Message.Content);
        }

        return ApprovalResponseParser.Parse(responses.ToString());
    }

    /// <summary>
    /// プルリクエストを承認します
    /// </summary>
    public async Task<string> ApproveAsync(
        string owner,
        string repo,
        int prNumber,
        string? comment = null)
    {
        var result = await _gitHubService.ApprovePullRequestAsync(owner, repo, prNumber, comment);
        return $"PR approved: {result.HtmlUrl}";
    }

    /// <summary>
    /// レビューと承認を自動的に行います（承認条件を満たす場合）
    /// </summary>
    public async Task<(bool Approved, string Reasoning)> ReviewAndApproveAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold,
        CancellationToken cancellationToken = default)
    {
        // まずReviewエージェントを呼び出してレビューを実行
        var reviewAgent = new SKReviewAgent(_agentFactory, _prDataService);
        var reviewResult = await reviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken: cancellationToken);

        // 決定を行う
        var (shouldApprove, reasoning, comment) = await DecideAsync(
            owner, repo, prNumber, reviewResult, threshold, cancellationToken);

        // 承認する場合、実際に承認を実行
        if (shouldApprove)
        {
            await ApproveAsync(owner, repo, prNumber, comment);
            return (true, $"Approved: {reasoning}");
        }

        return (false, $"Not approved: {reasoning}");
    }

    /// <summary>
    /// GitHub操作関数を持つApprovalエージェントを作成します
    /// </summary>
    public async Task<ChatCompletionAgent> CreateAgentWithGitHubFunctionsAsync(
        string owner,
        string repo,
        int prNumber,
        string? customSystemPrompt = null)
    {
        var kernel = _agentFactory;

        // GitHub操作用のプラグインを作成
        var approveFunction = new ApprovePRFunction(_gitHubService, owner, repo, prNumber);
        var commentFunction = new PostCommentFunction(_gitHubService, owner, repo, prNumber);

        // 関数をKernelFunctionとして登録
        var functions = new List<KernelFunction>
        {
            ApprovePRFunction.ApproveAsyncFunction(_gitHubService, owner, repo, prNumber),
            ApprovePRFunction.GetApprovalStatusFunction(_gitHubService, owner, repo, prNumber),
            PostCommentFunction.PostCommentAsyncFunction(_gitHubService, owner, repo, prNumber),
            PostCommentFunction.PostReviewCommentAsyncFunction(_gitHubService, owner, repo, prNumber),
            PostCommentFunction.PostLineCommentAsyncFunction(_gitHubService, owner, repo, prNumber)
        };

        return await _agentFactory.CreateApprovalAgentAsync(
            owner, repo, prNumber, customSystemPrompt, functions);
    }

    /// <summary>
    /// 関数呼び出し機能を持つApprovalエージェントを使用して決定を行います
    /// </summary>
    public async Task<(bool ShouldApprove, string Reasoning, string? Comment)> DecideWithFunctionCallingAsync(
        string owner,
        string repo,
        int prNumber,
        string reviewResult,
        ApprovalThreshold threshold,
        bool autoApprove = false,
        CancellationToken cancellationToken = default)
    {
        // GitHub関数を持つエージェントを作成
        var agent = await CreateAgentWithGitHubFunctionsAsync(owner, repo, prNumber);

        // PR情報を取得
        var pr = await _gitHubService.GetPullRequestAsync(owner, repo, prNumber);
        var thresholdDescription = ApprovalThresholdHelper.GetDescription(threshold);

        // プロンプトを作成
        var autoApproveInstruction = autoApprove
            ? "If the decision is to APPROVE, use the approve_pull_request function to actually approve the PR."
            : "";

        var prompt = $"""
            Based on the code review below, make an approval decision for this pull request.

            ## Pull Request
            - Title: {pr.Title}
            - Author: {pr.User.Login}

            ## Code Review Result
            {reviewResult}

            ## Approval Threshold
            {thresholdDescription}

            Your task:
            1. Analyze the review results against the approval threshold
            2. Make a decision (APPROVE or REJECT)
            3. {autoApproveInstruction}
            4. If there are specific concerns that need to be addressed, use post_pr_comment or post_line_comment

            Provide your decision in this format:

            DECISION: [APPROVE/REJECT]
            REASONING: [Explain why, listing any issues above the threshold]
            CONDITIONS: [Any conditions for merge, or N/A]
            APPROVAL_COMMENT: [Brief comment if approved, or N/A]

            Be conservative - when in doubt, reject or request additional review.
            """;

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        var responses = new System.Text.StringBuilder();
        await foreach (var response in agent.InvokeAsync(chatHistory, cancellationToken: cancellationToken))
        {
            responses.Append(response.Message.Content);
        }

        // レスポンスを解析
        return ApprovalResponseParser.Parse(responses.ToString());
    }
}

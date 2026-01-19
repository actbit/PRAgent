using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
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
    /// バッファを使用してGitHub操作関数を持つApprovalエージェントを作成します
    /// </summary>
    public async Task<ChatCompletionAgent> CreateAgentWithBufferAsync(
        string owner,
        string repo,
        int prNumber,
        PRActionBuffer buffer,
        string? customSystemPrompt = null)
    {
        // プラグインインスタンスを作成
        var approvePlugin = new ApprovePRFunction(buffer);
        var commentPlugin = new PostCommentFunction(buffer);

        // Kernelを作成してプラグインを登録
        var kernel = _agentFactory.CreateApprovalKernel(owner, repo, prNumber, customSystemPrompt);
        kernel.ImportPluginFromObject(approvePlugin, "ApprovePR");
        kernel.ImportPluginFromObject(commentPlugin, "PostComment");

        // エージェントを作成
        var agent = new ChatCompletionAgent
        {
            Name = AgentDefinition.ApprovalAgent.Name,
            Description = AgentDefinition.ApprovalAgent.Description,
            Instructions = customSystemPrompt ?? AgentDefinition.ApprovalAgent.SystemPrompt,
            Kernel = kernel
        };

        return await Task.FromResult(agent);
    }

    /// <summary>
    /// バッファリングパターンを使用して決定を行い、完了後にアクションを一括実行します
    /// </summary>
    public async Task<(bool ShouldApprove, string Reasoning, string? Comment, PRActionResult? ActionResult)> DecideWithFunctionCallingAsync(
        string owner,
        string repo,
        int prNumber,
        string reviewResult,
        ApprovalThreshold threshold,
        bool autoApprove = false,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        // バッファを作成
        var buffer = new PRActionBuffer();

        // バッファを使用したエージェントを作成
        var agent = await CreateAgentWithBufferAsync(owner, repo, prNumber, buffer);

        // PR情報を取得
        var pr = await _gitHubService.GetPullRequestAsync(owner, repo, prNumber);
        var thresholdDescription = ApprovalThresholdHelper.GetDescription(threshold);

        // プロンプトを作成
        var autoApproveInstruction = autoApprove
            ? "If the decision is to APPROVE, use the approve_pull_request function to add approval to buffer."
            : "If the decision is to APPROVE, clearly state DECISION: APPROVE in your response.";

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
            2. Make a decision (APPROVE, CHANGES_REQUESTED, or COMMENT_ONLY)
            3. {autoApproveInstruction}
            4. If the decision is CHANGES_REQUESTED, use the request_changes function to add changes requested to buffer.
            5. If there are specific concerns that need to be addressed, use:
               - post_pr_comment for general comments
               - post_line_comment for specific line-level feedback
               - post_review_comment for review-level comments

            All actions will be buffered and executed after your analysis is complete.

            Provide your decision in this format:

            DECISION: [APPROVE/CHANGES_REQUESTED/COMMENT_ONLY]
            REASONING: [Explain why, listing any issues above the threshold]
            CONDITIONS: [Any conditions for merge, or N/A]
            APPROVAL_COMMENT: [Brief comment if approved, or N/A]

            Be conservative - when in doubt, request changes or add comments.
            """;

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        // エージェントを実行（関数呼び出しはバッファに追加される）
        var responses = new System.Text.StringBuilder();
        await foreach (var response in agent.InvokeAsync(chatHistory, cancellationToken: cancellationToken))
        {
            responses.Append(response.Message.Content);
        }

        var responseText = responses.ToString();

        // レスポンスを解析
        var (shouldApprove, reasoning, comment) = ApprovalResponseParser.Parse(responseText);

        // バッファの内容を実行
        PRActionResult? actionResult = null;
        var executor = new PRActionExecutor(_gitHubService, owner, repo, prNumber);
        var state = buffer.GetState();

        if (state.LineCommentCount > 0 || state.ReviewCommentCount > 0 ||
            state.HasGeneralComment || state.ApprovalState != PRApprovalState.None)
        {
            actionResult = await executor.ExecuteAsync(buffer, cancellationToken);
        }

        return (shouldApprove, reasoning, comment, actionResult);
    }
}

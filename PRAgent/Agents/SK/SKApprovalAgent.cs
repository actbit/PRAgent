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
        string? customSystemPrompt = null,
        string? language = null)
    {
        // プラグインインスタンスを作成
        var approvePlugin = new ApprovePRFunction(buffer);
        var commentPlugin = new PostCommentFunction(buffer);

        // Kernelを作成してプラグインを登録
        var kernel = _agentFactory.CreateApprovalKernel(owner, repo, prNumber, customSystemPrompt);
        kernel.ImportPluginFromObject(approvePlugin);
        kernel.ImportPluginFromObject(commentPlugin);

        // 言語に応じたシステムプロンプトを作成
        var systemPrompt = customSystemPrompt ?? GetSystemPrompt(language);

        // エージェントを作成
        var agent = new ChatCompletionAgent
        {
            Name = AgentDefinition.ApprovalAgent.Name,
            Description = AgentDefinition.ApprovalAgent.Description,
            Instructions = systemPrompt,
            Kernel = kernel
        };

        return await Task.FromResult(agent);
    }

    /// <summary>
    /// 言語に応じたシステムプロンプトを取得します
    /// </summary>
    private static string GetSystemPrompt(string? language)
    {
        var isJapanese = language?.ToLowerInvariant() == "ja";

        if (isJapanese)
        {
            return $"""
                あなたはプルリクエストの承認決定を行うシニアテクニカルリードです。

                あなたの役割:
                1. コードレビュー結果を分析
                2. 承認基準に照らして評価
                3. 保守的でリスクを考慮した承認決定を行う
                4. 判断について明確な理由を提供

                ## 利用可能な関数
                以下の関数を呼び出してアクションをバッファに追加してください:
                - approve_pull_request - PRを承認
                - request_changes - 変更を依頼
                - post_pr_comment - 全体コメントを追加
                - post_line_comment - 特定行にコメントを追加
                - post_range_comment - 複数行にコメントを追加（開始行と終了行を指定）
                - post_review_comment - レビューレベルのコメントを追加

                ## 関数呼び出しの方法
                関数を呼び出すには、関数名と必要なパラメータを明記してください:
                例: 「approve_pull_request関数を呼び出します。コメント: 良好です」

                すべてのアクションはバッファに追加され、分析完了後に一括でGitHubに投稿されます。

                ## 承認基準
                - critical: 重大な問題が0件であること
                - major: 重大または重大な問題が0件であること
                - minor: 軽微、重大、重大な問題が0件であること
                - none: 常に承認

                不確実な場合は、慎重を期して追加レビューまたは変更依頼を推奨します。
                """;
        }
        else
        {
            return $"""
                You are a senior technical lead responsible for making approval decisions on pull requests.

                Your role is to:
                1. Analyze code review results
                2. Evaluate findings against approval thresholds
                3. Make conservative, risk-aware approval decisions
                4. Provide clear reasoning for your decisions

                ## Available Functions
                Call the following functions to add actions to buffer:
                - approve_pull_request - Approve the PR
                - request_changes - Request changes
                - post_pr_comment - Add a general comment
                - post_line_comment - Add a comment to a specific line
                - post_range_comment - Add a comment to a range of lines
                - post_review_comment - Add a review-level comment

                ## How to Call Functions
                Explicitly state that you are calling a function with its parameters:
                Example: "I will call the approve_pull_request function with comment: Looks good."

                All actions will be buffered and posted to GitHub after your analysis is complete.

                ## Approval Thresholds
                - critical: PR must have NO critical issues
                - major: PR must have NO major or critical issues
                - minor: PR must have NO minor, major, or critical issues
                - none: Always approve

                When in doubt, err on the side of caution and recommend rejection or additional review.
                """;
        }
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

        // バッファを使用したエージェントを作成（言語指定）
        var agent = await CreateAgentWithBufferAsync(owner, repo, prNumber, buffer, language: language);

        // PR情報を取得
        var pr = await _gitHubService.GetPullRequestAsync(owner, repo, prNumber);
        var thresholdDescription = ApprovalThresholdHelper.GetDescription(threshold);

        // プロンプトを作成
        var autoApproveInstruction = autoApprove
            ? "判断が承認（APPROVE）の場合は、approve_pull_request関数を呼び出して承認をバッファに追加してください。"
            : "判断が承認（APPROVE）の場合は、DECISION: APPROVEと明確に記載してください。";

        var prompt = $"""
            コードレビューの結果に基づいて、このプルリクエストの承認判断を行ってください。

            ## プルリクエスト
            - タイトル: {pr.Title}
            - 作成者: {pr.User.Login}

            ## コードレビュー結果
            {reviewResult}

            ## 承認基準
            {thresholdDescription}

            あなたのタスク:
            1. レビュー結果を承認基準と照らして分析
            2. 判断を下してください（APPROVE、CHANGES_REQUESTED、COMMENT_ONLYのいずれか）
            3. {autoApproveInstruction}
            4. 判断がCHANGES_REQUESTEDの場合、request_changes関数を呼び出して変更依頼をバッファに追加してください
            5. 対処すべき懸念事項がある場合は、以下の関数を呼び出してください:
               - post_pr_comment - 全般的なコメント
               - post_line_comment - 特定行へのフィードバック
               - post_range_comment - 複数行へのフィードバック（開始行と終了行を指定）
               - post_review_comment - レビューレベルのコメント

            重要: すべてのアクションはバッファに追加され、分析完了後に一括で実行されます。

            以下の形式で判断を記載してください:

            DECISION: [APPROVE/CHANGES_REQUESTED/COMMENT_ONLY]
            REASONING: [判断理由を説明]
            CONDITIONS: [マージ条件があれば記載、なければN/A]
            APPROVAL_COMMENT: [承認時のコメント、なければN/A]

            不確かな場合は、変更依頼またはコメントを追加してください。
            """;

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        // FunctionCallingを有効にするために、Kernelから直接サービスを呼び出し
        var kernel = agent.Kernel;
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        // OpenAI用の実行設定でFunctionCallingを有効化
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var responses = new System.Text.StringBuilder();

        // 関数呼び出しを含む可能性があるため、複数回の反復処理を行う
        var maxIterations = 10;
        var iteration = 0;

        while (iteration < maxIterations)
        {
            iteration++;

            // 非ストリーミングAPIで完全なレスポンスを取得
            var contents = await chatService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                kernel,
                cancellationToken);

            var content = contents.FirstOrDefault();
            if (content == null) break;

            var currentResponse = content.Content ?? string.Empty;
            responses.Append(currentResponse);
            chatHistory.AddAssistantMessage(currentResponse);

            // 関数呼び出しが行われたかチェック
            var hasFunctionCalls = content.Items?.Any(i => i is FunctionCallContent) == true;

            // 関数呼び出しがない場合はループを抜ける
            if (!hasFunctionCalls)
            {
                break;
            }
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

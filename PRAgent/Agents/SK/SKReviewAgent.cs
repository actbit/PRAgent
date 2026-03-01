using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PRAgent.Models;
using PRAgent.Services;
using PRAgent.Plugins.GitHub;
using PRAgentDefinition = PRAgent.Agents.AgentDefinition;
using Octokit;

namespace PRAgent.Agents.SK;

/// <summary>
/// Semantic Kernel ChatCompletionAgentベースのレビューエージェント
/// ReviewAgent（概要）とDetailedCommentAgent（詳細）を連携させ、一つのReviewとして投稿
/// </summary>
public class SKReviewAgent
{
    private readonly PRAgentFactory _agentFactory;
    private readonly PullRequestDataService _prDataService;
    private readonly IGitHubService _gitHubService;
    private readonly IDetailedCommentAgent _detailedCommentAgent;

    public SKReviewAgent(
        PRAgentFactory agentFactory,
        PullRequestDataService prDataService,
        IGitHubService gitHubService,
        IDetailedCommentAgent detailedCommentAgent)
    {
        _agentFactory = agentFactory;
        _prDataService = prDataService;
        _gitHubService = gitHubService;
        _detailedCommentAgent = detailedCommentAgent;
    }

    /// <summary>
    /// プルリクエストのコードレビューを実行します
    /// </summary>
    public async Task<string> ReviewAsync(
        string owner,
        string repo,
        int prNumber,
        string? customSystemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        // Reviewエージェントを作成
        var agent = await _agentFactory.CreateReviewAgentAsync(owner, repo, prNumber, customSystemPrompt);

        // PRデータを取得
        var (pr, files, diff) = await _prDataService.GetPullRequestDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        // プロンプトを作成
        var systemPrompt = customSystemPrompt ?? PRAgentDefinition.ReviewAgent.SystemPrompt;
        var prompt = PullRequestDataService.CreateReviewPrompt(pr, fileList, diff, systemPrompt);

        // チャット履歴を作成してエージェントを実行
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        var responses = new System.Text.StringBuilder();
        await foreach (var response in agent.InvokeAsync(chatHistory, cancellationToken: cancellationToken))
        {
            responses.Append(response.Message.Content);
        }

        return responses.ToString();
    }

    /// <summary>
    /// ストリーミングでコードレビューを実行します
    /// </summary>
    public async IAsyncEnumerable<string> ReviewStreamingAsync(
        string owner,
        string repo,
        int prNumber,
        string? customSystemPrompt = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Reviewエージェントを作成
        var agent = await _agentFactory.CreateReviewAgentAsync(owner, repo, prNumber, customSystemPrompt);

        // PRデータを取得
        var (pr, files, diff) = await _prDataService.GetPullRequestDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        // プロンプトを作成
        var systemPrompt = customSystemPrompt ?? PRAgentDefinition.ReviewAgent.SystemPrompt;
        var prompt = PullRequestDataService.CreateReviewPrompt(pr, fileList, diff, systemPrompt);

        // チャット履歴を作成してエージェントを実行
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        await foreach (var response in agent.InvokeAsync(chatHistory, cancellationToken: cancellationToken))
        {
            yield return response.Message.Content ?? string.Empty;
        }
    }

    /// <summary>
    /// ReviewAgent（概要）とDetailedCommentAgent（詳細）を連携させたレビューを実行
    /// 一つのReviewとしてまとめて投稿
    /// </summary>
    public async Task<(string ReviewText, PRActionResult? ActionResult)> ReviewWithLineCommentsAsync(
        string owner,
        string repo,
        int prNumber,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        // PRデータを取得
        var (pr, files, diff) = await _prDataService.GetPullRequestDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        // ===== バッファとKernelを準備 =====
        var buffer = new PRActionBuffer();
        var reviewSystemPrompt = GetReviewOverviewPrompt(language);

        // Kernelを作成してツールを登録
        var kernel = _agentFactory.CreateReviewKernel(owner, repo, prNumber, reviewSystemPrompt);

        // Approve/RequestChangesツールを登録
        var approvePlugin = new ApprovePRFunction(buffer);
        kernel.Plugins.AddFromObject(approvePlugin, "pr_actions");

        // Function Calling設定
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        // ===== Step 1: ReviewAgentで概要を作成（Function Calling有効） =====
        var reviewPrompt = $"""
            以下のプルリクエストのコードレビューを行ってください。

            ## プルリクエスト情報
            - タイトル: {pr.Title}
            - 作成者: {pr.User.Login}
            - 説明: {pr.Body}

            ## 変更されたファイル
            {fileList}

            ## 差分
            {diff}

            ## レビュー指示
            1. 全体的な概要を3-5行程度で作成
            2. 発見した問題点をリストアップ（各問題には重要度: Critical/Major/Minor を付与）
            3. レビュー完了後、以下のアクションをとってください:
               - Criticalな問題がある場合: request_changes ツールを使用
               - 問題がない、またはMinorのみの場合: approve_pull_request ツールを使用

            出力形式:
            ## 概要
            [全体的な概要]

            ## 発見した問題
            ### [Critical/Major/Minor] 問題タイトル
            **ファイル:** `path/to/file.cs`
            **行番号:** 45
            **説明:** 問題の詳細説明
            **修正提案:**
            ```suggestion
            // 修正内容
            ```
            """;

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(reviewPrompt);

        var reviewResponses = new System.Text.StringBuilder();
        await foreach (var content in chatService.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken))
        {
            reviewResponses.Append(content.Content);
        }

        var reviewText = reviewResponses.ToString();

        // ===== Step 2: DetailedCommentAgentで詳細な行コメントを作成 =====
        var detailedComments = await _detailedCommentAgent.CreateCommentsAsync(reviewText, language ?? "en");

        // ===== Step 3: バッファにコメントを追加 =====
        // 概要をレビューコメントとして追加
        buffer.AddReviewComment(reviewText);

        // 詳細な行コメントを追加
        foreach (var comment in detailedComments)
        {
            buffer.AddLineComment(comment.Path, comment.Line, comment.Body, comment.Suggestion);
        }

        // ===== Step 4: アクションを実行 =====
        PRActionResult? actionResult = null;
        var executor = new PRActionExecutor(_gitHubService, owner, repo, prNumber);
        var state = buffer.GetState();

        if (state.LineCommentCount > 0 || state.ReviewCommentCount > 0 || state.ApprovalState != PRApprovalState.None)
        {
            actionResult = await executor.ExecuteAsync(buffer, cancellationToken);
        }

        return (reviewText, actionResult);
    }

    /// <summary>
    /// 言語に応じた概要レビュープロンプトを取得します
    /// </summary>
    private static string GetReviewOverviewPrompt(string? language)
    {
        var isJapanese = language?.ToLowerInvariant() == "ja";

        if (isJapanese)
        {
            return """
                あなたはシニアソフトウェアエンジニアとしてプルリクエストのコードレビューを行います。

                ## 役割
                1. コードの全体的な概要を作成（3-5行程度）
                2. 発見した問題点をリストアップ
                3. 各問題には重要度を付与（Critical/Major/Minor）

                ## 出力形式
                ## 概要
                [全体的な概要]

                ## 発見した問題
                ### [Critical/Major/Minor] 問題タイトル
                **ファイル:** `path/to/file.cs`
                **行番号:** 45
                **説明:** 問題の詳細説明
                **修正提案:**
                ```suggestion
                // 修正内容
                ```

                簡潔で建設的なフィードバックを心がけてください。
                """;
        }
        else
        {
            return """
                You are a senior software engineer performing code reviews on pull requests.

                ## Your Role
                1. Create a brief overview (3-5 lines)
                2. List all issues found with severity (Critical/Major/Minor)
                3. For each issue, include file path, line number, description, and suggestion

                ## Output Format
                ## Overview
                [Brief overview of the changes]

                ## Issues Found
                ### [Critical/Major/Minor] Issue Title
                **File:** `path/to/file.cs`
                **Line:** 45
                **Description:** Detailed explanation of the issue
                **Suggestion:**
                ```suggestion
                // Fixed code
                ```

                Keep feedback concise and constructive.
                """;
        }
    }
}

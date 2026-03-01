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
    /// Function CallingでDetailedCommentAgentを呼び出し
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
        _detailedCommentAgent.SetLanguage(language ?? "en");

        var reviewSystemPrompt = GetReviewWithSubAgentPrompt(language);

        // Kernelを作成してツールを登録
        var kernel = _agentFactory.CreateReviewKernel(owner, repo, prNumber, reviewSystemPrompt);

        // ツールを登録
        var commentPlugin = new PostCommentFunction(buffer);
        var approvePlugin = new ApprovePRFunction(buffer);
        kernel.Plugins.AddFromObject(commentPlugin, "pr_actions");
        kernel.Plugins.AddFromObject(approvePlugin, "pr_actions");
        kernel.Plugins.AddFromObject(_detailedCommentAgent, "sub_agents");

        // Function Calling設定
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        // ===== ReviewAgentでレビュー実行（Function CallingでSubAgent呼び出し） =====
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
            利用可能なツールを使ってレビューを行ってください：

            1. **post_review_comment**: レビューの概要を投稿
               - 全体的な概要（3-5行程度）
               - レビューのまとめ

            2. **get_detailed_comment**: 個々の問題について詳細なコメントを生成
               - ファイルパス、行番号、周辺コード、問題の概要を指定
               - SubAgentが詳細なコメントと修正提案を生成
               - 各問題に対して呼び出す

            3. **post_line_comment**: 生成された詳細コメントを投稿
               - get_detailed_commentの結果を使って投稿

            4. **approve_pull_request**: PRを承認
               - Criticalな問題がない場合に使用

            5. **request_changes**: 変更を依頼
               - Criticalな問題がある場合に使用

            ## ワークフロー
            1. まず post_review_comment で概要を投稿
            2. 各問題について:
               a. get_detailed_comment で詳細なコメントを生成
               b. post_line_comment でコメントを投稿
            3. 最後に approve_pull_request または request_changes で判定
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

        // ===== アクションを実行 =====
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

    /// <summary>
    /// SubAgentありモード用のプロンプトを取得
    /// </summary>
    private static string GetReviewWithSubAgentPrompt(string? language)
    {
        var isJapanese = language?.ToLowerInvariant() == "ja";

        if (isJapanese)
        {
            return """
                あなたはシニアソフトウェアエンジニアとしてプルリクエストのコードレビューを行います。

                ## 役割
                提供されたツールを使ってレビューを行い、詳細なコメントはSubAgentに委任します。

                ## 利用可能なツール
                - post_review_comment: レビューの概要を投稿
                - get_detailed_comment: SubAgentを呼び出して詳細なコメントを生成
                - post_line_comment: 生成された詳細コメントを投稿
                - approve_pull_request: PRを承認
                - request_changes: 変更を依頼

                ## レビューの基準
                - Critical: セキュリティ脆弱性、バグ、データ損失の可能性
                - Major: パフォーマンス問題、保守性の問題
                - Minor: コードスタイル、軽微な改善提案

                ## ワークフロー
                1. コードを分析して問題を特定
                2. post_review_commentで概要を投稿
                3. 各問題について:
                   a. get_detailed_commentでSubAgentに詳細コメントを生成させる
                   b. post_line_commentでコメントを投稿
                4. Criticalな問題がなければapprove、あればrequest_changes

                重要: 行コメントの内容は必ず get_detailed_comment を使ってSubAgentに生成させてください。
                """;
        }
        else
        {
            return """
                You are a senior software engineer performing code reviews on pull requests.

                ## Your Role
                Review code using provided tools, delegating detailed comments to SubAgent.

                ## Available Tools
                - post_review_comment: Post review overview
                - get_detailed_comment: Call SubAgent to generate detailed comment
                - post_line_comment: Post the generated detailed comment
                - approve_pull_request: Approve the PR
                - request_changes: Request changes

                ## Review Criteria
                - Critical: Security vulnerabilities, bugs, data loss potential
                - Major: Performance issues, maintainability problems
                - Minor: Code style, minor improvements

                ## Workflow
                1. Analyze code and identify issues
                2. Post overview with post_review_comment
                3. For each issue:
                   a. Use get_detailed_comment to have SubAgent generate detailed comment
                   b. Post comment with post_line_comment
                4. approve if no Critical issues, request_changes if any

                Important: Always use get_detailed_comment to have SubAgent generate line comment content.
                """;
        }
    }
    public async Task<(string ReviewText, PRActionResult? ActionResult)> ReviewDirectAsync(
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
        var systemPrompt = GetDirectReviewPrompt(language);

        // Kernelを作成してツールを登録
        var kernel = _agentFactory.CreateReviewKernel(owner, repo, prNumber, systemPrompt);

        // すべてのツールを登録
        var commentPlugin = new PostCommentFunction(buffer);
        var approvePlugin = new ApprovePRFunction(buffer);
        kernel.Plugins.AddFromObject(commentPlugin, "pr_actions");
        kernel.Plugins.AddFromObject(approvePlugin, "pr_actions");

        // Function Calling設定
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        // ===== レビュープロンプト =====
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
            利用可能なツールを使ってレビューを行ってください：

            1. **post_review_comment**: レビューの概要を投稿（必須）
               - 全体的な概要（3-5行程度）
               - レビューのまとめ

            2. **post_line_comment**: 特定の行にコメントを投稿
               - ファイルパス、行番号、コメント内容を指定
               - 修正提案がある場合はsuggestionパラメータを使用

            3. **approve_pull_request**: PRを承認
               - Criticalな問題がない場合に使用

            4. **request_changes**: 変更を依頼
               - Criticalな問題がある場合に使用

            ## ワークフロー
            1. まず post_review_comment で概要を投稿
            2. 問題がある行に対して post_line_comment でコメント
            3. 最後に approve_pull_request または request_changes で判定
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

        // ===== アクションを実行 =====
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
    /// SubAgentなしモード用のプロンプトを取得
    /// </summary>
    private static string GetDirectReviewPrompt(string? language)
    {
        var isJapanese = language?.ToLowerInvariant() == "ja";

        if (isJapanese)
        {
            return """
                あなたはシニアソフトウェアエンジニアとしてプルリクエストのコードレビューを行います。

                ## 役割
                提供されたツールを使って、プルリクエストのレビューを行ってください。

                ## 利用可能なツール
                - post_review_comment: レビューの概要を投稿
                - post_line_comment: 特定の行にコメントを投稿
                - approve_pull_request: PRを承認
                - request_changes: 変更を依頼

                ## レビューの基準
                - Critical: セキュリティ脆弱性、バグ、データ損失の可能性
                - Major: パフォーマンス問題、保守性の問題
                - Minor: コードスタイル、軽微な改善提案

                ## ワークフロー
                1. コードを分析
                2. post_review_commentで概要を投稿
                3. 問題がある箇所にpost_line_commentでコメント
                4. Criticalな問題がなければapprove、あればrequest_changes

                簡潔で建設的なフィードバックを心がけてください。
                """;
        }
        else
        {
            return """
                You are a senior software engineer performing code reviews on pull requests.

                ## Your Role
                Use the provided tools to review pull requests.

                ## Available Tools
                - post_review_comment: Post review overview
                - post_line_comment: Post comment on specific line
                - approve_pull_request: Approve the PR
                - request_changes: Request changes

                ## Review Criteria
                - Critical: Security vulnerabilities, bugs, data loss potential
                - Major: Performance issues, maintainability problems
                - Minor: Code style, minor improvements

                ## Workflow
                1. Analyze the code
                2. Post overview with post_review_comment
                3. Comment on issues with post_line_comment
                4. approve if no Critical issues, request_changes if any

                Keep feedback concise and constructive.
                """;
        }
    }
}

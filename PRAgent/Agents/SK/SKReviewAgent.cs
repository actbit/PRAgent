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
/// </summary>
public class SKReviewAgent
{
    private readonly PRAgentFactory _agentFactory;
    private readonly PullRequestDataService _prDataService;
    private readonly IGitHubService _gitHubService;

    public SKReviewAgent(
        PRAgentFactory agentFactory,
        PullRequestDataService prDataService,
        IGitHubService gitHubService)
    {
        _agentFactory = agentFactory;
        _prDataService = prDataService;
        _gitHubService = gitHubService;
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
    /// 指定された関数（プラグイン）を持つReviewエージェントを作成します
    /// </summary>
    public async Task<ChatCompletionAgent> CreateAgentWithFunctionsAsync(
        string owner,
        string repo,
        int prNumber,
        IEnumerable<KernelFunction> functions,
        string? customSystemPrompt = null)
    {
        return await _agentFactory.CreateReviewAgentAsync(
            owner, repo, prNumber, customSystemPrompt, functions);
    }

    /// <summary>
    /// バッファを使用して行コメント付きレビューを実行します
    /// メインコメントは簡潔に保ち、詳細なフィードバックは行コメントとして投稿します
    /// </summary>
    public async Task<(string ReviewText, PRActionResult? ActionResult)> ReviewWithLineCommentsAsync(
        string owner,
        string repo,
        int prNumber,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        // バッファを作成
        var buffer = new PRActionBuffer();

        // プラグインインスタンスを作成
        var commentPlugin = new PostCommentFunction(buffer);

        // カスタムシステムプロンプトを作成（簡潔なメインコメント+行コメント重視）
        var systemPrompt = GetReviewWithLineCommentsPrompt(language);

        // Kernelを作成してプラグインを登録
        var kernel = _agentFactory.CreateApprovalKernel(owner, repo, prNumber, systemPrompt);
        kernel.ImportPluginFromObject(commentPlugin);

        // OpenAI用の実行設定でFunctionCallingを有効化
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        // エージェントを作成（ArgumentsでFunctionCallingを有効化）
        var agent = new ChatCompletionAgent
        {
            Name = AgentDefinition.ReviewAgent.Name,
            Description = AgentDefinition.ReviewAgent.Description,
            Instructions = systemPrompt,
            Kernel = kernel,
            Arguments = new KernelArguments(executionSettings)
        };

        // PRデータを取得
        var (pr, files, diff) = await _prDataService.GetPullRequestDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        // プロンプトを作成
        var prompt = $"""
            以下のプルリクエストをコードレビューしてください。

            ## プルリクエスト情報
            - タイトル: {pr.Title}
            - 作成者: {pr.User.Login}
            - 説明: {pr.Body}

            ## 変更されたファイル
            {fileList}

            ## 差分
            {diff}

            ## レビュー指示
            1. まず、post_review_comment関数を呼び出して、簡潔な全体レビュー（3-5行程度）を追加してください
            2. 各問題点に対して、post_line_comment関数を呼び出して行コメントを投稿してください
            3. 行コメントにはファイルパスと行番号を正確に指定してください
            4. 重大な問題には Critical、重要な問題には Major、軽微な問題には Minor のプレフィックスを付けてください

            重要: メインのレビューコメントは簡潔に保ち、詳細なフィードバックは行コメントとして投稿してください。
            """;

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        var responses = new System.Text.StringBuilder();

        // エージェントを実行（Function Callingは自動的に処理される）
        await foreach (var response in agent.InvokeAsync(chatHistory, cancellationToken: cancellationToken))
        {
            responses.Append(response.Message.Content);
        }

        var reviewText = responses.ToString();

        // バッファの内容を実行
        PRActionResult? actionResult = null;
        var executor = new PRActionExecutor(_gitHubService, owner, repo, prNumber);
        var state = buffer.GetState();

        if (state.LineCommentCount > 0 || state.ReviewCommentCount > 0 || state.HasGeneralComment)
        {
            actionResult = await executor.ExecuteAsync(buffer, cancellationToken);
        }

        return (reviewText, actionResult);
    }

    /// <summary>
    /// 言語に応じた行コメント重視のレビュープロンプトを取得します
    /// </summary>
    private static string GetReviewWithLineCommentsPrompt(string? language)
    {
        var isJapanese = language?.ToLowerInvariant() == "ja";

        if (isJapanese)
        {
            return """
                あなたはシニアソフトウェアエンジニアとしてプルリクエストのコードレビューを行います。

                ## 重要なルール
                1. メインのレビューコメントは簡潔に（3-5行程度）
                2. 詳細なフィードバックは行コメントとして投稿
                3. 各問題点に対して個別の行コメントを作成

                ## 利用可能な関数
                - post_review_comment: 全体的なレビューコメント（簡潔に）
                - post_line_comment: 特定の行にコメント（filePath, lineNumber, comment）
                - post_range_comment: 複数行にコメント（filePath, startLine, endLine, comment）
                - post_pr_comment: 全般的なコメント

                ## コメントの分類
                - [Critical]: 重大なバグ、セキュリティ問題
                - [Major]: 設計問題、パフォーマンス問題
                - [Minor]: スタイル、命名、軽微な改善

                ## 出力形式
                1. まず post_review_comment で簡潔な全体レビューを投稿
                2. 各問題点に対して post_line_comment で行コメントを投稿
                3. ファイルパスと行番号を正確に指定すること
                """;
        }
        else
        {
            return """
                You are a senior software engineer performing code reviews on pull requests.

                ## Important Rules
                1. Keep the main review comment concise (3-5 lines)
                2. Post detailed feedback as line comments
                3. Create individual line comments for each issue

                ## Available Functions
                - post_review_comment: Overall review comment (keep concise)
                - post_line_comment: Comment on specific line (filePath, lineNumber, comment)
                - post_range_comment: Comment on multiple lines (filePath, startLine, endLine, comment)
                - post_pr_comment: General comment

                ## Issue Classification
                - [Critical]: Critical bugs, security issues
                - [Major]: Design issues, performance problems
                - [Minor]: Style, naming, minor improvements

                ## Output Format
                1. First, post a concise overall review using post_review_comment
                2. For each issue, post a line comment using post_line_comment
                3. Ensure filePath and lineNumber are accurate
                """;
        }
    }
}

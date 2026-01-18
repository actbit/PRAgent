using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Octokit;
using PRAgent.Models;
using PRAgent.ReviewModels;
using PRAgent.Services;
using System.Text.Json;

namespace PRAgent.Agents;

/// <summary>
/// 統一されたレビューエージェント
/// Subagentを使用して問題点抽出とコメント生成を行う
/// </summary>
public class UnifiedReviewAgent
{
    private readonly ILogger<UnifiedReviewAgent> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IKernelService _kernelService;
    private readonly ReviewAnalysisAgent _reviewAnalysisAgent;
    private readonly CommentCreationAgent _commentCreationAgent;
    private readonly AISettings _aiSettings;

    public UnifiedReviewAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        AISettings aiSettings,
        ILogger<UnifiedReviewAgent> logger,
        IServiceProvider serviceProvider)
    {
        _kernelService = kernelService;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _aiSettings = aiSettings;

        // Loggerの型変換ができない場合は新しいLoggerインスタンスを作成
        var reviewAnalysisLogger = logger as ILogger<ReviewAnalysisAgent>;
        if (reviewAnalysisLogger == null)
        {
            _logger.LogWarning("Logger type mismatch for ReviewAnalysisAgent, creating new instance");
            reviewAnalysisLogger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<ReviewAnalysisAgent>();
        }

        _reviewAnalysisAgent = new ReviewAnalysisAgent(
            kernelService, gitHubService, prDataService, aiSettings, reviewAnalysisLogger);

        var commentCreationLogger = logger as ILogger<CommentCreationAgent>;
        if (commentCreationLogger == null)
        {
            _logger.LogWarning("Logger type mismatch for CommentCreationAgent, creating new instance");
            commentCreationLogger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<CommentCreationAgent>();
        }

        _commentCreationAgent = new CommentCreationAgent(
            kernelService, gitHubService, prDataService, aiSettings, commentCreationLogger);

        _logger.LogInformation("Subagents created successfully");
    }

    public void SetLanguage(string language)
    {
        // サブエージェントの言語も設定
        _reviewAnalysisAgent.GetTools().SetLanguage(language);
        _commentCreationAgent.GetTools().SetLanguage(language);
    }

    public async Task<ReviewResult> ReviewAsync(
        string owner,
        string repo,
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Starting Unified Review Agent ===");
        _logger.LogInformation("Language: {Language}", _aiSettings.Language);

        try
        {
            // 1. PRデータ取得
            var pr = await _serviceProvider.GetRequiredService<IGitHubService>().GetPullRequestAsync(owner, repo, prNumber);
            var (files, diff) = await GetPRDataAsync(owner, repo, prNumber);
            var fileList = PullRequestDataService.FormatFileList(files);

            // 2. レビュープロンプト作成
            var reviewPrompt = CreateReviewPrompt(pr, fileList, diff);

            _logger.LogInformation("=== UnifiedReviewAgent Review Prompt ===\n{Prompt}", reviewPrompt);

            // 3. Review Analysis Agentで問題点抽出
            _logger.LogInformation("=== Running ReviewAnalysisAgent ===");
            var analysisResult = await RunReviewAnalysisAgent(reviewPrompt);

            _logger.LogInformation("=== Extracted {Count} Issues ===", analysisResult?.Issues?.Count ?? 0);

            // 4. Comment Creation Agentでコメント生成
            _logger.LogInformation("=== Running CommentCreationAgent ===");
            var comments = await RunCommentCreationAgent(analysisResult);

            _logger.LogInformation("=== Generated {Count} Comments ===", comments?.Count ?? 0);

            // 5. レビューとコメントを投稿
            await PostReviewWithComments(owner, repo, prNumber, reviewPrompt, reviewPrompt, comments ?? new List<PRAgent.ReviewModels.DraftPullRequestReviewComment>());

            return new ReviewResult
            {
                Review = reviewPrompt,
                Comments = comments ?? new List<PRAgent.ReviewModels.DraftPullRequestReviewComment>(),
                AnalysisResult = analysisResult ?? new ReviewAnalysisResult()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform unified review");
            throw;
        }
    }

    private async Task<ReviewAnalysisResult> RunReviewAnalysisAgent(string reviewPrompt)
    {
        var kernel = _kernelService.CreateKernel();

        // Pluginを登録
        var reviewTools = new ReviewAnalysisTools(
            _serviceProvider.GetRequiredService<IGitHubService>(),
            _serviceProvider.GetRequiredService<PullRequestDataService>(),
            _logger as ILogger<ReviewAnalysisTools> ?? LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ReviewAnalysisTools>());

        kernel.ImportPluginFromFunctions("ReviewAnalysis", [
            KernelFunctionFactory.CreateFromMethod(reviewTools.ExtractReviewIssuesAsync, "ExtractReviewIssues"),
            KernelFunctionFactory.CreateFromMethod(reviewTools.ReadFileContentAsync, "ReadFileContent")
        ]);

        // プロンプトを構築
        var systemPrompt = $"""
            あなたは専門のコードレビューアです。
            以下のPull Requestをレビューし、ReviewAnalysis.ExtractReviewIssuesツールを使用して問題点を抽出してください。

            ツールの使い方:
            - ExtractReviewIssues: レビュー内容から問題点を抽出します

            レスポンスはツールの実行結果のみを出力してください。

            言語：{_aiSettings.Language}
            """;

        // プロンプトを実行
        var fullPrompt = $"{systemPrompt}\n\n{reviewPrompt}";

        try
        {
            var result = await kernel.InvokePromptAsync(fullPrompt, new KernelArguments {
                ["language"] = _aiSettings.Language
            });

            // 結果を解析してReviewAnalysisResultに変換
            return ParseToolResult<ReviewAnalysisResult>(result.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run ReviewAnalysisAgent");
            return new ReviewAnalysisResult();
        }
    }

    private async Task<List<PRAgent.ReviewModels.DraftPullRequestReviewComment>> RunCommentCreationAgent(ReviewAnalysisResult analysisResult)
    {
        var kernel = _kernelService.CreateKernel();

        // Pluginを登録
        var commentTools = new ReviewAnalysisTools(
            _serviceProvider.GetRequiredService<IGitHubService>(),
            _serviceProvider.GetRequiredService<PullRequestDataService>(),
            _logger as ILogger<ReviewAnalysisTools> ?? LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ReviewAnalysisTools>());

        kernel.ImportPluginFromFunctions("CommentCreation", [
            KernelFunctionFactory.CreateFromMethod(commentTools.GenerateReviewCommentsAsync, "GenerateReviewComments"),
            KernelFunctionFactory.CreateFromMethod(commentTools.ReadFileContentAsync, "ReadFileContent")
        ]);

        // プロンプトを構築
        var systemPrompt = $"""
            あなたはGitHubのコメント生成エージェントです。
            CommentCreation.GenerateReviewCommentsツールを使用して、抽出された問題点からGitHubプルリクエストコメントを作成してください。

            ツールの使い方:
            - GenerateReviewComments: 問題点からGitHubレビューコメントを生成します

            レスポンスはツールの実行結果のみを出力してください。

            言語：{_aiSettings.Language}
            """;

        // アナリシス結果をテキスト形式に変換
        var analysisText = analysisResult != null && analysisResult.Issues.Any()
            ? $"抽出された問題点:\n{string.Join("\n\n", analysisResult.Issues.Select(i => $"- {i.Level}: {i.Title}\n  {i.Description}\n  修正案: {i.Suggestion}"))}"
            : "問題点は見つかりませんでした。";

        var fullPrompt = $"{systemPrompt}\n\n{analysisText}";

        try
        {
            var result = await kernel.InvokePromptAsync(fullPrompt, new KernelArguments {
                ["language"] = _aiSettings.Language,
                ["analysis"] = analysisText
            });

            // 結果を解析してList<DraftPullRequestReviewComment>に変換
            return ParseToolResult<List<PRAgent.ReviewModels.DraftPullRequestReviewComment>>(result.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run CommentCreationAgent");
            return new List<PRAgent.ReviewModels.DraftPullRequestReviewComment>();
        }
    }

    private T ParseToolResult<T>(string result) where T : new()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(result))
            {
                _logger.LogWarning("Tool result is empty or whitespace for type {TypeName}", typeof(T).Name);
                return new T();
            }

            // JSONとしてパースを試みる
            return JsonSerializer.Deserialize<T>(result) ?? new T();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse tool result for type {TypeName}. Result: {Result}", typeof(T).Name, result);
            return new T();
        }
    }

    private async Task<(IReadOnlyList<PullRequestFile> files, string diff)> GetPRDataAsync(string owner, string repo, int prNumber)
    {
        var gitHubService = _serviceProvider.GetRequiredService<IGitHubService>();
        var prDataService = _serviceProvider.GetRequiredService<PullRequestDataService>();

        var files = await gitHubService.GetPullRequestFilesAsync(owner, repo, prNumber);
        var diff = await gitHubService.GetPullRequestDiffAsync(owner, repo, prNumber);

        return (files, diff);
    }

    private string CreateReviewPrompt(PullRequest pr, string fileList, string diff)
    {
        return $"""
            You are an expert code reviewer. Please review the following pull request:

            ## Pull Request Information
            - Title: {pr.Title}
            - Author: {pr.User.Login}
            - Branch: {pr.Head.Ref} → {pr.Base.Ref}
            - Files Changed: {fileList}
            - Diff: {diff}

            Please provide a comprehensive code review focusing on:
            1. Code quality and best practices
            2. Security vulnerabilities
            3. Performance considerations
            4. Readability and maintainability
            5. Potential bugs or issues
            6. Suggestions for improvements

            Output your review in the following format:

            ## Overall Assessment
            [Brief overall assessment]

            ## Code Quality
            [Detailed review findings]

            ## Security Considerations
            [Security analysis]

            ## Performance Analysis
            [Performance evaluation]

            ## Recommendations
            [Specific recommendations]

            Language: {_aiSettings.Language}
            """;
    }

    private async Task PostReviewWithComments(string owner, string repo, int prNumber, string review, string reviewContent, List<PRAgent.ReviewModels.DraftPullRequestReviewComment> comments)
    {
        try
        {
            _logger.LogInformation("=== Posting Review and Comments ===");

            // GitHub APIでレビューを投稿
            var gitHubService = _serviceProvider.GetRequiredService<IGitHubService>();

            // コメントを個別に投稿
            foreach (var comment in comments)
            {
                try
                {
                    if (string.IsNullOrEmpty(comment.Path))
                    {
                        _logger.LogWarning("Comment has empty file path. Skipping comment: {Body}", comment.Body);
                        continue;
                    }

                    if (!comment.Position.HasValue || comment.Position.Value <= 0)
                    {
                        _logger.LogWarning("Comment for file {FilePath} has invalid position: {Position}. Skipping comment.", comment.Path, comment.Position ?? 0);
                        continue;
                    }

                    await gitHubService.CreatePullRequestCommentAsync(
                        owner,
                        repo,
                        prNumber,
                        comment.Path,
                        comment.Position.Value,
                        comment.Body);

                    _logger.LogInformation("Comment posted successfully for file {FilePath} at line {Position}", comment.Path, comment.Position.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create comment for file {FilePath} at position {Position}", comment.Path, comment.Position ?? 0);
                }
            }

            _logger.LogInformation("Review and comments posted successfully to GitHub");
            _logger.LogInformation("Review length: {Length} characters", reviewContent.Length);
            _logger.LogInformation("Comments count: {Count}", comments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post review to GitHub");

            // エラーの場合はIssueCommentとして投稿
            var gitHubService2 = _serviceProvider.GetRequiredService<IGitHubService>();
            await gitHubService2.CreateIssueCommentAsync(owner, repo, prNumber,
                $"## 🤖 PRAgent Review (Fallback)\n\n{review}\n\n*Note: Failed to post as review comments, posted as issue comment instead.*");

            throw;
        }
    }
}
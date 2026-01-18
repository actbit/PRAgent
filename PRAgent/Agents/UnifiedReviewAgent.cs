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

            // 3. AIでレビュー生成
            var kernel = _kernelService.CreateKernel();
            var reviewContent = await _kernelService.InvokePromptAsStringAsync(kernel, reviewPrompt, cancellationToken);

            _logger.LogInformation("=== UnifiedReviewAgent Review Response ===\n{Response}", reviewContent);

            // 4. Subagentを使用して問題点抽出 - toolを自動で呼び出す
            _logger.LogInformation("=== Using ReviewAnalysisAgent with Auto Tool Invocation ===");
            var reviewAnalysisKernel = _reviewAnalysisAgent.GetKernel();

            // System Promptを設定
            var reviewSystemPrompt = $"あなたは専門のコードレビューアです。以下のレビュー結果から構造化された問題点を抽出してください。言語：{_aiSettings.Language}";
            reviewAnalysisKernel.ImportPluginFromFunctions("ReviewAnalysis", [
                KernelFunctionFactory.CreateFromMethod(_reviewAnalysisAgent.GetTools().ExtractReviewIssuesAsync, "ExtractReviewIssues"),
                KernelFunctionFactory.CreateFromMethod(_reviewAnalysisAgent.GetTools().GenerateReviewCommentsAsync, "GenerateReviewComments"),
                KernelFunctionFactory.CreateFromMethod(_reviewAnalysisAgent.GetTools().ReadFileContentAsync, "ReadFileContent")
            ]);

            // プロンプト実行
            var reviewPromptWithSystem = $"{reviewSystemPrompt}\n\n{reviewPrompt}";
            var analysisResult = await reviewAnalysisKernel.InvokeAsync<ReviewAnalysisResult>(
                "ReviewAnalysis", "ExtractReviewIssues",
                new KernelArguments
                {
                    ["reviewContent"] = reviewPromptWithSystem,
                    ["language"] = _aiSettings.Language
                });

            _logger.LogInformation("=== Extracted {Count} Issues ===", analysisResult.Issues.Count);

            // 5. Subagentを使用してコメント生成 - toolを自動で呼び出す
            _logger.LogInformation("=== Using CommentCreationAgent with Auto Tool Invocation ===");
            var commentCreationKernel = _commentCreationAgent.GetKernel();

            // System Promptを設定
            var commentSystemPrompt = $"あなたはGitHubのコメント生成エージェントです。抽出された問題点から、適切なGitHubプルリクエストコメントを作成してください。言語：{_aiSettings.Language}";
            commentCreationKernel.ImportPluginFromFunctions("CommentCreation", [
                KernelFunctionFactory.CreateFromMethod(_commentCreationAgent.GetTools().GenerateReviewCommentsAsync, "GenerateReviewComments"),
                KernelFunctionFactory.CreateFromMethod(_commentCreationAgent.GetTools().ReadFileContentAsync, "ReadFileContent")
            ]);

            // プロンプト実行
            var commentPrompt = $"{commentSystemPrompt}\n\n抽出された問題点:\n{JsonSerializer.Serialize(analysisResult)}";
            var comments = await commentCreationKernel.InvokeAsync<List<PRAgent.ReviewModels.DraftPullRequestReviewComment>>(
                "CommentCreation", "GenerateReviewComments",
                new KernelArguments
                {
                    ["analysis"] = analysisResult,
                    ["language"] = _aiSettings.Language
                });

            _logger.LogInformation("=== Generated {Count} Comments ===", comments.Count);

            // 6. レビューとコメントを投稿
            await PostReviewWithComments(owner, repo, prNumber, reviewContent, reviewContent, comments);

            return new ReviewResult
            {
                Review = reviewContent,
                Comments = comments,
                AnalysisResult = analysisResult
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform unified review");
            throw;
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
                    await gitHubService.CreatePullRequestCommentAsync(
                        owner,
                        repo,
                        prNumber,
                        comment.Path,
                        comment.Position ?? 0,
                        comment.Body);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create comment for file {FilePath}", comment.Path);
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
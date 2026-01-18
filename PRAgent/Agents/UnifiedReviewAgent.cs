using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Octokit;
using PRAgent.Models;
using PRAgent.ReviewModels;
using PRAgent.Services;

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

        // Subagentを作成
        var reviewAnalysisLogger = logger as ILogger<ReviewAnalysisAgent>;
        _reviewAnalysisAgent = new ReviewAnalysisAgent(
            kernelService, gitHubService, prDataService, aiSettings,
            reviewAnalysisLogger ?? throw new ArgumentNullException(nameof(logger)));

        var commentCreationLogger = logger as ILogger<CommentCreationAgent>;
        _commentCreationAgent = new CommentCreationAgent(
            kernelService, gitHubService, prDataService, aiSettings,
            commentCreationLogger ?? throw new ArgumentNullException(nameof(logger)));
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

            // 4. Subagentを使用して問題点抽出
            _logger.LogInformation("=== Using ReviewAnalysisAgent ===");
            var reviewAnalysisKernel = _reviewAnalysisAgent.GetKernel();
            var analysisResult = await reviewAnalysisKernel.InvokeAsync<PRAgent.ReviewModels.ReviewAnalysisResult>(
                "ReviewAnalysis", "ExtractReviewIssues",
                new KernelArguments
                {
                    ["reviewContent"] = reviewContent,
                    ["language"] = _aiSettings.Language
                });

            _logger.LogInformation("=== Extracted {Count} Issues ===", analysisResult.Issues.Count);

            // 5. Subagentを使用してコメント生成
            _logger.LogInformation("=== Using CommentCreationAgent ===");
            var commentCreationKernel = _commentCreationAgent.GetKernel();
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

    private async Task PostReviewWithComments(string owner, string repo, int prNumber, string review, string reviewContent, List<ReviewModels.DraftPullRequestReviewComment> comments)
    {
        try
        {
            _logger.LogInformation("=== Posting Review and Comments ===");

            // GitHub APIでレビューを投稿
            var gitHubService = _serviceProvider.GetRequiredService<IGitHubService>();

            // レビューはGitHubに直接投稿せず、結果を返す
            // 実際のGitHub投稿は呼び出し側で行う

            _logger.LogInformation("Review and comments generated successfully");
            _logger.LogInformation("Review length: {Length} characters", reviewContent.Length);
            _logger.LogInformation("Comments count: {Count}", comments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post review to GitHub");
            throw;
        }
    }
}
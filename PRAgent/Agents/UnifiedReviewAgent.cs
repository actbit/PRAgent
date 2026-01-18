using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Octokit;
using PRAgent.Models;
using PRAgent.Services;

namespace PRAgent.Agents;

/// <summary>
/// 統一されたレビューエージェント
/// Tool呼び出しを使用して問題点抽出とコメント生成を行う
/// </summary>
public class UnifiedReviewAgent : ReviewAgentBase
{
    private readonly IKernelService _kernelService;
    private readonly ILogger<UnifiedReviewAgent> _logger;

    public UnifiedReviewAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        AISettings aiSettings,
        ILogger<UnifiedReviewAgent> logger,
        string? customSystemPrompt = null)
        : base(kernelService, gitHubService, prDataService, aiSettings, AgentDefinition.ReviewAgent, customSystemPrompt)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// 統一されたレビュー実行
    /// </summary>
    public async Task<ReviewResult> ReviewAsync(
        string owner,
        string repo,
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Language: {Language}", AISettings.Language);
        SetLanguage(AISettings.Language);

        // 1. PRデータ取得とプロンプット作成
        var (pr, files, diff) = await GetPRDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        var reviewPrompt = CreateReviewPrompt(pr, fileList, diff);

        // プロンプット出力
        _logger.LogInformation("=== UnifiedReviewAgent Review Prompt ===\n{Prompt}", reviewPrompt);

        // 2. AIでレビュー生成
        var review = await _kernelService.InvokePromptAsStringAsync(CreateKernel(), reviewPrompt, cancellationToken);

        _logger.LogInformation("=== UnifiedReviewAgent Review Response ===\n{Response}", review);

        // 3. Tool呼び出しで問題点抽出
        var analysisResult = await ExtractReviewIssuesWithTool(review);

        // 4. Tool呼び出しでコメント生成
        var comments = await GenerateReviewCommentsWithTool(analysisResult);

        // 5. レビューとコメントを投稿
        await PostReviewWithComments(owner, repo, prNumber, review, comments);

        return new ReviewResult
        {
            Review = review,
            Comments = comments,
            AnalysisResult = analysisResult
        };
    }

    /// <summary>
    /// Toolを使用してレビュー問題点を抽出
    /// </summary>
    private async Task<ReviewAnalysisResult> ExtractReviewIssuesWithTool(string review)
    {
        try
        {
            // Kernelを作成してToolを登録
            var kernel = _kernelService.CreateKernel();
            kernel.ImportPluginFromType<ReviewAnalysisTools>();

            // Tool呼び出しで問題点抽出
            var analysisResult = await kernel.InvokeAsync<ReviewAnalysisResult>(
                kernel.Plugins["ReviewAnalysisTools"]["ExtractReviewIssues"],
                new KernelArguments
                {
                    ["reviewContent"] = review,
                    ["language"] = AISettings.Language
                });

            _logger.LogInformation("=== Extracted Issues ===");
            foreach (var issue in analysisResult.Issues)
            {
                _logger.LogInformation("Issue: {Title} - {Level} - {File}:{Line}",
                    issue.Title, issue.Level, issue.FilePath, issue.StartLine);
            }

            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract review issues using tool");
            throw;
        }
    }

    /// <summary>
    /// Toolを使用してレビューコメントを生成
    /// </summary>
    private async Task<List<DraftPullRequestReviewComment>> GenerateReviewCommentsWithTool(ReviewAnalysisResult analysisResult)
    {
        try
        {
            // Kernelを作成してToolを登録
            var kernel = _kernelService.CreateKernel();
            kernel.ImportPluginFromType<ReviewAnalysisTools>();

            // Tool呼び出しでコメント生成
            var comments = await kernel.InvokeAsync<List<DraftPullRequestReviewComment>>(
                kernel.Plugins["ReviewAnalysisTools"]["GenerateReviewComments"],
                new KernelArguments
                {
                    ["analysisResult"] = analysisResult,
                    ["language"] = AISettings.Language
                });

            _logger.LogInformation("=== Generated Comments ===");
            foreach (var comment in comments)
            {
                _logger.LogInformation("Comment for {File}:{Line}", comment.Path, comment.Position);
            }

            return comments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate review comments using tool");
            throw;
        }
    }

    /// <summary>
    /// レビューとコメントをGitHubに投稿
    /// </summary>
    private async Task PostReviewWithComments(
        string owner,
        string repo,
        int prNumber,
        string review,
        List<DraftPullRequestReviewComment> comments)
    {
        try
        {
            // PRのHEADコミットIDを取得
            var pr = await GitHubService.GetPullRequestAsync(owner, repo, prNumber);
            var commitId = pr.Head.Sha;

            // レビューとコメントを一度に投稿
            await GitHubService.CreateReviewWithCommentsAsync(
                owner,
                repo,
                prNumber,
                commitId,
                review,
                comments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post review to GitHub");
            throw;
        }
    }

    /// <summary>
    /// レビュー用のプロンプットを作成
    /// </summary>
    private string CreateReviewPrompt(PullRequest pr, string fileList, string diff, string? customSystemPrompt = null)
    {
        var systemPrompt = customSystemPrompt ?? """
            You are an expert code reviewer with deep knowledge of software engineering best practices.
            Your role is to provide thorough, constructive, and actionable feedback on pull requests.

            When reviewing code, focus on:
            - Correctness and potential bugs
            - Security vulnerabilities
            - Performance considerations
            - Code organization and readability
            - Adherence to best practices and design patterns
            - Test coverage and quality

            **Output Format:**
            - Organize findings by individual issue/problem
            - Each issue should be a separate section starting with ###
            - Use severity labels: [CRITICAL], [MAJOR], [MINOR], [POSITIVE]
            - For each issue, include:
              * File path (extract from review or use a reasonable default)
              * Line numbers (if applicable)
              * Detailed description
              * Specific code examples
              * Concrete suggestions

            Example:
            ### [CRITICAL] SQL Injection Vulnerability

            **File:** `src/Authentication.cs` (lines 45-52)

            **Problem:** User input is directly concatenated into SQL queries...

            **Suggested Fix:**
            ```suggestion
            // Use parameterized queries
            var query = "SELECT * FROM Users WHERE Id = @Id";
            ```
            """;

        return PullRequestDataService.CreateReviewPrompt(pr, fileList, diff, systemPrompt);
    }
}

/// <summary>
/// 統合されたレビュー結果
/// </summary>
public class ReviewResult
{
    public string Review { get; set; } = string.Empty;
    public List<DraftPullRequestReviewComment> Comments { get; set; } = new List<DraftPullRequestReviewComment>();
    public ReviewAnalysisResult? AnalysisResult { get; set; }
}
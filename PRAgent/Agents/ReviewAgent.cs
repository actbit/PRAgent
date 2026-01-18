using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Octokit;
using PRAgent.Models;
using PRAgent.Services;

namespace PRAgent.Agents;

public class ReviewAgent : ReviewAgentBase
{
    private readonly IDetailedCommentAgent _detailedCommentAgent;
    private readonly ILogger<ReviewAgent> _logger;

    public ReviewAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        AISettings aiSettings,
        IDetailedCommentAgent detailedCommentAgent,
        ILogger<ReviewAgent> logger,
        string? customSystemPrompt = null)
        : base(kernelService, gitHubService, prDataService, aiSettings, AgentDefinition.ReviewAgent, customSystemPrompt)
    {
        _detailedCommentAgent = detailedCommentAgent;
        _logger = logger;
    }

    public new void SetLanguage(string language) => base.SetLanguage(language);

    public async Task<string> ReviewAsync(
        string owner,
        string repo,
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Language: {Language}", AISettings.Language);
        var (pr, files, diff) = await GetPRDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        var systemPrompt = """
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

        var prompt = PullRequestDataService.CreateReviewPrompt(pr, fileList, diff, systemPrompt);

        // プロンプットを出力
        _logger.LogInformation("=== ReviewAgent Prompt ===\n{Prompt}", prompt);

        var review = await KernelService.InvokePromptAsStringAsync(CreateKernel(), prompt, cancellationToken);

        _logger.LogInformation("=== ReviewAgent Response ===\n{Response}", review);

        // サブエージェントに渡すために言語設定
        _detailedCommentAgent.SetLanguage(AISettings.Language);

        // サブエージェントで詳細なコメントを作成
        var comments = await _detailedCommentAgent.CreateCommentsAsync(review, AISettings.Language);

        return review;
    }
}
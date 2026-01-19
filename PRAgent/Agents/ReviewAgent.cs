using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PRAgent.Models;
using PRAgent.Services;
using PRAgent.Plugins;

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

    /// <summary>
    /// Function Callingを使用してレビューを実行し、アクションをバッファに蓄積します
    /// </summary>
    public async Task<string> ReviewWithActionsAsync(
        string owner,
        string repo,
        int prNumber,
        PRActionBuffer buffer,
        CancellationToken cancellationToken = default)
    {
        var (pr, files, diff) = await GetPRDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        // Kernelを作成して関数を登録
        var kernel = CreateKernel();
        var actionFunctions = new PRActionFunctions(buffer);
        kernel.ImportPluginFromObject(actionFunctions, "pr_actions");

        var systemPrompt = """
            You are an expert code reviewer with deep knowledge of software engineering best practices.
            Your role is to provide thorough, constructive, and actionable feedback on pull requests.

            You have access to the following functions to accumulate your review actions:
            - add_line_comment: Add a comment on a specific line of code
            - add_review_comment: Add a general review comment
            - add_summary: Add a summary of your review

            Instructions:
            1. Review the code changes thoroughly
            2. For each issue you find, use add_line_comment to mark the specific location
            3. If you have suggestions, include them in the line comment
            4. Use add_review_comment for general observations that don't apply to specific lines
            5. Use add_summary to provide an overall summary of your review
            6. When done, use ready_to_commit to indicate you're finished

            Only mark CRITICAL and MAJOR issues. Ignore MINOR issues unless there are many of them.

            File format for add_line_comment: use the exact file path as shown in the diff
            """;

        var prompt = $"""
            {systemPrompt}

            ## Pull Request Information
            - Title: {pr.Title}
            - Author: {pr.User.Login}
            - Description: {pr.Body ?? "No description provided"}
            - Branch: {pr.Head.Ref} -> {pr.Base.Ref}

            ## Changed Files
            {fileList}

            ## Diff
            {diff}

            Please review this pull request and use the available functions to add comments and mark issues.
            When you're done, call ready_to_commit.
            """;

        // 注: Semantic Kernel 1.68.0でのFunction Callingは、複雑なTool Call処理が必要です
        // 現在は簡易的な実装として、通常のレビューを実行します
        // 将来的には、Auto Commitオプションで完全なFunction Callingを実装予定

        var resultBuilder = new System.Text.StringBuilder();

        // 通常のレビューを実行して結果を取得
        var reviewResult = await KernelService.InvokePromptAsStringAsync(kernel, prompt, cancellationToken);
        resultBuilder.Append(reviewResult);

        // 注: 実際のFunction Calling実装時は、ここでTool Callを処理してバッファに追加

        // バッファの状態を追加（デモ用）
        var state = buffer.GetState();
        resultBuilder.AppendLine($"\n\n## Review Summary");
        resultBuilder.AppendLine($"Line comments added: {state.LineCommentCount}");
        resultBuilder.AppendLine($"Review comments added: {state.ReviewCommentCount}");
        resultBuilder.AppendLine($"Summaries added: {state.SummaryCount}");

        return resultBuilder.ToString();
    }
}

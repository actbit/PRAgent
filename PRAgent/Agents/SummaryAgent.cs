using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PRAgent.Models;
using PRAgent.Services;
using PRAgent.Plugins;

namespace PRAgent.Agents;

public class SummaryAgent : BaseAgent
{
    public SummaryAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        string? customSystemPrompt = null)
        : base(kernelService, gitHubService, prDataService, AgentDefinition.SummaryAgent, customSystemPrompt)
    {
    }

    public async Task<string> SummarizeAsync(
        string owner,
        string repo,
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        var (pr, files, diff) = await GetPRDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        var systemPrompt = """
            You are a technical writer specializing in creating clear, concise documentation.
            Your role is to summarize pull request changes accurately.
            """;

        var prompt = PullRequestDataService.CreateSummaryPrompt(pr, fileList, diff, systemPrompt);

        return await KernelService.InvokePromptAsStringAsync(CreateKernel(), prompt, cancellationToken);
    }

    /// <summary>
    /// Function Callingを使用してサマリーを作成し、アクションをバッファに蓄積します
    /// </summary>
    public async Task<string> SummarizeWithActionsAsync(
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
            You are a technical writer specializing in creating clear, concise documentation.
            Your role is to summarize pull request changes accurately.

            You have access to the following functions:
            - add_summary: Add a summary of the pull request
            - set_general_comment: Set a general comment to post
            - ready_to_commit: Indicate you're finished

            Instructions:
            1. Analyze the pull request changes
            2. Create a concise summary (under 300 words) using add_summary
            3. Optionally add additional context using set_general_comment
            4. When done, call ready_to_commit

            Focus on:
            - Purpose: What does this PR achieve?
            - Key Changes: Main files/components modified
            - Impact: Areas affected
            - Risk Assessment: Low/Medium/High with justification
            - Testing Notes: Areas needing special attention
            """;

        var prompt = $"""
            {systemPrompt}

            ## Pull Request
            - Title: {pr.Title}
            - Author: {pr.User.Login}
            - Description: {pr.Body ?? "No description provided"}
            - Branch: {pr.Head.Ref} -> {pr.Base.Ref}

            ## Changed Files
            {fileList}

            ## Diff
            {diff}

            Please summarize this pull request and use the available functions to add the summary.
            When you're done, call ready_to_commit.
            """;

        // 注: Semantic Kernel 1.68.0でのFunction Callingは、複雑なTool Call処理が必要です
        // 現在は簡易的な実装として、通常のサマリーを実行します
        // 将来的には、Auto Commitオプションで完全なFunction Callingを実装予定

        var resultBuilder = new System.Text.StringBuilder();

        // 通常のサマリーを実行して結果を取得
        var summaryResult = await KernelService.InvokePromptAsStringAsync(kernel, prompt, cancellationToken);
        resultBuilder.Append(summaryResult);

        // 注: 実際のFunction Calling実装時は、ここでTool Callを処理してバッファに追加

        // バッファの状態を追加（デモ用）
        var state = buffer.GetState();
        resultBuilder.AppendLine($"\n\n## Summary Summary");
        resultBuilder.AppendLine($"Summaries added: {state.SummaryCount}");
        resultBuilder.AppendLine($"General comment set: {state.HasGeneralComment}");

        return resultBuilder.ToString();
    }
}

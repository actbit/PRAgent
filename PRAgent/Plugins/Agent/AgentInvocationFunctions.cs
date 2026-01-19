using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using PRAgent.Agents;
using PRAgent.Services;
using PRAgentDefinition = PRAgent.Agents.AgentDefinition;

namespace PRAgent.Plugins.Agent;

/// <summary>
/// Agent-as-Functionパターンを実装するプラグイン
/// 他のエージェントを関数として呼び出すことを可能にします
/// </summary>
public class AgentInvocationFunctions
{
    private readonly PRAgentFactory _agentFactory;
    private readonly PullRequestDataService _prDataService;
    private readonly string _owner;
    private readonly string _repo;
    private readonly int _prNumber;

    public AgentInvocationFunctions(
        PRAgentFactory agentFactory,
        PullRequestDataService prDataService,
        string owner,
        string repo,
        int prNumber)
    {
        _agentFactory = agentFactory;
        _prDataService = prDataService;
        _owner = owner;
        _repo = repo;
        _prNumber = prNumber;
    }

    /// <summary>
    /// Reviewエージェントを呼び出してコードレビューを実行します
    /// </summary>
    /// <param name="customPrompt">カスタムプロンプト（オプション）</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>レビュー結果</returns>
    [KernelFunction("invoke_review_agent")]
    public async Task<string> InvokeReviewAgentAsync(
        string? customPrompt = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Reviewエージェントを作成
            var agent = await _agentFactory.CreateReviewAgentAsync(_owner, _repo, _prNumber);

            // PRデータを取得
            var (pr, files, diff) = await _prDataService.GetPullRequestDataAsync(_owner, _repo, _prNumber);
            var fileList = PullRequestDataService.FormatFileList(files);

            // プロンプトを作成
            var prompt = string.IsNullOrEmpty(customPrompt)
                ? PullRequestDataService.CreateReviewPrompt(pr, fileList, diff, PRAgentDefinition.ReviewAgent.SystemPrompt)
                : customPrompt;

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
        catch (Exception ex)
        {
            return $"Error invoking review agent: {ex.Message}";
        }
    }

    /// <summary>
    /// Summaryエージェントを呼び出して要約を作成します
    /// </summary>
    /// <param name="customPrompt">カスタムプロンプト（オプション）</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>要約結果</returns>
    [KernelFunction("invoke_summary_agent")]
    public async Task<string> InvokeSummaryAgentAsync(
        string? customPrompt = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Summaryエージェントを作成
            var agent = await _agentFactory.CreateSummaryAgentAsync(_owner, _repo, _prNumber);

            // PRデータを取得
            var (pr, files, diff) = await _prDataService.GetPullRequestDataAsync(_owner, _repo, _prNumber);
            var fileList = PullRequestDataService.FormatFileList(files);

            // プロンプトを作成
            var prompt = string.IsNullOrEmpty(customPrompt)
                ? PullRequestDataService.CreateSummaryPrompt(pr, fileList, diff, PRAgentDefinition.SummaryAgent.SystemPrompt)
                : customPrompt;

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
        catch (Exception ex)
        {
            return $"Error invoking summary agent: {ex.Message}";
        }
    }

    /// <summary>
    /// Reviewエージェントを関数として呼び出すためのKernelFunctionを作成します
    /// </summary>
    public static KernelFunction InvokeReviewAgentFunction(
        PRAgentFactory agentFactory,
        PullRequestDataService prDataService,
        string owner,
        string repo,
        int prNumber)
    {
        var invocationPlugin = new AgentInvocationFunctions(agentFactory, prDataService, owner, repo, prNumber);
        return KernelFunctionFactory.CreateFromMethod(
            (string? customPrompt, CancellationToken ct) => invocationPlugin.InvokeReviewAgentAsync(customPrompt, ct),
            functionName: "invoke_review_agent",
            description: "Invokes the review agent to perform code review on a pull request",
            parameters: new[]
            {
                new KernelParameterMetadata("customPrompt")
                {
                    Description = "Optional custom prompt for the review agent",
                    IsRequired = false,
                    DefaultValue = null
                }
            });
    }

    /// <summary>
    /// Summaryエージェントを関数として呼び出すためのKernelFunctionを作成します
    /// </summary>
    public static KernelFunction InvokeSummaryAgentFunction(
        PRAgentFactory agentFactory,
        PullRequestDataService prDataService,
        string owner,
        string repo,
        int prNumber)
    {
        var invocationPlugin = new AgentInvocationFunctions(agentFactory, prDataService, owner, repo, prNumber);
        return KernelFunctionFactory.CreateFromMethod(
            (string? customPrompt, CancellationToken ct) => invocationPlugin.InvokeSummaryAgentAsync(customPrompt, ct),
            functionName: "invoke_summary_agent",
            description: "Invokes the summary agent to create a summary of a pull request",
            parameters: new[]
            {
                new KernelParameterMetadata("customPrompt")
                {
                    Description = "Optional custom prompt for the summary agent",
                    IsRequired = false,
                    DefaultValue = null
                }
            });
    }

    /// <summary>
    /// Approvalエージェントを呼び出して承認決定を行います
    /// </summary>
    /// <param name="reviewResult">レビュー結果</param>
    /// <param name="threshold">承認閾値</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>承認決定結果</returns>
    [KernelFunction("invoke_approval_agent")]
    public async Task<string> InvokeApprovalAgentAsync(
        string reviewResult,
        string threshold = "major",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Approvalエージェントを作成
            var agent = await _agentFactory.CreateApprovalAgentAsync(_owner, _repo, _prNumber);

            // PRデータを取得
            var pr = await _prDataService.GetPullRequestDataAsync(_owner, _repo, _prNumber);

            // プロンプトを作成
            var thresholdDescription = GetThresholdDescription(threshold);
            var prompt = $"""
                Based on the code review below, make an approval decision for this pull request.

                ## Pull Request
                - Title: {pr.Item1.Title}
                - Author: {pr.Item1.User.Login}

                ## Code Review Result
                {reviewResult}

                ## Approval Threshold
                {thresholdDescription}

                Provide your decision in this format:

                DECISION: [APPROVE/REJECT]
                REASONING: [Explain why, listing any issues above the threshold]
                CONDITIONS: [Any conditions for merge, or N/A]
                APPROVAL_COMMENT: [Brief comment if approved, or N/A]

                Be conservative - when in doubt, reject or request additional review.
                """;

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
        catch (Exception ex)
        {
            return $"Error invoking approval agent: {ex.Message}";
        }
    }

    private static string GetThresholdDescription(string threshold)
    {
        return threshold.ToLower() switch
        {
            "critical" => "critical: PR must have NO critical issues",
            "major" => "major: PR must have NO major or critical issues",
            "minor" => "minor: PR must have NO minor, major, or critical issues",
            "none" => "none: Always approve",
            _ => "major: PR must have NO major or critical issues (default)"
        };
    }

    /// <summary>
    /// Approvalエージェントを関数として呼び出すためのKernelFunctionを作成します
    /// </summary>
    public static KernelFunction InvokeApprovalAgentFunction(
        PRAgentFactory agentFactory,
        PullRequestDataService prDataService,
        string owner,
        string repo,
        int prNumber)
    {
        var invocationPlugin = new AgentInvocationFunctions(agentFactory, prDataService, owner, repo, prNumber);
        return KernelFunctionFactory.CreateFromMethod(
            (string reviewResult, string threshold, CancellationToken ct) =>
                invocationPlugin.InvokeApprovalAgentAsync(reviewResult, threshold, ct),
            functionName: "invoke_approval_agent",
            description: "Invokes the approval agent to make an approval decision based on review results",
            parameters: new[]
            {
                new KernelParameterMetadata("reviewResult")
                {
                    Description = "The code review results to evaluate",
                    IsRequired = true
                },
                new KernelParameterMetadata("threshold")
                {
                    Description = "The approval threshold (critical, major, minor, none)",
                    IsRequired = false,
                    DefaultValue = "major"
                }
            });
    }
}

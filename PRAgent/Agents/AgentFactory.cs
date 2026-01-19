using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using PRAgent.Services;

namespace PRAgent.Agents;

/// <summary>
/// Semantic Kernel ChatCompletionAgentの作成を集中管理するファクトリクラス
/// </summary>
public class PRAgentFactory
{
    private readonly IKernelService _kernelService;
    private readonly IGitHubService _gitHubService;
    private readonly PullRequestDataService _prDataService;

    public PRAgentFactory(
        IKernelService kernelService,
        IGitHubService gitHubService,
        PullRequestDataService prDataService)
    {
        _kernelService = kernelService;
        _gitHubService = gitHubService;
        _prDataService = prDataService;
    }

    /// <summary>
    /// Reviewエージェントを作成
    /// </summary>
    public async Task<ChatCompletionAgent> CreateReviewAgentAsync(
        string owner,
        string repo,
        int prNumber,
        string? customSystemPrompt = null,
        IEnumerable<KernelFunction>? functions = null)
    {
        var kernel = _kernelService.CreateAgentKernel(AgentDefinition.ReviewAgent.SystemPrompt);

        if (functions != null)
        {
            foreach (var function in functions)
            {
                kernel.ImportPluginFromObject(function);
            }
        }

        var agent = new ChatCompletionAgent
        {
            Name = AgentDefinition.ReviewAgent.Name,
            Description = AgentDefinition.ReviewAgent.Description,
            Instructions = customSystemPrompt ?? AgentDefinition.ReviewAgent.SystemPrompt,
            Kernel = kernel
        };

        return await Task.FromResult(agent);
    }

    /// <summary>
    /// Summaryエージェントを作成
    /// </summary>
    public async Task<ChatCompletionAgent> CreateSummaryAgentAsync(
        string owner,
        string repo,
        int prNumber,
        string? customSystemPrompt = null,
        IEnumerable<KernelFunction>? functions = null)
    {
        var kernel = _kernelService.CreateAgentKernel(AgentDefinition.SummaryAgent.SystemPrompt);

        if (functions != null)
        {
            foreach (var function in functions)
            {
                kernel.ImportPluginFromObject(function);
            }
        }

        var agent = new ChatCompletionAgent
        {
            Name = AgentDefinition.SummaryAgent.Name,
            Description = AgentDefinition.SummaryAgent.Description,
            Instructions = customSystemPrompt ?? AgentDefinition.SummaryAgent.SystemPrompt,
            Kernel = kernel
        };

        return await Task.FromResult(agent);
    }

    /// <summary>
    /// Approvalエージェントを作成
    /// </summary>
    public async Task<ChatCompletionAgent> CreateApprovalAgentAsync(
        string owner,
        string repo,
        int prNumber,
        string? customSystemPrompt = null,
        IEnumerable<KernelFunction>? functions = null)
    {
        var kernel = _kernelService.CreateAgentKernel(AgentDefinition.ApprovalAgent.SystemPrompt);

        // GitHub操作用のプラグインを登録
        if (functions != null)
        {
            foreach (var function in functions)
            {
                kernel.ImportPluginFromObject(function);
            }
        }

        var agent = new ChatCompletionAgent
        {
            Name = AgentDefinition.ApprovalAgent.Name,
            Description = AgentDefinition.ApprovalAgent.Description,
            Instructions = customSystemPrompt ?? AgentDefinition.ApprovalAgent.SystemPrompt,
            Kernel = kernel,
            Arguments = new KernelArguments
            {
                // Approvalエージェント用の特殊設定
                ["approval_mode"] = true,
                ["owner"] = owner,
                ["repo"] = repo,
                ["pr_number"] = prNumber
            }
        };

        return await Task.FromResult(agent);
    }

    /// <summary>
    /// カスタムエージェントを作成（汎用メソッド）
    /// </summary>
    public async Task<ChatCompletionAgent> CreateCustomAgentAsync(
        string name,
        string description,
        string systemPrompt,
        string owner,
        string repo,
        int prNumber,
        IEnumerable<KernelFunction>? functions = null,
        KernelArguments? arguments = null)
    {
        var kernel = _kernelService.CreateAgentKernel(systemPrompt);

        if (functions != null)
        {
            foreach (var function in functions)
            {
                kernel.ImportPluginFromObject(function);
            }
        }

        var agent = new ChatCompletionAgent
        {
            Name = name,
            Description = description,
            Instructions = systemPrompt,
            Kernel = kernel,
            Arguments = arguments
        };

        return await Task.FromResult(agent);
    }

    /// <summary>
    /// 複数のエージェントを一度に作成
    /// </summary>
    public async Task<(ChatCompletionAgent reviewAgent, ChatCompletionAgent summaryAgent, ChatCompletionAgent approvalAgent)> CreateAllAgentsAsync(
        string owner,
        string repo,
        int prNumber,
        string? customReviewPrompt = null,
        string? customSummaryPrompt = null,
        string? customApprovalPrompt = null)
    {
        var reviewAgent = await CreateReviewAgentAsync(owner, repo, prNumber, customReviewPrompt);
        var summaryAgent = await CreateSummaryAgentAsync(owner, repo, prNumber, customSummaryPrompt);
        var approvalAgent = await CreateApprovalAgentAsync(owner, repo, prNumber, customApprovalPrompt);

        return (reviewAgent, summaryAgent, approvalAgent);
    }
}

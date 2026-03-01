using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
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
    /// Reviewエージェント用のKernelを作成（FunctionCalling有効）
    /// </summary>
    public Kernel CreateReviewKernel(
        string owner,
        string repo,
        int prNumber,
        string? customSystemPrompt = null)
    {
        return _kernelService.CreateAgentKernel(
            customSystemPrompt ?? AgentDefinition.ReviewAgent.SystemPrompt);
    }

    /// <summary>
    /// Approvalエージェント用のKernelを作成（後方互換性のため残す）
    /// </summary>
    [Obsolete("Use CreateReviewKernel instead. Approval functionality is now integrated into ReviewAgent.")]
    public Kernel CreateApprovalKernel(
        string owner,
        string repo,
        int prNumber,
        string? customSystemPrompt = null)
    {
        return CreateReviewKernel(owner, repo, prNumber, customSystemPrompt);
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
}

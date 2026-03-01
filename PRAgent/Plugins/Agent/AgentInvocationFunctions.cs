using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using PRAgent.Agents;
using PRAgent.Services;
using PRAgentDefinition = PRAgent.Agents.AgentDefinition;

namespace PRAgent.Plugins.Agent;

/// <summary>
/// Agent-as-Functionパターンを実装するプラグイン
/// ReviewAgentを関数として呼び出すことを可能にします
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
}

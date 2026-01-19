using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using PRAgent.Services;
using PRAgentDefinition = PRAgent.Agents.AgentDefinition;

namespace PRAgent.Agents.SK;

/// <summary>
/// Semantic Kernel ChatCompletionAgentベースのサマリーエージェント
/// </summary>
public class SKSummaryAgent
{
    private readonly PRAgentFactory _agentFactory;
    private readonly PullRequestDataService _prDataService;

    public SKSummaryAgent(
        PRAgentFactory agentFactory,
        PullRequestDataService prDataService)
    {
        _agentFactory = agentFactory;
        _prDataService = prDataService;
    }

    /// <summary>
    /// プルリクエストの要約を作成します
    /// </summary>
    public async Task<string> SummarizeAsync(
        string owner,
        string repo,
        int prNumber,
        string? customSystemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        // Summaryエージェントを作成
        var agent = await _agentFactory.CreateSummaryAgentAsync(owner, repo, prNumber, customSystemPrompt);

        // PRデータを取得
        var (pr, files, diff) = await _prDataService.GetPullRequestDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        // プロンプトを作成
        var systemPrompt = customSystemPrompt ?? PRAgentDefinition.SummaryAgent.SystemPrompt;
        var prompt = PullRequestDataService.CreateSummaryPrompt(pr, fileList, diff, systemPrompt);

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

    /// <summary>
    /// ストリーミングで要約を作成します
    /// </summary>
    public async IAsyncEnumerable<string> SummarizeStreamingAsync(
        string owner,
        string repo,
        int prNumber,
        string? customSystemPrompt = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Summaryエージェントを作成
        var agent = await _agentFactory.CreateSummaryAgentAsync(owner, repo, prNumber, customSystemPrompt);

        // PRデータを取得
        var (pr, files, diff) = await _prDataService.GetPullRequestDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        // プロンプトを作成
        var systemPrompt = customSystemPrompt ?? PRAgentDefinition.SummaryAgent.SystemPrompt;
        var prompt = PullRequestDataService.CreateSummaryPrompt(pr, fileList, diff, systemPrompt);

        // チャット履歴を作成してエージェントを実行
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        await foreach (var response in agent.InvokeAsync(chatHistory, cancellationToken: cancellationToken))
        {
            yield return response.Message.Content ?? string.Empty;
        }
    }

    /// <summary>
    /// 指定された関数（プラグイン）を持つSummaryエージェントを作成します
    /// </summary>
    public async Task<ChatCompletionAgent> CreateAgentWithFunctionsAsync(
        string owner,
        string repo,
        int prNumber,
        IEnumerable<KernelFunction> functions,
        string? customSystemPrompt = null)
    {
        return await _agentFactory.CreateSummaryAgentAsync(
            owner, repo, prNumber, customSystemPrompt, functions);
    }
}

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using PRAgent.Services;
using PRAgentDefinition = PRAgent.Agents.AgentDefinition;

namespace PRAgent.Agents.SK;

/// <summary>
/// Semantic Kernel ChatCompletionAgentベースのレビューエージェント
/// </summary>
public class SKReviewAgent
{
    private readonly PRAgentFactory _agentFactory;
    private readonly PullRequestDataService _prDataService;

    public SKReviewAgent(
        PRAgentFactory agentFactory,
        PullRequestDataService prDataService)
    {
        _agentFactory = agentFactory;
        _prDataService = prDataService;
    }

    /// <summary>
    /// プルリクエストのコードレビューを実行します
    /// </summary>
    public async Task<string> ReviewAsync(
        string owner,
        string repo,
        int prNumber,
        string? customSystemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        // Reviewエージェントを作成
        var agent = await _agentFactory.CreateReviewAgentAsync(owner, repo, prNumber, customSystemPrompt);

        // PRデータを取得
        var (pr, files, diff) = await _prDataService.GetPullRequestDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        // プロンプトを作成
        var systemPrompt = customSystemPrompt ?? PRAgentDefinition.ReviewAgent.SystemPrompt;
        var prompt = PullRequestDataService.CreateReviewPrompt(pr, fileList, diff, systemPrompt);

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
    /// ストリーミングでコードレビューを実行します
    /// </summary>
    public async IAsyncEnumerable<string> ReviewStreamingAsync(
        string owner,
        string repo,
        int prNumber,
        string? customSystemPrompt = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Reviewエージェントを作成
        var agent = await _agentFactory.CreateReviewAgentAsync(owner, repo, prNumber, customSystemPrompt);

        // PRデータを取得
        var (pr, files, diff) = await _prDataService.GetPullRequestDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        // プロンプトを作成
        var systemPrompt = customSystemPrompt ?? PRAgentDefinition.ReviewAgent.SystemPrompt;
        var prompt = PullRequestDataService.CreateReviewPrompt(pr, fileList, diff, systemPrompt);

        // チャット履歴を作成してエージェントを実行
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        await foreach (var response in agent.InvokeAsync(chatHistory, cancellationToken: cancellationToken))
        {
            yield return response.Message.Content ?? string.Empty;
        }
    }

    /// <summary>
    /// 指定された関数（プラグイン）を持つReviewエージェントを作成します
    /// </summary>
    public async Task<ChatCompletionAgent> CreateAgentWithFunctionsAsync(
        string owner,
        string repo,
        int prNumber,
        IEnumerable<KernelFunction> functions,
        string? customSystemPrompt = null)
    {
        return await _agentFactory.CreateReviewAgentAsync(
            owner, repo, prNumber, customSystemPrompt, functions);
    }
}

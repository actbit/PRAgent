using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PRAgent.Models;

namespace PRAgent.Services;

public class KernelService : IKernelService
{
    private readonly AISettings _aiSettings;
    private ILogger? _logger;

    public KernelService(AISettings aiSettings)
    {
        _aiSettings = aiSettings;
    }

    public void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    public Kernel CreateKernel(string? systemPrompt = null)
    {
        var builder = Kernel.CreateBuilder();

        var endpoint = _aiSettings.Endpoint;

        // エンドポイントが指定されている場合はカスタムエンドポイントを使用
        if (!string.IsNullOrEmpty(endpoint))
        {
            _logger?.LogInformation("Using custom endpoint: {Endpoint}", endpoint);
            builder.Services.AddOpenAIChatCompletion(
                modelId: _aiSettings.ModelId,
                apiKey: _aiSettings.ApiKey,
                endpoint: new Uri(endpoint)
            );
        }
        else
        {
            _logger?.LogInformation("Using default OpenAI endpoint");
            builder.AddOpenAIChatCompletion(
                modelId: _aiSettings.ModelId,
                apiKey: _aiSettings.ApiKey
            );
        }

        var kernel = builder.Build();

        return kernel;
    }

    public Kernel CreateAgentKernel(string? systemPrompt = null)
    {
        var builder = Kernel.CreateBuilder();

        var endpoint = _aiSettings.Endpoint;

        // エンドポイントが指定されている場合はカスタムエンドポイントを使用
        if (!string.IsNullOrEmpty(endpoint))
        {
            _logger?.LogInformation("Using custom endpoint: {Endpoint}", endpoint);
            builder.Services.AddOpenAIChatCompletion(
                modelId: _aiSettings.ModelId,
                apiKey: _aiSettings.ApiKey,
                endpoint: new Uri(endpoint)
            );
        }
        else
        {
            _logger?.LogInformation("Using default OpenAI endpoint");
            builder.AddOpenAIChatCompletion(
                modelId: _aiSettings.ModelId,
                apiKey: _aiSettings.ApiKey
            );
        }

        var kernel = builder.Build();

        return kernel;
    }

    public Kernel RegisterFunctionPlugins(Kernel kernel, IEnumerable<object> plugins)
    {
        foreach (var plugin in plugins)
        {
            kernel.ImportPluginFromObject(plugin);
        }

        return kernel;
    }

    public Kernel RegisterFunctionPlugin(Kernel kernel, object plugin, string? pluginName = null)
    {
        if (!string.IsNullOrEmpty(pluginName))
        {
            kernel.ImportPluginFromObject(plugin, pluginName);
        }
        else
        {
            kernel.ImportPluginFromObject(plugin);
        }

        return kernel;
    }

    public async IAsyncEnumerable<string> InvokePromptAsync(
        Kernel kernel,
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // プロンプトを出力
        _logger?.LogInformation("=== KernelService Prompt ===\n{Prompt}", prompt);

        var service = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        // ストリーミングを実行（リトライなし）
        await foreach (var content in service.GetStreamingChatMessageContentsAsync(chatHistory, cancellationToken: cancellationToken))
        {
            yield return content.Content ?? string.Empty;
        }

        _logger?.LogInformation("=== KernelService Response (Streaming) ===\n{Response}", "<streaming response>");
    }

    public async Task<string> InvokePromptAsStringAsync(
        Kernel kernel,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        // プロンプトを出力
        _logger?.LogInformation("=== KernelService Prompt ===\n{Prompt}", prompt);

        var service = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        // リトライ付きでストリーミングを実行して結果を収集
        var results = await RetryHelper.ExecuteStreamingWithRetryAsync(
            () => service.GetStreamingChatMessageContentsAsync(chatHistory, cancellationToken: cancellationToken),
            "InvokePromptAsync",
            _logger,
            cancellationToken);

        var response = string.Join("", results.Select(c => c.Content ?? string.Empty));
        _logger?.LogInformation("=== KernelService Response ===\n{Response}", response);

        return response;
    }
}

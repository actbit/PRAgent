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
        if (!string.IsNullOrEmpty(endpoint) && endpoint != "https://api.openai.com/v1")
        {
            builder.Services.AddOpenAIChatCompletion(
                modelId: _aiSettings.ModelId,
                apiKey: _aiSettings.ApiKey,
                endpoint: new Uri(endpoint)
            );
        }
        else
        {
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
        if (!string.IsNullOrEmpty(endpoint) && endpoint != "https://api.openai.com/v1")
        {
            builder.Services.AddOpenAIChatCompletion(
                modelId: _aiSettings.ModelId,
                apiKey: _aiSettings.ApiKey,
                endpoint: new Uri(endpoint)
            );
        }
        else
        {
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
        // プロンプットを出力
        _logger?.LogInformation("=== KernelService Prompt ===\n{Prompt}", prompt);

        var service = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        await foreach (var content in service.GetStreamingChatMessageContentsAsync(chatHistory, cancellationToken: cancellationToken))
        {
            var responseContent = content.Content ?? string.Empty;
            yield return responseContent;
        }

        _logger?.LogInformation("=== KernelService Response (Streaming) ===\n{Response}", "<streaming response>");
    }

    public async Task<string> InvokePromptAsStringAsync(
        Kernel kernel,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        // プロンプットを出力
        _logger?.LogInformation("=== KernelService Prompt ===\n{Prompt}", prompt);

        var resultBuilder = new System.Text.StringBuilder();

        await foreach (var content in InvokePromptAsync(kernel, prompt, cancellationToken))
        {
            resultBuilder.Append(content);
        }

        var response = resultBuilder.ToString();
        _logger?.LogInformation("=== KernelService Response ===\n{Response}", response);

        return response;
    }
}

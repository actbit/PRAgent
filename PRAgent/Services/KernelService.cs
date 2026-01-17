using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PRAgent.Models;

namespace PRAgent.Services;

public class KernelService : IKernelService
{
    private readonly AISettings _aiSettings;

    public KernelService(AISettings aiSettings)
    {
        _aiSettings = aiSettings;
    }

    public Kernel CreateKernel(string? systemPrompt = null)
    {
        var builder = Kernel.CreateBuilder();

        builder.AddOpenAIChatCompletion(
            modelId: _aiSettings.ModelId,
            apiKey: _aiSettings.ApiKey,
            endpoint: new Uri(_aiSettings.Endpoint)
        );

        var kernel = builder.Build();

        return kernel;
    }

    public async IAsyncEnumerable<string> InvokePromptAsync(
        Kernel kernel,
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var service = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        await foreach (var content in service.GetStreamingChatMessageContentsAsync(chatHistory, cancellationToken: cancellationToken))
        {
            yield return content.Content ?? string.Empty;
        }
    }

    public async Task<string> InvokePromptAsStringAsync(
        Kernel kernel,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var resultBuilder = new System.Text.StringBuilder();

        await foreach (var content in InvokePromptAsync(kernel, prompt, cancellationToken))
        {
            resultBuilder.Append(content);
        }

        return resultBuilder.ToString();
    }
}

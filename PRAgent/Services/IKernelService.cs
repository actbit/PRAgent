using Microsoft.SemanticKernel;

namespace PRAgent.Services;

public interface IKernelService
{
    Kernel CreateKernel(string? systemPrompt = null);
    Kernel CreateAgentKernel(string? systemPrompt = null);
    Kernel RegisterFunctionPlugins(Kernel kernel, IEnumerable<object> plugins);
    Kernel RegisterFunctionPlugin(Kernel kernel, object plugin, string? pluginName = null);
    IAsyncEnumerable<string> InvokePromptAsync(Kernel kernel, string prompt, CancellationToken cancellationToken = default);
    Task<string> InvokePromptAsStringAsync(Kernel kernel, string prompt, CancellationToken cancellationToken = default);
}

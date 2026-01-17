using Microsoft.SemanticKernel;

namespace PRAgent.Services;

public interface IKernelService
{
    Kernel CreateKernel(string? systemPrompt = null);
    IAsyncEnumerable<string> InvokePromptAsync(Kernel kernel, string prompt, CancellationToken cancellationToken = default);
    Task<string> InvokePromptAsStringAsync(Kernel kernel, string prompt, CancellationToken cancellationToken = default);
}

using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;

namespace PRAgent.Services;

public interface IKernelService
{
    Kernel CreateKernel(string? systemPrompt = null);
    IAsyncEnumerable<string> InvokePromptAsync(Kernel kernel, string prompt, CancellationToken cancellationToken = default);
    Task<string> InvokePromptAsStringAsync(Kernel kernel, string prompt, CancellationToken cancellationToken = default);

    // Loggerを設定するメソッド
    void SetLogger(ILogger logger);
}

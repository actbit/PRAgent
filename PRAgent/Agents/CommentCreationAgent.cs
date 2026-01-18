using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PRAgent.Models;
using PRAgent.Services;

namespace PRAgent.Agents;

/// <summary>
/// レビューコメント作成を行うSubagent
/// </summary>
public class CommentCreationAgent
{
    private readonly ILogger<CommentCreationAgent> _logger;
    private readonly ReviewAnalysisTools _tools;
    private readonly Kernel _kernel;

    public CommentCreationAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        AISettings aiSettings,
        ILogger<CommentCreationAgent> logger)
    {
        _logger = logger;
        var toolsLogger = logger as ILogger<ReviewAnalysisTools>;
        if (toolsLogger == null)
        {
            _logger.LogWarning("Logger type mismatch, creating new logger instance");
            toolsLogger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<ReviewAnalysisTools>();
        }
        _tools = new ReviewAnalysisTools(gitHubService, prDataService, toolsLogger);
        _kernel = kernelService.CreateKernel();

        // ツールを登録
        _kernel.ImportPluginFromFunctions("CommentCreation",
        [
            KernelFunctionFactory.CreateFromMethod(_tools.GenerateReviewCommentsAsync, "GenerateReviewComments"),
            KernelFunctionFactory.CreateFromMethod(_tools.ReadFileContentAsync, "ReadFileContent")
        ]);
    }

    public Kernel GetKernel() => _kernel;

    public ReviewAnalysisTools GetTools() => _tools;
}
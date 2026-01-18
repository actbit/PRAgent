using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PRAgent.Models;
using PRAgent.Services;

namespace PRAgent.Agents;

/// <summary>
/// レビュー分析を行うSubagent
/// </summary>
public class ReviewAnalysisAgent
{
    private readonly ILogger<ReviewAnalysisAgent> _logger;
    private readonly ReviewAnalysisTools _tools;
    private readonly Kernel _kernel;

    public ReviewAnalysisAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        AISettings aiSettings,
        ILogger<ReviewAnalysisAgent> logger)
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
        _kernel.ImportPluginFromFunctions("ReviewAnalysis",
        [
            KernelFunctionFactory.CreateFromMethod(_tools.ExtractReviewIssuesAsync, "ExtractReviewIssues"),
            KernelFunctionFactory.CreateFromMethod(_tools.GenerateReviewCommentsAsync, "GenerateReviewComments"),
            KernelFunctionFactory.CreateFromMethod(_tools.ReadFileContentAsync, "ReadFileContent")
        ]);
    }

    public Kernel GetKernel() => _kernel;

    public ReviewAnalysisTools GetTools() => _tools;
}
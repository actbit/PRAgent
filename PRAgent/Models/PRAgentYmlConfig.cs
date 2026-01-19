namespace PRAgent.Models;

public class PRAgentYmlConfig
{
    public PRAgentConfig? PRAgent { get; set; }
}

public class PRAgentConfig
{
    public bool Enabled { get; set; } = true;
    public string? SystemPrompt { get; set; }
    public ReviewConfig? Review { get; set; }
    public SummaryConfig? Summary { get; set; }
    public ApproveConfig? Approve { get; set; }
    public List<string>? IgnorePaths { get; set; }
    public AgentFrameworkConfig? AgentFramework { get; set; }
}

public class ReviewConfig
{
    public bool Enabled { get; set; } = true;
    public bool AutoPost { get; set; } = false;
    public string? CustomPrompt { get; set; }
}

public class SummaryConfig
{
    public bool Enabled { get; set; } = true;
    public bool PostAsComment { get; set; } = true;
    public string? CustomPrompt { get; set; }
}

public class ApproveConfig
{
    public bool Enabled { get; set; } = true;
    public string AutoApproveThreshold { get; set; } = "minor";
    public bool RequireReviewFirst { get; set; } = true;
}

public enum ApprovalThreshold
{
    Critical,
    Major,
    Minor,
    None
}

/// <summary>
/// Semantic Kernel Agent Frameworkの設定
/// </summary>
public class AgentFrameworkConfig
{
    /// <summary>
    /// Agent Frameworkを有効にするかどうか
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 使用するオーケストレーションモード
    /// </summary>
    public string OrchestrationMode { get; set; } = "sequential"; // sequential, agent_chat, collaborative, parallel

    /// <summary>
    /// 選択戦略
    /// </summary>
    public string SelectionStrategy { get; set; } = "approval_workflow"; // sequential, approval_workflow, conditional, round_robin, content_based

    /// <summary>
    /// 関数呼び出しを有効にするかどうか
    /// </summary>
    public bool EnableFunctionCalling { get; set; } = true;

    /// <summary>
    /// 自動承認を有効にするかどうか
    /// </summary>
    public bool EnableAutoApproval { get; set; } = false;

    /// <summary>
    /// 最大ターン数（AgentGroupChat用）
    /// </summary>
    public int MaxTurns { get; set; } = 10;

    /// <summary>
    /// 各エージェントの設定
    /// </summary>
    public AgentConfigs? Agents { get; set; }
}

/// <summary>
/// 個別エージェントの設定
/// </summary>
public class AgentConfigs
{
    public ReviewAgentConfig? Review { get; set; }
    public SummaryAgentConfig? Summary { get; set; }
    public ApprovalAgentConfig? Approval { get; set; }
}

/// <summary>
/// Reviewエージェントの設定
/// </summary>
public class ReviewAgentConfig
{
    public string? CustomSystemPrompt { get; set; }
    public bool EnablePlugins { get; set; } = false;
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
}

/// <summary>
/// Summaryエージェントの設定
/// </summary>
public class SummaryAgentConfig
{
    public string? CustomSystemPrompt { get; set; }
    public bool EnablePlugins { get; set; } = false;
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
}

/// <summary>
/// Approvalエージェントの設定
/// </summary>
public class ApprovalAgentConfig
{
    public string? CustomSystemPrompt { get; set; }
    public bool EnableGitHubFunctions { get; set; } = true;
    public bool AutoApproveOnDecision { get; set; } = false;
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
}

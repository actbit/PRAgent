using YamlDotNet.Serialization;

namespace PRAgent.Models;

public class PRAgentYmlConfig
{
    [YamlMember(Alias = "pragent")]
    public PRAgentConfig? PRAgent { get; set; }
}

public class PRAgentConfig
{
    public bool Enabled { get; set; } = true;

    [YamlMember(Alias = "system_prompt")]
    public string? SystemPrompt { get; set; }

    [YamlMember(Alias = "review")]
    public ReviewConfig? Review { get; set; }

    [YamlMember(Alias = "summary")]
    public SummaryConfig? Summary { get; set; }

    [YamlMember(Alias = "approve")]
    public ApproveConfig? Approve { get; set; }

    [YamlMember(Alias = "ignore_paths")]
    public List<string>? IgnorePaths { get; set; }
}

public class ReviewConfig
{
    public bool Enabled { get; set; } = true;

    [YamlMember(Alias = "auto_post")]
    public bool AutoPost { get; set; } = false;

    [YamlMember(Alias = "custom_prompt")]
    public string? CustomPrompt { get; set; }
}

public class SummaryConfig
{
    public bool Enabled { get; set; } = true;

    [YamlMember(Alias = "post_as_comment")]
    public bool PostAsComment { get; set; } = true;

    [YamlMember(Alias = "custom_prompt")]
    public string? CustomPrompt { get; set; }
}

public class ApproveConfig
{
    public bool Enabled { get; set; } = true;

    [YamlMember(Alias = "auto_approve_threshold")]
    public string AutoApproveThreshold { get; set; } = "minor";

    [YamlMember(Alias = "require_review_first")]
    public bool RequireReviewFirst { get; set; } = true;
}

public enum ApprovalThreshold
{
    Critical,
    Major,
    Minor,
    None
}

namespace PRAgent.Models;

public class PRSettings
{
    public const string SectionName = "PRSettings";

    public string GitHubToken { get; set; } = string.Empty;
    public string DefaultOwner { get; set; } = string.Empty;
    public string DefaultRepo { get; set; } = string.Empty;
}

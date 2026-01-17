namespace PRAgent.Models;

public class AISettings
{
    public const string SectionName = "AISettings";

    public string Endpoint { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "gpt-4o-mini";
    public int MaxTokens { get; set; } = 4000;
    public double Temperature { get; set; } = 0.7;
}

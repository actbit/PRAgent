namespace PRAgent.Models;

public class AISettings
{
    public const string SectionName = "AISettings";

    public string Endpoint { get; set; } = string.Empty;  // デフォルトは空（環境変数で設定）
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "gpt-4o-mini";
    public int MaxTokens { get; set; } = 4000;
    public double Temperature { get; set; } = 0.7;
    public string Language { get; set; } = "en";  // Default: English
}

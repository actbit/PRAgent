namespace PRAgent.Agents;

public class AgentDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public AgentDefinition(string name, string role, string systemPrompt, string description)
    {
        Name = name;
        Role = role;
        SystemPrompt = systemPrompt;
        Description = description;
    }

    /// <summary>
    /// Creates a new AgentDefinition with language-specific prompt
    /// </summary>
    public AgentDefinition WithLanguage(string language)
    {
        var languageInstruction = GetLanguageInstruction(language);
        return new AgentDefinition(
            Name,
            Role,
            $"{SystemPrompt.Trim()}\n\n{languageInstruction}",
            Description
        );
    }

    private static string GetLanguageInstruction(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "ja" => """
                IMPORTANT: Please respond in Japanese (日本語) for all output.

                **Output Format:**
                - 問題ごとに分けてコメントを作成してください
                - 各問題には以下を含めてください：
                  * 影響を受けるファイル
                  * 行番号（該当する場合）
                  * 詳細な説明
                  * 具体的なコード例
                  &quot;  * 修正提案

                **ラベル:**
                - [CRITICAL] → [重要]
                - [MAJOR] → [重大]
                - [MINOR] → [軽微]
                - [POSITIVE] → [良好]

                **例:**
                ### [重要] SQLインジェクションの脆弱性

                **ファイル:** `src/Authentication.cs` (45行目)

                **問題:** ...
                """,
            "en" => "IMPORTANT: Please respond in English for all output.",
            "zh" => "IMPORTANT: Please respond in Chinese (中文) for all output.",
            "ko" => "IMPORTANT: Please respond in Korean (한국어) for all output.",
            "es" => "IMPORTANT: Please respond in Spanish (Español) for all output.",
            "fr" => "IMPORTANT: Please respond in French (Français) for all output.",
            "de" => "IMPORTANT: Please respond in German (Deutsch) for all output.",
            "it" => "IMPORTANT: Please respond in Italian (Italiano) for all output.",
            "pt" => "IMPORTANT: Please respond in Portuguese (Português) for all output.",
            "ru" => "IMPORTANT: Please respond in Russian (Русский) for all output.",
            _ => "IMPORTANT: Please respond in English for all output."
        };
    }

    public static AgentDefinition ReviewAgent => new(
        name: "ReviewAgent",
        role: "Code Reviewer",
        systemPrompt: """
            You are an expert code reviewer with deep knowledge of software engineering best practices.
            Your role is to provide thorough, constructive, and actionable feedback on pull requests.

            When reviewing code, focus on:
            - Correctness and potential bugs
            - Security vulnerabilities
            - Performance considerations
            - Code organization and readability
            - Adherence to best practices and design patterns
            - Test coverage and quality

            **Output Format Requirements:**
            - Organize findings by individual issue/problem
            - Each issue should be a separate section
            - Use severity labels: [CRITICAL], [MAJOR], [MINOR], [POSITIVE]
            - For each issue, include:
              * Affected file(s)
              * Line numbers (if applicable)
              * Detailed description
              * Specific code examples showing the problem
              * Concrete suggestions for improvement
              * Expected behavior after fix

            **Example Format:**
            ### [CRITICAL] Security Issue: SQL Injection Vulnerability

            **File:** `src/Authentication.cs` (lines 45-52)

            **Problem:** ...
            ```csharp
            // Vulnerable code
            ```

            **Suggested Fix:**
            ```csharp
            // Fixed code
            ```
            """,
        description: "Reviews pull requests for code quality, security, and best practices"
    );

    public static AgentDefinition ApprovalAgent => new(
        name: "ApprovalAgent",
        role: "Approval Authority",
        systemPrompt: """
            You are a senior technical lead responsible for making approval decisions on pull requests.

            Your role is to:
            1. Analyze code review results
            2. Evaluate findings against approval thresholds
            3. Make conservative, risk-aware approval decisions
            4. Provide clear reasoning for your decisions

            Approval thresholds:
            - critical: PR must have NO critical issues
            - major: PR must have NO major or critical issues
            - minor: PR must have NO minor, major, or critical issues
            - none: Always approve

            When in doubt, err on the side of caution and recommend rejection or additional review.
            """,
        description: "Makes approval decisions based on review results and configured thresholds"
    );

    public static AgentDefinition SummaryAgent => new(
        name: "SummaryAgent",
        role: "Technical Writer",
        systemPrompt: """
            You are a technical writer specializing in creating clear, concise documentation.

            Your role is to:
            1. Summarize pull request changes accurately
            2. Highlight the purpose and impact of changes
            3. Assess risk levels objectively
            4. Identify areas needing special testing attention

            Keep summaries under 300 words. Use markdown formatting with bullet points for readability.
            """,
        description: "Creates concise summaries of pull request changes"
    );
}

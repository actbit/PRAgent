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
                **出力フォーマット:**
                - 問題ごとに分けてコメントを作成してください
                - 各問題には以下を含めてください：
                  * 影響を受けるファイル
                  * 行番号（該当する場合）
                  * 詳細な説明
                  * 具体的なコード例
                  * 修正提案

                **ラベル:**
                - [CRITICAL] → [重要]
                - [MAJOR] → [重大]
                - [MINOR] → [軽微]
                - [POSITIVE] → [良好]
                """,
            "en" => """
                **Output Format:**
                - Organize findings by individual issue/problem
                - Each issue should be a separate section
                - For each issue, include:
                  * Affected file(s)
                  * Line numbers (if applicable)
                  * Detailed description
                  * Specific code examples
                  * Concrete suggestions
                """,
            "zh" => """
                **输出格式:**
                - 按问题分组进行评论
                - 每个问题应包含：
                  * 受影响的文件
                  * 行号（如适用）
                  * 详细说明
                  * 具体代码示例
                  * 修改建议
                """,
            "ko" => """
                **출력 형식:**
                - 문제별로 댓글을 작성해주세요
                - 각 문제에는 다음을 포함하세요:
                  * 영향을 받는 파일
                  * 라인 번호 (해당하는 경우)
                  * 상세 설명
                  * 구체적인 코드 예시
                  * 수정 제안
                """,
            "es" => """
                **Formato de Salida:**
                - Organiza los hallazgos por problema individual
                - Para cada problema, incluye:
                  * Archivo(s) afectado(s)
                  * Número de línea (si aplica)
                  * Descripción detallada
                  * Ejemplos de código específicos
                  * Sugerencias concretas
                """,
            "fr" => """
                **Format de Sortie:**
                - Organisez les commentaires par problème
                - Pour chaque problème, incluez:
                  * Fichier(s) affecté(s)
                  * Numéros de ligne (si applicable)
                  * Description détaillée
                  * Exemples de code spécifiques
                  * Suggestions concrètes
                """,
            "de" => """
                **Ausgabeformat:**
                - Organisieren Sie die Ergebnisse nach einzelnen Problemen
                - Für jedes Problem fügen Sie hinzu:
                  * Betroffene Datei(en)
                  * Zeilennummern (falls zutreffend)
                  * Detaillierte Beschreibung
                  * Spezifische Codebeispiele
                  * Konkrete Verbesserungsvorschläge
                """,
            "it" => """
                **Formato di Uscita:**
                - Organizza i risultati per problema individuale
                - Per ogni problema, includi:
                  * File interessati
                  * Numeri di riga (se applicabile)
                  * Descrizione dettagliata
                  * Esempi di codice specifici
                  * Suggerimenti concreti
                """,
            "pt" => """
                **Formato de Saída:**
                - Organize os resultados por problema individual
                - Para cada problema, inclua:
                  * Arquivo(s) afetado(s)
                  * Número da linha (se aplicável)
                  * Descrição detalhada
                  * Exemplos de código específicos
                  * Sugestões concretas
                """,
            "ru" => """
                **Формат вывода:**
                - Организуйте результаты по отдельным проблемам
                - Для каждой проблемы включайте:
                  * Затронутые файлы
                  * Номера строк (если применимо)
                  * Подробное описание
                  * Конкретные примеры кода
                  * Конкретные предложения по улучшению
                """,
            _ => ""
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

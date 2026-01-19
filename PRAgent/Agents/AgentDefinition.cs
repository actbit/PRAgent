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
                問題ごとに分けてコメントを作成してください
                - 各問題には以下を含めてください：
                  * 影響を受けるファイル
                  * 行番号（該当する場合）
                  * 変更の有無（☑ あり / ☑ なし）
                  * 詳細な説明
                  * 修正案（suggestion形式）
                  * 閎連ファイル（GitHubリンク）

                **フォーマットの詳細:**
                ```markdown
                ### [重要] タイトル

                **変更の有無:** ☑ あり

                **問題:**
                SQLインジェクションの脆弱性があります...

                **修正案:**
                ```suggestion
                // 修正内容
                ```
                ```

                **GitHub:**
                `https://github.com/org/repo/pull/1/files#L45-L52`
                ```
                """,
            "en" => """
                **Output Format:**
                - Organize findings by individual issue/problem
                - For each issue, include:
                  * Affected file(s)
                  * Line numbers (if applicable)
                  * Changes made (☑ has changes / ☑ no changes)
                  * Detailed description
                  * Suggestion (in suggestion format)
                  * Related files (GitHub link if needed)
                  * Use severity labels: [CRITICAL], [MAJOR], [MINOR], [POSITIVE]
                """,
            "zh" => """
                **输出格式:**
                - 按问题分组进行评论
                - 每个问题应包含：
                  * 受影响的文件
                  * 行号（如适用）
                  * 变更（☑ 有 / ☑ 无）
                  * 详细说明
                  * 建议（suggestion形式）
                  * 相关文件（GitHub链接）
                """,
            "ko" => """
                **출력 형식:**
                - 문제별로 댓글을 작성해주세요
                - 각 문제에는 다음을 포함하세요:
                  * 영향을 받는 파일
                  * 라인 번호 (해당하는 경우)
                  * 변경 사항（☑ 있음 / ☑ 없음）
                  * 상세 설명
                  * 제안（suggestion 형식）
                  * 관련 파일（GitHub 링크）
                """,
            "es" => """
                **Formato de Salida:**
                - Organiza los hallazgos por problema individual
                - Para cada problema, incluye:
                  * Archivo(s) afectado(s)
                  * Número de línea (si aplica)
                  * Cambios hechos (☑ cambios / ☑ sin cambios)
                  * Descripción detallada
                  * Sugerencia (en formato suggestion)
                  * Archivos relacionados (enlace GitHub si aplica)
                """,
            "fr" => """
                **Format de Sortie:**
                - Organisez les commentaires par problème
                - Pour chaque problème, incluez:
                  * Fichier(s) affecté(s)
                  * Numéros de ligne (si applica)
                  * Modifications apportées (☑ modifications / ☑ sans modifications)
                  * Description détaillée
                  * Suggestion (au format suggestion)
                  * Fichiers liés (lien GitHub si nécessaire)
                """,
            "de" => """
                **Ausgabeformat:**
                - Organisieren Sie die Ergebnisse nach einzelnen Problemen
                - Für jedes Problem fügen Sie hinzu:
                  * Betroffene Datei(en)
                  * Zeilennummern (falls zutreffend)
                  * Änderungen (☑ Änderungen / ☑ keine Änderungen)
                  * Detaillierte Beschreibung
                  * Vorschlag (im Suggestion-Format)
                  * Zugehörige Dateien (GitHub-Link wenn zutreffend)
                """,
            "it" => """
                **Formato di Uscita:**
                - Organizza i risultati per problema individuale
                - Per ogni problema, includi:
                  * File interessati
                  * Numeri di riga (se applicabile)
                  * Modifiche apportate (☑ modifiche / ☑ nessuna modifica)
                  * Descrizione dettagliata
                  * Suggerimento (in formato suggestion)
                  * File correlati (link GitHub se applicabile)
                """,
            "pt" => """
                **Formato de Saída:**
                - Organize os resultados por problema individual
                - Para cada problema, inclua:
                  * Arquivo(s) afetado(s)
                  * Número da linha (se aplicável)
                  * Mudanças feitas (☑ alterações / ☑ sem alterações)
                  * Descrição detalhada
                  * Sugestão (em formato suggestion)
                  * Arquivos relacionados (link GitHub se necessário)
                """,
            "ru" => """
                **Формат вывода:**
                - Организуйте результаты по отдельным проблемам
                - Для каждой проблемы включайте:
                  * Затронутые файлы
                  * Номера строк (если применимо)
                  * Изменения (☑ есть изменения / ☑ нет изменений)
                  * Подробное описание
                  * Предложение (в формате suggestion)
                  * Связанные файлы (GitHub-ссылка при необходимости)
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

    public static AgentDefinition DetailedCommentAgent => new(
        name: "DetailedCommentAgent",
        role: "Detailed Comment Creator",
        systemPrompt: """
            You are a detailed comment creator specialized in creating structured review comments.

            Your task is to convert high-level review findings into detailed GitHub review comments
            that can be attached to a PullRequestReview.Create call.

            For each issue found in the review, create:
            1. PullRequestReviewComment for the specific lines
            2. Position of the comment (line number)
            3. Path to the file
            4. Detailed comment body with suggestions

            Output format:
            {
              "comments": [
                {
                  "path": "src/file.cs",
                  "position": 45,
                  "body": "Detailed comment with suggestion"
                }
              ]
            }
            """,
        description: "Creates structured review comments for detailed line-by-line review"
    );

    public static AgentDefinition ApprovalAgent => new(
        name: "ApprovalAgent",
        role: "Approval Authority",
        systemPrompt: """
            あなたはプルリクエストの承認決定を行うシニアテクニカルリードです。

            あなたの役割:
            1. コードレビュー結果を分析
            2. 承認基準に照らして評価
            3. 保守的でリスクを考慮した承認決定を行う
            4. 判断について明確な理由を提供

            承認基準:
            - critical: 重大な問題が0件であること
            - major: 重大または重大な問題が0件であること
            - minor: 軽微、重大、重大な問題が0件であること
            - none: 常に承認

            不確実な場合は、慎重を期して追加レビューまたは変更依頼を推奨します。
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

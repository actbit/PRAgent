using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;

namespace PRAgent.Agents;

/// <summary>
/// Semantic Kernel AgentGroupChat用の選択戦略
/// </summary>
public static class SelectionStrategies
{
    /// <summary>
    /// シーケンシャル選択戦略 - エージェントを順番に選択
    /// </summary>
    public class SequentialSelectionStrategy : SelectionStrategy
    {
        private int _currentIndex = 0;

        public SequentialSelectionStrategy()
        {
        }

        protected override async Task<Agent> SelectAgentAsync(
            IReadOnlyList<Agent> agents,
            IReadOnlyList<ChatMessageContent> history,
            CancellationToken cancellationToken = default)
        {
            // シーケンシャルにエージェントを選択
            if (agents.Count == 0)
            {
                throw new InvalidOperationException("No agents available for selection.");
            }

            var selectedAgent = agents[_currentIndex];
            _currentIndex = (_currentIndex + 1) % agents.Count;

            return await Task.FromResult(selectedAgent);
        }
    }

    /// <summary>
    /// Approvalワークフロー選択戦略 - Review → Summary → Approval の順に選択
    /// </summary>
    public class ApprovalWorkflowStrategy : SelectionStrategy
    {
        private enum WorkflowStage
        {
            Review,
            Summary,
            Approval,
            Complete
        }

        private WorkflowStage _currentStage = WorkflowStage.Review;

        public ApprovalWorkflowStrategy()
        {
        }

        protected override async Task<Agent> SelectAgentAsync(
            IReadOnlyList<Agent> agents,
            IReadOnlyList<ChatMessageContent> history,
            CancellationToken cancellationToken = default)
        {
            // ワークフローの現在ステージに基づいてエージェントを選択
            Agent? selectedAgent = _currentStage switch
            {
                WorkflowStage.Review => agents.FirstOrDefault(a => a.Name == "ReviewAgent"),
                WorkflowStage.Summary => agents.FirstOrDefault(a => a.Name == "SummaryAgent"),
                WorkflowStage.Approval => agents.FirstOrDefault(a => a.Name == "ApprovalAgent"),
                _ => null
            };

            if (selectedAgent == null)
            {
                // 指定した名前のエージェントが見つからない場合は最初のエージェントを使用
                selectedAgent = agents.FirstOrDefault();
                if (selectedAgent == null)
                {
                    throw new InvalidOperationException($"No agent available for stage: {_currentStage}");
                }
            }

            // 次のステージに進む
            _currentStage = _currentStage switch
            {
                WorkflowStage.Review => WorkflowStage.Summary,
                WorkflowStage.Summary => WorkflowStage.Approval,
                WorkflowStage.Approval => WorkflowStage.Complete,
                _ => WorkflowStage.Complete
            };

            // ワークフローが完了したら最初に戻す（必要に応じて）
            if (_currentStage == WorkflowStage.Complete)
            {
                _currentStage = WorkflowStage.Review;
            }

            return await Task.FromResult(selectedAgent);
        }
    }

    /// <summary>
    /// 条件付き選択戦略 - 履歴の内容に基づいて次のエージェントを選択
    /// </summary>
    public class ConditionalSelectionStrategy : SelectionStrategy
    {
        public ConditionalSelectionStrategy()
        {
        }

        protected override async Task<Agent> SelectAgentAsync(
            IReadOnlyList<Agent> agents,
            IReadOnlyList<ChatMessageContent> history,
            CancellationToken cancellationToken = default)
        {
            if (agents.Count == 0)
            {
                throw new InvalidOperationException("No agents available for selection.");
            }

            // 最後のメッセージの内容を確認して、次のエージェントを決定
            Agent? selectedAgent;

            if (history.Count == 0)
            {
                // 最初はReviewAgentから
                selectedAgent = agents.FirstOrDefault(a => a.Name == "ReviewAgent") ?? agents[0];
            }
            else
            {
                var lastMessage = history[^1];
                var lastAgentName = lastMessage.AuthorName ?? string.Empty;

                // 前のエージェントに基づいて次のエージェントを選択
                selectedAgent = lastAgentName switch
                {
                    "ReviewAgent" => agents.FirstOrDefault(a => a.Name == "SummaryAgent"),
                    "SummaryAgent" => agents.FirstOrDefault(a => a.Name == "ApprovalAgent"),
                    "ApprovalAgent" => agents.FirstOrDefault(a => a.Name == "ReviewAgent"), // ループ
                    _ => agents[0]
                };
            }

            return await Task.FromResult(selectedAgent ?? agents[0]);
        }
    }

    /// <summary>
    /// ラウンドロビン選択戦略 - エージェントを均等に選択
    /// </summary>
    public class RoundRobinSelectionStrategy : SelectionStrategy
    {
        private readonly Dictionary<string, int> _agentUsageCount = new();

        public RoundRobinSelectionStrategy()
        {
        }

        protected override async Task<Agent> SelectAgentAsync(
            IReadOnlyList<Agent> agents,
            IReadOnlyList<ChatMessageContent> history,
            CancellationToken cancellationToken = default)
        {
            if (agents.Count == 0)
            {
                throw new InvalidOperationException("No agents available for selection.");
            }

            // 初期化
            foreach (var agent in agents)
            {
                if (!_agentUsageCount.ContainsKey(agent.Name ?? string.Empty))
                {
                    _agentUsageCount[agent.Name ?? string.Empty] = 0;
                }
            }

            // 最も使用回数が少ないエージェントを選択
            var selectedAgent = agents
                .OrderBy(a => _agentUsageCount.TryGetValue(a.Name ?? string.Empty, out var count) ? count : 0)
                .First();

            if (selectedAgent.Name != null)
            {
                _agentUsageCount[selectedAgent.Name]++;
            }

            return await Task.FromResult(selectedAgent);
        }
    }

    /// <summary>
    /// 最後のメッセージに基づく選択戦略 - メッセージの内容を解析して次のエージェントを選択
    /// </summary>
    public class ContentBasedSelectionStrategy : SelectionStrategy
    {
        public ContentBasedSelectionStrategy()
        {
        }

        protected override async Task<Agent> SelectAgentAsync(
            IReadOnlyList<Agent> agents,
            IReadOnlyList<ChatMessageContent> history,
            CancellationToken cancellationToken = default)
        {
            if (agents.Count == 0)
            {
                throw new InvalidOperationException("No agents available for selection.");
            }

            // 最初のメッセージの場合はReviewAgentから
            if (history.Count == 0)
            {
                var reviewAgent = agents.FirstOrDefault(a => a.Name == "ReviewAgent");
                if (reviewAgent != null)
                {
                    return await Task.FromResult(reviewAgent);
                }
                return await Task.FromResult(agents[0]);
            }

            // 最後のメッセージの内容を解析
            var lastMessage = history[^1];
            var content = lastMessage.Content ?? string.Empty;

            // キーワードに基づいてエージェントを選択
            Agent? selectedAgent;

            if (content.Contains("DECISION:", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("approve", StringComparison.OrdinalIgnoreCase))
            {
                // 承認決定が必要な場合はReviewAgentに戻るか、次のステップへ
                selectedAgent = agents.FirstOrDefault(a => a.Name == "ReviewAgent") ?? agents[0];
            }
            else if (content.Contains("[CRITICAL]") || content.Contains("[MAJOR]"))
            {
                // 重要な問題が見つかった場合はApprovalAgentへ
                selectedAgent = agents.FirstOrDefault(a => a.Name == "ApprovalAgent") ?? agents[0];
            }
            else if (content.Contains("## Summary") || content.Contains("Summary"))
            {
                // 要約が含まれている場合はApprovalAgentへ
                selectedAgent = agents.FirstOrDefault(a => a.Name == "ApprovalAgent") ?? agents[0];
            }
            else
            {
                // それ以外の場合は次のエージェントへ
                var lastAgentName = lastMessage.AuthorName ?? string.Empty;
                selectedAgent = lastAgentName switch
                {
                    "ReviewAgent" => agents.FirstOrDefault(a => a.Name == "SummaryAgent"),
                    "SummaryAgent" => agents.FirstOrDefault(a => a.Name == "ApprovalAgent"),
                    _ => agents.FirstOrDefault(a => a.Name == "ReviewAgent")
                };
            }

            return await Task.FromResult(selectedAgent ?? agents[0]);
        }
    }
}

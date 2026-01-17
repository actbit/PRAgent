# PRAgent

AI-powered Pull Request Agent for GitHub - Semantic Kernelを使用したC#製PRレビュー・要約・承認ツール

**サーバーレス・GitHub Actionsで動作**

## 機能

- **PRレビュー**: AIがコード変更を分析し、構造化されたレビューを提供
- **PR要約**: 変更内容を簡潔に要約
- **PR承認**: レビュー結果に基づき、AIが承認可否を判断して自動承認
- **マルチエージェントアーキテクチャ**: ReviewAgentとApprovalAgentが連携して判断
- **OpenAI互換エンドポイント対応**: 柔軟なAIバックエンド選択
- **リポジトリ別設定**: `.github/pragent.yml` でカスタムプロンプト・設定可能

---

## クイックスタート（GitHub Actions）

### 1. ワークフローファイルを追加

このリポジトリの `.github/workflows/` を自分のリポジトリにコピーします：

```
.github/workflows/
├── pragent-native.yml   # 自動実行（推奨）
└── pragent-docker.yml   # 手動実行用
```

### 2. GitHub Secretsを設定

```
Settings → Secrets and variables → Actions → New repository secret
```

| Secret | 値 |
|--------|-----|
| `AI_API_KEY` | OpenAI APIキー（`sk-...`） |

> **注**: `GITHUB_TOKEN` は自動的に提供されます

### 3. （オプション）GitHub Variablesを設定

```
Settings → Secrets and variables → Actions → Variables → New repository variable
```

| Variable | 値（デフォルト） |
|----------|------------------|
| `AI_ENDPOINT` | `https://api.openai.com/v1` |
| `AI_MODEL_ID` | `gpt-4o-mini` |

### 4. PRを作成

PRを作成するだけで、自動でレビューが実行されます！

```
PR作成 → PRAgent実行 → レビューコメント自動投稿
```

---

## GitHub Actionsの使い方

### 自動実行（Native版）

PRを作成・更新すると自動で実行されます（30秒〜1分）

### 手動実行

```
Actionsタブ
    ├── PRAgent (Native)  → Run workflow  （高速）
    └── PRAgent (Docker)  → Run workflow  （環境依存なし）
```

手動実行では、以下を選択できます：
- PR番号
- コマンド（review/summary/approve）

### ワークフロー比較

| 項目 | Native版 | Docker版 |
|------|----------|----------|
| 自動実行 | ✓ | - |
| 手動実行 | ✓ | ✓ |
| 実行速度 | **30秒〜1分** | 2〜4分（初回） |
| 用途 | 通常使用 | トラブルシューティング |

---

## CLIでの使い方（ローカル開発）

### インストール

```bash
git clone <repository-url>
cd PRAgent
dotnet build -c Release
```

### 設定

**方法1: 環境変数（推奨）**

```bash
export AI_ENDPOINT=https://api.openai.com/v1
export AI_API_KEY=your-api-key
export AI_MODEL_ID=gpt-4o-mini
export GITHUB_TOKEN=your-github-token
```

**方法2: appsettings.json**

```json
{
  "AISettings": {
    "Endpoint": "https://api.openai.com/v1",
    "ApiKey": "your-api-key",
    "ModelId": "gpt-4o-mini"
  },
  "PRSettings": {
    "GitHubToken": "your-github-token"
  }
}
```

### コマンド

```bash
# ヘルプ
dotnet run -- help

# PRレビュー
dotnet run -- review --owner "org" --repo "repo" --pr 123 --post-comment

# PR要約
dotnet run -- summary -o "org" -r "repo" -p 123 --post-comment

# 手動承認
dotnet run -- approve -o "org" -r "repo" -p 123 --comment "LGTM"

# AI判断で自動承認
dotnet run -- approve -o "org" -r "repo" -p 123 --auto --threshold minor
```

---

## 承認しきい値 (threshold)

| 値 | 説明 |
|---|---|
| `critical` | Criticalな問題がない場合のみ承認 |
| `major` | Major以上の問題がない場合のみ承認 |
| `minor` | Minor以上の問題がない場合のみ承認（デフォルト） |
| `none` | 常に承認 |

---

## リポジトリ内設定ファイル

対象リポジトリに `.github/pragent.yml` を配置することで、カスタム設定が可能です：

```yaml
pragent:
  enabled: true

  # システムプロンプトの上書き
  system_prompt: |
    あなたは経験豊富なコードレビュアーです。
    以下の点に特に注意してください：
    - セキュリティ脆弱性
    - パフォーマンス問題
    - 社内コーディング規約への準拠

  # レビュー設定
  review:
    enabled: true
    auto_post: false

  # 要約設定
  summary:
    enabled: true
    post_as_comment: true

  # 承認設定
  approve:
    enabled: true
    auto_approve_threshold: "minor"
    require_review_first: true

  # 除外パス
  ignore_paths:
    - "*.min.js"
    - "dist/**"
    - "node_modules/**"
```

---

## コスト

### GitHub Actions（推奨）

| プラン | 無料枠 | 超過料金 |
|--------|--------|----------|
| パブリックリポジトリ | **無料（無制限）** | - |
| プライベートリポジトリ | 2000分/月 | $0.008/分 |

**実用例**: PRレビュー1回あたり1分 × 月100回 = 100分/月 → **無料**

サーバーは一切不要です。

---

## マルチエージェントアーキテクチャ

```
┌─────────────┐
│ ReviewAgent │ ← コードレビューを実行
└──────┬──────┘
       │ レビュー結果
       ▼
┌──────────────┐
│ApprovalAgent │ ← レビュー結果に基づき承認判断
└──────────────┘
       │
       ▼
   GitHub PR 承認
```

- **ReviewAgent**: コードの品質、セキュリティ、パフォーマンスを分析
- **ApprovalAgent**: 設定されたしきい値に基づき承認可否を判断
- **SummaryAgent**: PR変更を簡潔に要約

---

## 対応AIプロバイダ

OpenAI互換エンドポイントであれば動作します：

- OpenAI (GPT-4, GPT-4o, GPT-4o-mini)
- Azure OpenAI
- 各種ローカルLLM (Ollama, LM Studio等)

---

## プロジェクト構造

```
PRAgent/
├── .github/
│   └── workflows/              # GitHub Actionsワークフロー
├── Plugins/
│   ├── GitHub/                 # GitHub操作プラグイン
│   └── PRAnalysis/             # AI分析プラグイン
│       └── Prompts/            # プロンプトテンプレート
├── Services/                   # サービス層
├── Agents/                     # エージェント実装
├── Models/                     # 設定モデル
├── Configuration/              # 設定処理
└── Program.cs                  # エントリーポイント
```

---

## ライセンス

MIT License

---

## 関連プロジェクト

- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- [Octokit.NET](https://github.com/octokit/octokit.net)

---

**注意**: このツールはAIによる自動レビューを補助するものです。最終的な承認判断は人間が行うことを推奨します。

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

### 方法1: Reusable Workflow（推奨）

ワークフローファイルをコピーせず、PRAgentリポジトリを参照して使用します。

**自分のリポジトリ**に `.github/workflows/pr-review.yml` を作成：

```yaml
name: PR Review

on:
  pull_request:
    types: [opened, synchronize]

permissions:
  contents: read
  pull-requests: write

jobs:
  pr-agent:
    uses: actbit/PRAgent/.github/workflows/pragent-native.yml@main
    with:
      pr_number: ${{ github.event.pull_request.number }}
      command: review
    secrets:
      ai_api_key: ${{ secrets.AI_API_KEY }}
```

**Secretsを設定**：

```
Settings → Secrets and variables → Actions → New repository secret
```

| Secret | 値 |
|--------|-----|
| `AI_API_KEY` | OpenAI APIキー（`sk-...`） |

**完了**: PRを作成するだけで自動レビューが実行されます。

---

### 他のジョブと組み合わせる例

```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: npm test

  pr-agent:
    uses: actbit/PRAgent/.github/workflows/pragent-native.yml@main
    needs: test  # テスト成功後のみ実行
    with:
      pr_number: ${{ github.event.pull_request.number }}
    secrets:
      ai_api_key: ${{ secrets.AI_API_KEY }}
```

---

### 方法2: ワークフローファイルをコピー（カスタマイズ重視）

完全なカスタマイズが必要な場合、ワークフローファイルをコピーして使用します。

このリポジトリのファイルを自分のリポジトリにコピーします：

- `.github/workflows/pragent-native.yml` - Native版（高速）
- `.github/workflows/pragent-docker.yml` - Docker版（環境依存なし）

### Docker版を参照して使う場合

```yaml
jobs:
  pr-agent:
    uses: actbit/PRAgent/.github/workflows/pragent-docker.yml@main
    with:
      pr_number: ${{ github.event.pull_request.number }}
      command: review
    secrets:
      ai_api_key: ${{ secrets.AI_API_KEY }}
```

### 共通設定（どちらの場合も）

**1. GitHub Secrets**：

```
Settings → Secrets and variables → Actions → New repository secret
```

| Secret | 値 | 取得方法 |
|--------|-----|----------|
| `AI_API_KEY` | OpenAI APIキー（`sk-...`） | [OpenAI API Keys](https://platform.openai.com/api-keys) |

> **注**: `GITHUB_TOKEN` はGitHub Actionsが自動的に提供します（設定不要）

---

**2. GitHub Personal Access Token（手動実行の場合のみ）**

ローカル環境や手動実行で使用する場合、GitHub Personal Access Token が必要です：

1. **GitHubでTokenを作成**:
   ```
   Settings → Developer settings → Personal access tokens → Tokens (classic)
   → Generate new token (classic)
   ```

2. **権限を設定**:
   - `repo`（フルコントロール）
   - `pull_requests:write`（PRコメント・承認用）

3. **Tokenをコピー**（一度しか表示されません）

**GitHub Variables（オプション）**：

```
Settings → Secrets and variables → Actions → Variables → New repository variable
```

| Variable | 値（デフォルト） |
|----------|------------------|
| `AI_ENDPOINT` | `https://api.openai.com/v1` |
| `AI_MODEL_ID` | `gpt-4o-mini` |

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

| 項目 | Native（参照） | Native（コピー） | Docker（参照） | Docker（コピー） |
|------|----------------|------------------|----------------|------------------|
| 導入方法 | `uses:` で参照 | ファイルをコピー | `uses:` で参照 | ファイルをコピー |
| 自動実行 | ✓ | ✓ | - | - |
| 手動実行 | ✓ | ✓ | ✓ | ✓ |
| 実行速度 | **30秒〜1分** | **30秒〜1分** | 2〜4分 | 2〜4分 |
| 更新の反映 | 自動 | 手動 | 自動 | 手動 |
| カスタマイズ | 限定的 | 完全 | 限定的 | 完全 |
| 用途 | 通常使用（推奨） | カスタムが必要 | トラブルシューティング | トラブルシューティング |

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
# OpenAI API設定
export AISettings__Endpoint=https://api.openai.com/v1
export AISettings__ApiKey=your-api-key
export AISettings__ModelId=gpt-4o-mini

# GitHub Token（Personal Access Token）
export PRSettings__GitHubToken=your-github-token
```

> **GitHub Tokenの取得方法**:
> ```
> GitHub Settings → Developer settings → Personal access tokens →
> Tokens (classic) → Generate new token (classic)
> ```
> 必要な権限: `repo`（フルコントロール）

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
│       ├── ci.yml              # CI（ビルド＆テスト）
│       ├── pr-review.yml       # このリポジトリのPR用AIレビュー
│       ├── pragent-native.yml  # 外部リポジトリ参照用（Native版）
│       └── pragent-docker.yml  # 外部リポジトリ参照用（Docker版）
├── PRAgent/
│   ├── CommandLine/            # CLIコマンド解析・ハンドラー
│   ├── Configuration/          # DI設定
│   ├── Plugins/                # プラグイン
│   │   ├── GitHub/             # GitHub操作プラグイン
│   │   └── PRAnalysis/         # AI分析プラグイン
│   ├── Services/               # サービス層
│   ├── Agents/                 # エージェント実装
│   ├── Models/                 # 設定モデル
│   └── Program.cs              # エントリーポイント
└── PRAgent.Tests/              # テストプロジェクト
```

---

## リファクタリングによる改善

- **Program.cs**: 510行 → 120行（76%削減）
- **CLIコマンド構造**: 専用ハンドラークラスに分離
- **設定管理**: ServiceCollectionExtensionsに集約
- **保守性**: 各機能が独立したクラスに配置

---

## ライセンス

MIT License

---

## 関連プロジェクト

- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- [Octokit.NET](https://github.com/octokit/octokit.net)

---

**注意**: このツールはAIによる自動レビューを補助するものです。最終的な承認判断は人間が行うことを推奨します。

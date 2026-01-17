# PRAgent

AI-powered Pull Request Agent for GitHub - Semantic Kernelを使用したC#製PRレビュー・要約・承認ツール

## 機能

- **PRレビュー**: AIがコード変更を分析し、構造化されたレビューを提供
- **PR要約**: 変更内容を簡潔に要約
- **PR承認**: レビュー結果に基づき、AIが承認可否を判断して自動承認
- **マルチエージェントアーキテクチャ**: ReviewAgentとApprovalAgentが連携して判断
- **OpenAI互換エンドポイント対応**: 柔軟なAIバックエンド選択
- **リポジトリ別設定**: `.github/pragent.yml` でカスタムプロンプト・設定可能

## インストール

### 方法1: ソースからビルド

```bash
git clone <repository-url>
cd PRAgent
dotnet build -c Release
```

### 方法2: Docker

```bash
docker build -t pragent:latest .
```

### 方法3: NuGet（将来公開予定）

```bash
dotnet tool install --global PRAgent
```

## 設定

### 環境変数で設定（推奨）

```bash
export AI_ENDPOINT=https://api.openai.com/v1
export AI_API_KEY=your-api-key
export AI_MODEL_ID=gpt-4o-mini
export GITHUB_TOKEN=your-github-token
```

### appsettings.jsonで設定

`appsettings.json`を作成：

```json
{
  "AISettings": {
    "Endpoint": "https://api.openai.com/v1",
    "ApiKey": "your-api-key",
    "ModelId": "gpt-4o-mini",
    "MaxTokens": 4000,
    "Temperature": 0.7
  },
  "PRSettings": {
    "GitHubToken": "your-github-token",
    "DefaultOwner": "",
    "DefaultRepo": ""
  }
}
```

## 使い方

### 基本的なコマンド

```bash
# ヘルプ表示
PRAgent help

# PRレビュー
PRAgent review --owner "org" --repo "repo" --pr 123

# レビューをPRコメントとして投稿
PRAgent review -o "org" -r "repo" -p 123 --post-comment

# PR要約
PRAgent summary --owner "org" --repo "repo" --pr 123

# 要約をPRコメントとして投稿
PRAgent summary -o "org" -r "repo" -p 123 --post-comment

# 手動承認（コメント付き）
PRAgent approve --owner "org" --repo "repo" --pr 123 --comment "LGTM"

# AI判断で自動承認
PRAgent approve --owner "org" --repo "repo" --pr 123 --auto

# しきい値を指定して自動承認
PRAgent approve -o "org" -r "repo" -p 123 --auto --threshold major
```

### 承認しきい値 (threshold)

| 値 | 説明 |
|---|---|
| `critical` | Criticalな問題がない場合のみ承認 |
| `major` | Major以上の問題がない場合のみ承認 |
| `minor` | Minor以上の問題がない場合のみ承認（デフォルト） |
| `none` | 常に承認 |

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
    custom_prompt: "plugins/review-prompt.txt"

  # 要約設定
  summary:
    enabled: true
    post_as_comment: true
    custom_prompt: "plugins/summary-prompt.txt"

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

### 設定優先順位

1. コマンドライン引数
2. 環境変数
3. `.github/pragent.yml`（リポジトリ内設定）
4. `~/.pragent/config.json`（ユーザー設定）
5. `appsettings.json`（デフォルト設定）

## Docker使用方法

```bash
# 環境変数で設定
docker run --rm \
  -e AI_ENDPOINT=https://api.openai.com/v1 \
  -e AI_API_KEY=your-key \
  -e GITHUB_TOKEN=your-token \
  pragent:latest \
  review --owner "org" --repo "repo" --pr 123

# 複数行コマンド
docker run --rm \
  -e AI_ENDPOINT=$AI_ENDPOINT \
  -e AI_API_KEY=$AI_API_KEY \
  -e GITHUB_TOKEN=$GITHUB_TOKEN \
  pragent:latest \
  approve -o "org" -r "repo" -p 123 --auto
```

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

## 対応AIプロバイダ

OpenAI互換エンドポートであれば動作します：

- OpenAI (GPT-4, GPT-4o, GPT-4o-mini)
- Azure OpenAI
- Anthropic Claude（OpenAI互換レイヤー経由）
- ローカルLLM (Ollama, LM Studio等)

## プロジェクト構造

```
PRAgent/
├── Plugins/
│   ├── GitHub/              # GitHub操作プラグイン
│   └── PRAnalysis/          # AI分析プラグイン
│       └── Prompts/         # プロンプトテンプレート
├── Services/                # サービス層
├── Agents/                  # エージェント実装
├── Models/                  # 設定モデル
├── Configuration/           # 設定処理
├── Validators/              # バリデーション
└── Program.cs              # エントリーポイント
```

## 開発

```bash
# デバッグビルド
dotnet build -c Debug

# テスト実行（将来実装予定）
dotnet test

# コードフォーマット
dotnet format
```

## ライセンス

MIT License

## 貢献

Pull Requestをお待ちしております。

## 作者

PRAgent Contributors

## 関連プロジェクト

- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- [Octokit.NET](https://github.com/octokit/octokit.net)

---

**注意**: このツールはAIによる自動レビューを補助するものです。最終的な承認判断は人間が行うことを推奨します。

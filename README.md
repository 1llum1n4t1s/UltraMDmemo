# UltraMDmemo

自由入力メモをボタン一つで構造化 Markdown に変換する Windows デスクトップアプリ。

Claude Code CLI をアプリ内で自動セットアップし、ローカル完結で動作します。事前の CLI インストールや API キーの設定は不要です。

---

## 特徴

- **ワンクリック変換** — テキストを貼り付けて「整形する」を押すだけ
- **構造化 Markdown** — タイトル・サマリー・要点・詳細・不明点を自動生成
- **ゼロセットアップ** — Node.js / Claude Code CLI の導入・認証をアプリが自動処理
- **プレビュー表示** — Markdown のレンダリングプレビューと Raw テキストの切替
- **履歴管理** — 変換結果をローカルに自動保存、再利用・再整形が可能
- **自動更新** — Velopack による GitHub Releases からのサイレントアップデート
- **Native AOT** — ネイティブコンパイルによる高速起動・単一バイナリ

---

## 動作環境

| 項目 | 要件 |
|---|---|
| OS | Windows 11（win-x64） |
| ランタイム | .NET 10（Native AOT ビルド済みのため実行時は不要） |
| 認証 | Anthropic アカウント（OAuth、ブラウザ認証） |

> Node.js と Claude Code CLI はアプリが `%LOCALAPPDATA%\UltraMDmemo\lib\` に自動インストールします。システムへのグローバルインストールは行いません。

---

## インストール・更新

### インストール

GitHub Releases から Velopack インストーラー（`UltraMDmemo-win-Setup.exe`）をダウンロードして実行します。

- スタートメニューとデスクトップにショートカットが作成されます
- Windows スタートアップに `--update-check` 付きで登録され、ログイン時にサイレント更新チェックが自動実行されます

### 自動更新

更新は以下の 2 経路で自動適用されます。

1. **Windows ログイン時** — スタートアップ登録により `--update-check` モードで起動し、UI なしでサイレント更新を実行
2. **アプリ起動時** — 通常起動のセットアップシーケンス内で更新を確認

いずれの場合も、GitHub Releases から最新版を検出するとダウンロード → 適用 → 再起動が自動で行われます。

### アンインストール

Windows の「設定 > アプリ」からアンインストールします。スタートアップ登録も自動で解除されます。

---

## 起動シーケンス

初回起動時、アプリは以下を自動実行します。

1. **Velopack ライフサイクル処理** — インストール/更新/アンインストール時のフック（スタートアップ登録等）
2. **Node.js インストール** — v20 LTS をローカルにダウンロード・展開
3. **Claude Code CLI インストール** — npm 経由で `@anthropic-ai/claude-code` をインストール
4. **認証** — 未ログインならブラウザを開いて OAuth 認証（完了を自動検知）
5. **接続検証** — CLI の疎通を確認
6. **メイン画面表示**

2回目以降は既存の環境を検出してスキップするため、高速に起動します。

---

## 使い方

### 基本操作

1. 左ペインにテキストを入力（最大 20,000 文字）
2. **「整形する」** ボタンをクリック
3. 右ペインに構造化 Markdown が表示される

### オプション

| 項目 | 説明 |
|---|---|
| **種類** | 文書の種類（自動判定 / 会議メモ / 要件メモ / インシデント記録 / 学習ノート / 記事下書き / チャット要約 / 汎用メモ） |
| **モード** | 整形の方針（バランス / 厳密 / 簡潔 / 詳細） |
| **末尾に原文挿入** | 変換結果の末尾に入力テキストをそのまま付与 |
| **タイトル指示** | タイトル生成のヒントを指定（任意） |

### 出力操作

- **コピー** — Markdown テキストをクリップボードにコピー
- **保存 (.md)** — `.md` ファイルとして保存
- **Meta保存 (.json)** — メタ情報（処理時間・設定・パス等）を JSON で保存

### 履歴

フッターの **「履歴」** ボタンから履歴パネルを開き、過去の変換結果を閲覧・再利用できます。

- **読込** — 入力と出力を画面に復元
- **再整形** — 過去の入力テキストを再度変換
- **削除** — 履歴ファイルを削除

---

## 出力テンプレート

変換結果は以下の構造で生成されます。

```markdown
# 2026-02-08 07:32_打ち合わせメモ（進捗と課題）

## サマリー
（全体の要約）

## 要点
（箇条書きの要点）

## 詳細
（詳細な内容）

## 不明点 / 要確認
（不明な点や確認事項）

## 決定事項 / 結論       ← 内容がある場合のみ
## TODO / 次アクション   ← - [ ] 形式
## 参照 / リンク         ← 内容がある場合のみ

---
## 原文                  ← 「末尾に原文挿入」ON の場合のみ
```

---

## データ保存先

```
%LOCALAPPDATA%\UltraMDmemo\
├─ current/                … Velopack がアプリ本体を配置
│  └─ UltraMDmemo.exe
├─ lib/
│  ├─ nodejs/              … Node.js ランタイム（自動ダウンロード）
│  ├─ npm/
│  │  └─ node_modules/
│  │     └─ @anthropic-ai/
│  │        └─ claude-code/ … Claude Code CLI
│  └─ npm-cache/
├─ history/                … 変換履歴
│  ├─ <id>.input.txt
│  ├─ <id>.output.md
│  └─ <id>.meta.json
└─ settings.json           … ユーザー設定
```

---

## 技術スタック

| カテゴリ | 技術 |
|---|---|
| フレームワーク | [Avalonia UI](https://avaloniaui.net/) 11.3 |
| UI テーマ | Avalonia FluentTheme |
| アーキテクチャ | MVVM（[CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) 8.4） |
| Markdown 表示 | [Markdown.Avalonia.Tight](https://github.com/whistyun/Markdown.Avalonia) 11.0 |
| 自動更新 | [Velopack](https://velopack.io/) |
| AI バックエンド | [Claude Code CLI](https://code.claude.com/) (`@anthropic-ai/claude-code`) |
| ターゲット | .NET 10 / win-x64 / Native AOT |

---

## プロジェクト構成

```
UltraMDmemo/
├─ .github/workflows/
│  ├─ dotnet-build.yml           … PR 時ビルド検証
│  └─ velopack-release.yml       … リリースビルド＋配布
├─ Models/
│  ├─ AppJsonContext.cs           … JSON Source Generator（AOT 対応）
│  ├─ AppSettings.cs
│  ├─ HistoryPaths.cs
│  ├─ LabeledValue.cs
│  ├─ TransformError.cs
│  ├─ TransformIntent.cs
│  ├─ TransformMeta.cs
│  ├─ TransformMode.cs
│  ├─ TransformRequest.cs
│  └─ TransformResult.cs
├─ ViewModels/
│  ├─ MainWindowViewModel.cs
│  └─ ViewModelBase.cs
├─ Views/
│  ├─ MainWindow.axaml
│  └─ MainWindow.axaml.cs
├─ Services/
│  ├─ AppPaths.cs                 … パス解決（Velopack/開発環境自動判定）
│  ├─ ClaudeCodeSetupService.cs   … Node.js/CLI セットアップ・認証
│  ├─ ClaudeCodeProcessHost.cs    … CLI プロセス実行
│  ├─ TransformService.cs         … 変換ロジック
│  ├─ HistoryService.cs           … 履歴 I/O
│  ├─ SettingsService.cs          … 設定管理
│  └─ StartupRegistration.cs      … Windows スタートアップ登録
├─ Converters/
│  └─ InverseBoolConverter.cs
├─ App.axaml / App.axaml.cs
├─ Program.cs                     … エントリポイント（Velopack フック・サイレント更新）
├─ Directory.Build.props           … バージョン管理
├─ TrimmerRoots.xml                … AOT Trimmer 保護設定
├─ UltraMDmemo.csproj
└─ UltraMDmemo.slnx
```

---

## ビルド

```bash
# 復元 + ビルド
dotnet restore UltraMDmemo.slnx
dotnet build UltraMDmemo.slnx --configuration Release

# 実行（開発環境）
dotnet run --project UltraMDmemo.csproj

# Native AOT パブリッシュ
dotnet publish UltraMDmemo.csproj -c Release -r win-x64
```

---

## バージョン管理

アプリのバージョンは `Directory.Build.props` の `<Version>` で一元管理します。

```xml
<Version>1.0.2</Version>
```

リリース時は CI/CD がこの値を読み取り、Velopack パッケージのバージョンとして使用します。

---

## CI/CD

| ワークフロー | トリガー | 内容 |
|---|---|---|
| `dotnet-build.yml` | `pull_request` | ビルド検証 + 成果物アップロード |
| `velopack-release.yml` | `push`（`release/**` ブランチ） | AOT パブリッシュ → Velopack パッケージ作成 → GitHub Releases 公開 |

### リリース手順

1. `Directory.Build.props` の `<Version>` を更新
2. `release/X.Y.Z` ブランチを作成してプッシュ
3. GitHub Actions が自動で以下を実行:
   - `dotnet publish`（Native AOT）
   - `vpk pack`（ショートカット作成: スタートメニュー・デスクトップ）
   - 既存の同バージョンリリースを削除
   - `vpk upload github`（`--channel win` で GitHub Releases に公開）
4. ユーザーのアプリが次回ログイン時または起動時に自動更新を検知・適用

---

## ライセンス

MIT

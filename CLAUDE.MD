# 仕様書（実装構成版）：UltraMDmemo

**Windowsネイティブデスクトップ（Avalonia + MVVM） / Claude Code（アプリ内セットアップ・ブラウザ認証）**

---

## 0. 実装前提（確定）

- OS：Windows 11 25H2（ビルド 26200）
- 開発言語・ランタイム：C# / **.NET 10**
- アプリ形態：Avalonia Desktop（Web API サーバー常駐なし）
- ソリューション形式：**`.slnx` のみ使用**（`.sln` は作成しない）
- 実行対象：**`<RuntimeIdentifier>win-x64</RuntimeIdentifier>` を必須**
- UI設計方針：MVVMベース、デザイン（色・幅・レイアウト）はXAML側で管理
- 出力形式：Markdown固定
- 入力上限：全角 20,000字（概算 `text.Length <= 20000`）
- 履歴保存先：`%LOCALAPPDATA%\UltraMDmemo\history` 固定
- タイトル形式：`日時 + 推定タイトル`
- Claude Code 接続：**アプリ内完結**（Node.js + Claude Code CLI をローカルインストール、ブラウザ OAuth 認証）

---

## 1. 目的

自由入力メモを、ボタン操作だけで汎用の構造化Markdownへ変換し、プレビュー・コピー・保存・履歴再利用をローカル完結で実現する。
Claude Code との接続はアプリが自動セットアップし、未ログイン時はブラウザを起動して認証を行う。ユーザーは事前にCLIをインストールする必要がない。

---

## 2. 構成方針（参照実装準拠）

ShogunGUI の実装方式を踏襲し、以下の責務分離で構成する。

1. **View（AXAML）**
   - 画面レイアウトと見た目を定義
   - 入力、結果表示、履歴一覧、状態表示のUIバインディングを保持
2. **ViewModel（MVVM）**
   - 画面状態、コマンド、非同期実行制御、UI通知
3. **Services**
   - Claude Code セットアップ（Node.js/CLI インストール・認証）、プロセスホスト、Markdown変換、履歴I/O、設定読み書き
4. **Models**
   - 変換要求/結果、履歴メタ、設定、エラー情報
5. **Infrastructure**
   - ファイルパス解決、プロセス実行ラッパー、ログ

---

## 3. 推奨プロジェクト構成

```text
UltraMDmemo/
├─ .github/
│  └─ workflows/
│     ├─ dotnet-build.yml      … プッシュ時ビルド検証
│     └─ velopack-release.yml  … リリースビルド＋配布
├─ UltraMDmemo.Avalonia.slnx
├─ UltraMDmemo.Avalonia/
│  ├─ App.axaml
│  ├─ MainWindow.axaml
│  ├─ MainWindow.axaml.cs
│  ├─ Models/
│  ├─ ViewModels/
│  ├─ Services/
│  ├─ Converters/
│  └─ UltraMDmemo.Avalonia.csproj
```

ランタイム資材の自動配置先（`LocalApplicationData` 配下）：

```text
%LOCALAPPDATA%\UltraMDmemo\
├─ lib/
│  ├─ nodejs/            … Node.js ランタイム（自動ダウンロード）
│  ├─ npm/
│  │  └─ node_modules/
│  │     └─ @anthropic-ai/
│  │        └─ claude-code/
│  │           └─ cli.js  … Claude Code CLI 本体
│  └─ npm-cache/         … npm キャッシュ
└─ history/              … 変換履歴（入力・出力・メタ）
```

`.csproj` の主要条件：

- `TargetFramework`：`net10.0`
- `RuntimeIdentifier`：`win-x64`
- `Nullable`：`enable`
- `PackageReference`：`Velopack`（自動更新）

---

## 4. 画面仕様（MainWindow）

### 4.1 レイアウト

- **ヘッダー**
  - タイトル：`UltraMDmemo`
  - 実行ボタン：`整形する`
  - 補助ボタン：`クリア`
  - オプション：`intent` / `mode` / `include_raw` / `title_hint`
- **メイン（2ペイン）**
  - 左：入力エリア（複数行テキスト）
  - 右：結果表示（`Preview` / `Raw` タブ）
- **フッター**
  - ステータス（成功/失敗、処理時間、文字数、警告）
  - 履歴導線（一覧、詳細、再整形）

### 4.2 操作

- 実行：入力を変換して右ペインへ表示
- 出力操作：`コピー` / `保存(.md)` / `meta保存(.json)`
- 履歴操作：一覧（新しい順）/ 詳細表示 / 再実行 / 削除

---

## 5. ViewModel仕様（実装の中心）

`MainWindowViewModel` に以下を集約する。

- **状態**
  - `InputText`、`OutputMarkdown`、`IsProcessing`、`Warnings`
  - `InputLength`、`DurationMs`、`HistoryItems`
- **コマンド**
  - `TransformCommand`
  - `ClearCommand`
  - `CopyCommand`
  - `SaveMarkdownCommand`
  - `SaveMetaCommand`
  - `OpenHistoryCommand` / `ReloadHistoryCommand` / `DeleteHistoryCommand` / `RerunHistoryCommand`
- **初期化（起動シーケンス）**
  0. Velopack 自動更新チェック（`Program.cs`、UI表示前に実行。更新があれば適用・再起動）
  1. 設定ロード
  2. Node.js ランタイム確認（未導入なら自動ダウンロード・展開）
  3. Claude Code CLI 確認（未導入なら `npm install -g @anthropic-ai/claude-code`）
  4. ログイン状態チェック（`claude config get` の終了コードで判定）
  5. 未ログインの場合：ブラウザ認証起動 → ポーリングで完了待ち（10秒間隔、最大10分）
  6. 接続検証（`claude -p "test"` で疎通確認）
  7. 履歴インデックス読込
  - 各ステップの進捗はローディング画面に表示する

---

## 6. サービス仕様

### 6.1 `IClaudeCodeSetupService`（ShogunGUI 準拠）

Node.js と Claude Code CLI のローカルインストール・認証を管理する。

- **Node.js インストール**
  - 公式バイナリ（win-x64）を `%LOCALAPPDATA%\UltraMDmemo\lib\nodejs` にダウンロード・展開
  - バージョン：v20系LTS（例: v20.20.0）
  - 既にインストール済みならスキップ
- **Claude Code CLI インストール**
  - ローカル Node.js の npm で `npm install -g @anthropic-ai/claude-code` を実行
  - インストール先：`%LOCALAPPDATA%\UltraMDmemo\lib\npm\node_modules\@anthropic-ai\claude-code`
  - エントリポイント：`cli.js`
- **ログイン管理**
  - `IsLoggedInAsync()`：`claude config get` を実行し終了コード 0 でログイン済みと判定（`CI=true` で非対話モード）
  - `RunLoginAsync()`：`node cli.js login` を `cmd.exe` 経由で起動（ブラウザが開き OAuth 認証）
  - ログイン完了まで 10秒間隔でポーリング（最大60回 = 10分）
- **接続検証**
  - `VerifyConnectivityAsync()`：`claude -p "test"` を実行し疎通確認
- **パス解決**
  - `GetNodePath()`：ローカル Node.js 実行ファイルパス
  - `GetCliJsPath()`：Claude Code CLI の `cli.js` パス

### 6.2 `IClaudeCodeProcessHost`

ローカルインストールした Node.js + cli.js を経由して Claude Code を実行する。

- `IClaudeCodeSetupService` からパスを取得し、`node cli.js -p <prompt>` を実行
- stdin に本文（UTF-8）を書き込み
- stdout を Markdown として取得
- タイムアウト（30秒）制御
- 環境変数 `PATH` にローカル Node.js の bin ディレクトリを追加して起動
- `--dangerously-skip-permissions` は使用しない（安全性確保）

### 6.3 `ITransformService`

- 入力チェック（空文字、20,000字超過）
- `IClaudeCodeProcessHost` への実行要求の組み立て
- 出力Markdownの最低構造検証
- `TransformResult` を返却

### 6.4 `IHistoryService`

- `%LOCALAPPDATA%\UltraMDmemo\history` への保存・読み出し・削除
- 1実行あたり3ファイル管理
  - `<id>.input.txt`
  - `<id>.output.md`
  - `<id>.meta.json`

### 6.5 `ISettingsService`

- UI設定・既定値管理（intent、mode、include_raw など）
- 実行オプションの永続化

---

## 7. 内部データ契約

### 7.1 変換要求（内部モデル）

```json
{
  "text": "string",
  "intent": "auto|meeting|requirements|incident|study|draft_article|chat_summary|generic",
  "mode": "balanced|strict|compact|verbose",
  "include_raw": true,
  "title_hint": "string|null"
}
```

### 7.2 変換結果（内部モデル）

```json
{
  "markdown": "string",
  "meta": {
    "id": "string",
    "created_at": "ISO-8601",
    "duration_ms": 1234,
    "intent": "string",
    "mode": "string",
    "include_raw": true,
    "input_chars": 12345,
    "warnings": ["string"],
    "history_paths": {
      "input": "string",
      "output": "string",
      "meta": "string"
    }
  }
}
```

### 7.3 エラーモデル

```json
{
  "error": {
    "code": "INPUT_TOO_LARGE|CLI_FAILED|TIMEOUT|INVALID_REQUEST|SETUP_FAILED|LOGIN_REQUIRED|LOGIN_TIMEOUT",
    "message": "string",
    "details": "string|null"
  }
}
```

---

## 8. Claude Code 接続基盤（ShogunGUI 準拠）

### 8.1 アーキテクチャ概要

```text
┌──────────────────────────────────────────────────┐
│              UltraMDmemo（Avalonia）              │
│                                                  │
│  ViewModel                                       │
│    │                                             │
│    ├─ IClaudeCodeSetupService                    │
│    │    ├─ Node.js ダウンロード・展開             │
│    │    ├─ npm install Claude Code CLI            │
│    │    ├─ ログイン状態チェック                    │
│    │    └─ ブラウザ認証起動                        │
│    │                                             │
│    ├─ ITransformService                          │
│    │    └─ 入力検証 → ProcessHost 呼出            │
│    │                                             │
│    └─ IClaudeCodeProcessHost                     │
│         └─ node cli.js -p <prompt>               │
│              stdin/stdout パイプ通信              │
└──────────────────────────────────────────────────┘
                      │
                      │ プロセス起動（stdin/stdout）
                      ▼
┌──────────────────────────────────────────────────┐
│  %LOCALAPPDATA%\UltraMDmemo\lib\                  │
│    nodejs\node.exe                               │
│    npm\node_modules\@anthropic-ai\claude-code\   │
│      cli.js                                      │
└──────────────────────────────────────────────────┘
                      │
                      │ Claude Code CLI が管理
                      ▼
┌──────────────────────────────────────────────────┐
│  Anthropic API（クラウド）                         │
│  OAuth トークンで認証                              │
└──────────────────────────────────────────────────┘
```

### 8.2 起動シーケンス

```text
アプリ起動
  │
  ├─ Node.js 存在チェック
  │    ├─ なし → ダウンロード・展開 → 再チェック
  │    └─ あり → 次へ
  │
  ├─ Claude Code CLI 存在チェック
  │    ├─ なし → npm install → 再チェック
  │    └─ あり → 次へ
  │
  ├─ ログイン状態チェック（claude config get）
  │    ├─ 未ログイン → ブラウザ認証起動
  │    │    └─ 10秒間隔ポーリング（最大10分）
  │    │         ├─ ログイン確認 → 次へ
  │    │         └─ タイムアウト → エラー表示
  │    └─ ログイン済み → 次へ
  │
  ├─ 接続検証（claude -p "test"）
  │    ├─ 失敗 → エラー表示
  │    └─ 成功 → 次へ
  │
  └─ メイン画面表示（履歴読込）
```

### 8.3 プロセス実行方式

- 変換実行ごとに `node cli.js -p <prompt>` をプロセス起動（ジョブ単位）
- 環境変数 `PATH` にローカル Node.js の bin ディレクトリを先頭追加
- 非対話モードで実行（`CI=true`）
- stdin にシステムプロンプト + ユーザー入力を書き込み
- stdout から Markdown 結果を取得
- タイムアウト 30秒で強制終了

---

## 9. 変換仕様（Markdown品質）

### 9.1 必須テンプレート

1. `# タイトル`
2. `## サマリー`
3. `## 要点`
4. `## 詳細`
5. `## 不明点 / 要確認`
6. 任意（内容がある場合のみ）
   - `## 決定事項 / 結論`
   - `## TODO / 次アクション`
   - `## 参照 / リンク`
7. `---`
8. `## 原文`（`include_raw=true` の場合のみ）

### 9.2 品質ルール

- 推測で補完しない（不明点に記載）
- TODO は `- [ ]` 形式
- 固有名詞・用語は極力維持
- `intent` は「詳細」配下の整理にのみ使い、骨格は固定

---

## 10. タイトル生成仕様

- 形式：`# {YYYY-MM-DD HH:mm}_{推定タイトル}`
- 推定タイトルは最大30文字程度
- ファイル名禁止文字 `\/:*?"<>|` は除去

例：

- `# 2026-02-08 07:32_打ち合わせメモ（進捗と課題）`
- `# 2026-02-08 07:45_要件メモ：整形アプリMVP`

---

## 11. 履歴保存仕様

### 11.1 保存先

- 固定：`%LOCALAPPDATA%\UltraMDmemo\history`

### 11.2 ID規則

- `YYYYMMDD_HHMMSS_<rand6>` 形式
- 同一秒多重実行でも衝突しないこと

### 11.3 meta.json 例

```json
{
  "id": "20260208_073201_a1b2c3",
  "created_at": "2026-02-08T07:32:01+09:00",
  "title": "2026-02-08 07:32_要件メモ：整形アプリMVP",
  "intent": "auto",
  "mode": "strict",
  "include_raw": true,
  "title_hint": null,
  "input_chars": 19876,
  "duration_ms": 8421,
  "warnings": [],
  "paths": {
    "input": "%LOCALAPPDATA%\\UltraMDmemo\\history\\20260208_073201_a1b2c3.input.txt",
    "output": "%LOCALAPPDATA%\\UltraMDmemo\\history\\20260208_073201_a1b2c3.output.md",
    "meta": "%LOCALAPPDATA%\\UltraMDmemo\\history\\20260208_073201_a1b2c3.meta.json"
  }
}
```

---

## 12. セキュリティ・運用要件

- ネットワーク公開しない（ローカルアプリ内で完結）
- 外部通信は以下のタイミングのみ発生する
  - アプリ起動時の自動更新チェック（GitHub Releases）
  - Node.js ダウンロード時（初回セットアップ）
  - `npm install` 時（Claude Code CLI インストール）
  - ブラウザ OAuth 認証時（未ログイン時のみ）
  - Claude Code CLI 実行時（ユーザーが「整形する」を押した時）
- 認証トークンは Claude Code CLI が自身の設定ディレクトリで管理（アプリは直接アクセスしない）
- APIキーの管理・入力は不要（OAuth トークンで認証）
- 入力上限 20,000 字
- 実行タイムアウト 30 秒
- 履歴には機密が含まれうるため、保管先権限はユーザー環境で管理
- ランタイム資材（`%LOCALAPPDATA%\UltraMDmemo\lib`）はユーザー権限内に収まる

---

## 13. 自動更新（Velopack）（MarkItDown.GUI 準拠）

### 13.1 概要

Velopack を使用してアプリの自動更新を実現する。起動時に GitHub Releases から最新バージョンを確認し、更新があれば自動でダウンロード・適用・再起動する。

### 13.2 更新フロー（`Program.cs` 起動時）

```text
アプリ起動
  │
  ├─ VelopackApp.Build().Run()  … Velopack 初期化
  │
  ├─ UpdateManager 生成（フィードURL指定）
  │
  ├─ CheckForUpdatesAsync()
  │    ├─ 更新あり → DownloadUpdatesAsync() → ApplyUpdatesAndRestart() → アプリ終了
  │    └─ 更新なし → 次へ
  │
  ├─ NotInstalledException → 開発環境のためスキップ（ログ出力のみ）
  │
  └─ 通常の起動シーケンスへ（セットアップ・認証・メイン画面）
```

### 13.3 更新フィードURL

- 既定値：`https://github.com/{owner}/UltraMDmemo/releases/latest/download`
- 設定ファイル（`appsettings.xml`）で上書き可能

### 13.4 Velopack 環境検出

- Velopack インストール時のアプリパスは `%LOCALAPPDATA%\UltraMDmemo\current\` となる
- `AppDomain.CurrentDomain.BaseDirectory` が `\current` で終わるかで判定
- Velopack 環境の場合：`lib` / `history` は `current` の親ディレクトリ配下に配置
- 開発環境の場合：アプリ直下に `lib` / `history` を配置

```text
%LOCALAPPDATA%\UltraMDmemo\
├─ current/              … Velopack がアプリ本体を配置
│  └─ UltraMDmemo.exe
├─ lib/                  … ランタイム資材（current の外）
│  ├─ nodejs/
│  ├─ npm/
│  └─ npm-cache/
└─ history/              … 変換履歴（current の外）
```

### 13.5 エラーハンドリング

- `NotInstalledException`：開発環境と判断し更新チェックをスキップ
- その他の例外：ログ出力のみで起動を継続（更新失敗でアプリが使えなくなることを防止）

---

## 14. CI/CD（GitHub Actions）（MarkItDown.GUI 準拠）

### 14.1 ワークフロー構成

| ファイル | トリガー | 目的 |
|---|---|---|
| `dotnet-build.yml` | `push`（`releases` / `release/**` 以外） | ビルド検証 |
| `velopack-release.yml` | `push`（`releases` または `release/**`） | リリースビルド＋配布 |

### 14.2 ビルドワークフロー（`dotnet-build.yml`）

```yaml
name: .NET ビルド

on:
  push:
    branches-ignore:
      - releases
      - release/**

jobs:
  build:
    name: .NET ビルド
    runs-on: windows-latest

    steps:
      - name: リポジトリをチェックアウト
        uses: actions/checkout@v4

      - name: .NET SDK をセットアップ
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: 依存関係を復元
        run: dotnet restore UltraMDmemo.slnx

      - name: ビルド
        run: dotnet build UltraMDmemo.slnx --configuration Release --no-restore
```

### 14.3 リリースワークフロー（`velopack-release.yml`）

```yaml
name: Velopack リリース

on:
  push:
    branches:
      - releases
      - release/**

permissions:
  contents: write

jobs:
  release:
    runs-on: windows-latest
    steps:
      - name: リポジトリをチェックアウト
        uses: actions/checkout@v4

      - name: リリースバージョンを決定
        shell: pwsh
        run: |
          $refName = '${{ github.ref_name }}'
          if ($refName -match '^release\/(?<version>\d+\.\d+\.\d+)$') {
            $version = $Matches['version']
          } else {
            $version = '1.0.${{ github.run_number }}'
          }
          "VPK_VERSION=$version" | Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append

      - name: .NET SDK をセットアップ
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: 依存関係を復元
        run: dotnet restore UltraMDmemo/UltraMDmemo.csproj

      - name: アプリを発行
        run: dotnet publish UltraMDmemo/UltraMDmemo.csproj -c Release -o artifacts/publish -r win-x64

      - name: Velopack CLI をインストール
        shell: pwsh
        run: |
          dotnet tool install --global vpk
          $toolsPath = "$env:USERPROFILE\.dotnet\tools"
          Add-Content -LiteralPath $env:GITHUB_PATH -Value $toolsPath -Encoding utf8

      - name: Velopack パッケージを作成
        shell: pwsh
        run: |
          vpk pack `
            --packId UltraMDmemo `
            --packVersion $env:VPK_VERSION `
            --packDir artifacts/publish `
            --mainExe UltraMDmemo.exe `
            --outputDir artifacts/releases

      - name: GitHub Releases へアップロード
        shell: pwsh
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          $repoUrl = "https://github.com/${{ github.repository }}"
          vpk upload github `
            --repoUrl $repoUrl `
            --outputDir artifacts/releases `
            --tag v$env:VPK_VERSION `
            --publish `
            --token $env:GITHUB_TOKEN
```

### 14.4 バージョニング規則

- ブランチ名 `release/X.Y.Z` → バージョン `X.Y.Z` を使用
- それ以外（`releases` ブランチ等）→ `1.0.{run_number}` をフォールバック
- GitHub Releases のタグ：`v{version}`（例: `v1.0.50`）

### 14.5 リリース手順

1. `release/1.0.0` のようなブランチを作成してプッシュ
2. GitHub Actions が自動で `dotnet publish` → `vpk pack` → `vpk upload github` を実行
3. GitHub Releases に `v1.0.0` タグでリリースが公開される
4. ユーザーのアプリが次回起動時に自動更新を検知

---

## 15. エラーハンドリング

- **自動更新チェック失敗**：ログ出力のみで起動を継続（更新失敗でアプリが使えなくなることを防止）
- **開発環境での更新チェック**：`NotInstalledException` をキャッチしスキップ
- **Node.js ダウンロード失敗**：エラーメッセージ表示、リトライ導線を提示
- **Claude Code CLI インストール失敗**：エラーメッセージ表示、リトライ導線を提示
- **未ログイン**：ブラウザ認証を起動し、ポーリングで完了を待つ
- **ログインタイムアウト（10分超過）**：タイムアウトメッセージ表示、アプリ再起動を案内
- **接続検証失敗**：Claude Code との疎通不可を表示、再ログイン導線を提示
- **入力超過**：実行ボタン無効化 + エラーメッセージ表示
- **CLI実行失敗/起動失敗**：エラー詳細を表示し処理中断
- **タイムアウト（30秒）**：実行停止して再試行可能状態へ復帰
- **出力テンプレ不足**：警告付きで表示し、必要時に再整形導線を提示

---

## 16. 受け入れ条件（DoD）

### セットアップ・認証

- 初回起動時に Node.js が `%LOCALAPPDATA%\UltraMDmemo\lib\nodejs` に自動インストールされる
- Claude Code CLI が npm 経由で自動インストールされる
- 未ログイン時にブラウザが起動し OAuth 認証が行える
- ログイン完了後、アプリが自動で検知して次のステップへ進む
- セットアップ進捗がローディング画面に表示される

### 機能

- 入力→`整形する` でMarkdownが表示される
- Preview/Raw の切替が機能する
- 20,000字制限がUIと処理層の両方で有効
- ローカルインストールした Claude Code CLI（`node cli.js -p`）経由で変換が実行される
- `.md`保存、コピー、meta保存が動作する
- 履歴が `%LOCALAPPDATA%\UltraMDmemo\history` に3ファイルで保存される
- 履歴一覧/詳細/削除/再整形が動作する
- タイトルが「日時 + 推定タイトル」で生成される

### 自動更新

- 起動時に GitHub Releases から更新を自動チェックする
- 更新がある場合、ダウンロード → 適用 → 再起動が自動で行われる
- 開発環境（Velopack 未インストール）では更新チェックをスキップする
- 更新チェック失敗時もアプリは正常に起動する

### CI/CD

- `release/**` 以外のブランチへのプッシュでビルド検証が実行される
- `release/X.Y.Z` ブランチへのプッシュで Velopack パッケージが作成され GitHub Releases に公開される
- リリースタグが `vX.Y.Z` 形式で自動付与される

### 構成

- `.slnx` 構成、`.NET 10`、`win-x64` 設定が維持される
- システムの Node.js / Claude CLI に依存しない（アプリ内完結）

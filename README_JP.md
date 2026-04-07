# CashChanger Simulator CLI

本プロジェクトは、UnifiedPOS (UPOS) 規格に準拠した釣銭機シミュレーターのコマンドラインインターフェース（CLI）版です。
GUI環境がないサーバーやCI環境、またはターミナル上での素早いデバッグ作業に最適化されています。

## 主な機能

- **対話型シェル (REPL)**: オートコンプリートや履歴保存機能を備えた対話型インターフェースを提供します。
- **UPOS 規格準拠**: デバイスのオープンから入出金サイクル（`BeginDeposit`〜`EndDeposit`）まで、標準的な操作をすべてサポート。
- **スクリプト実行**: JSON 形式のシナリオファイルを読み込み、一連の操作を自動で実行できます。
- **多言語対応**: コンソールメッセージの完全なローカライズをサポートしています。
- **設定管理**: TOML ファイルベースの設定を CLI 上から直接参照・変更・保存が可能です。

## セットアップ

### 前提条件

- .NET 10.0 SDK
- Windows OS (POS for .NET 依存のため)

### ビルドと実行

1. ターミナルで本ディレクトリに移動し、ビルドします。

   ```powershell
   dotnet build
   ```

2. CLI を起動します。

   ```powershell
   dotnet run --project src/Cli/CashChangerSimulator.UI.Cli.csproj
   ```

## 主要な操作コマンド

CLI 起動後の対話型プロンプト（`>`）で以下のコマンドが使用可能です。

| コマンド | 説明 |
| :--- | :--- |
| `status` | デバイスの状態と現在の在庫（Inventory）を表示します。 |
| `deposit [金額]` | 入金処理を開始します。 |
| `fix-deposit` | 投入された現金を確定（Escrow から本体へ移動）します。 |
| `end-deposit` | 入金処理を完了し、在庫を更新します。 |
| `dispense <金額>` | 指定した金額の払い出しを実行します。 |
| `read-counts` | 現在の在高情報をデバイスから再読み込みします。 |
| `adjust-counts` | 「1000:5,500:10」形式で在庫を手動調整します。 |
| `history` | 取引履歴を表示します。 |
| `run-script <path>` | 指定した JSON シナリオファイルを実行します。 |
| `config list` | 現在の設定値を一覧表示します。 |
| `help` | 利用可能な全コマンドと詳細な使いかたを表示します。 |

## ドキュメント

- [Architecture Overview](docs/Architecture_JP.md): アーキテクチャの概要
- [UPOS Compliance Mapping](docs/UposComplianceMapping_JP.md): UPOS 対応状況
- [操作説明書 (CLI)](docs/CliOperatingInstructions_JP.md): より詳細な CLI の使用ガイド

---
*For the English version, see [README.md](README.md).*

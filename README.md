# CashChanger Simulator CLI

This project is the Command Line Interface (CLI) version of the CashChanger Simulator, which emulates UnifiedPOS (UPOS) standard operations. It is optimized for server environments, CI pipelines, or quick debugging in a terminal.

## Key Features

- **Interactive Shell (REPL)**: Provides an interactive interface with auto-completion and command history.
- **UPOS Compliant**: Supports all standard operations, from opening the device to full deposit cycles (`BeginDeposit` to `EndDeposit`).
- **Scripted Automation**: Execute a series of operations automatically by loading JSON-based scenario files.
- **Localization**: Full support for localized console messages.
- **Configuration Management**: View, change, and save TOML-based configurations directly from the CLI.

## Setup

### Prerequisites

- .NET 10.0 SDK
- Windows OS (Required for POS for .NET dependency)

### Build and Run

1. Navigate to this directory in your terminal and build:

   ```powershell
   dotnet build
   ```

2. Start the CLI:

   ```powershell
   dotnet run --project src/Cli/CashChangerSimulator.UI.Cli.csproj
   ```

## Key Commands

After starting the CLI, you can use the following commands at the interactive prompt (`>`):

| Command | Description |
| :--- | :--- |
| `status` | Displays the device state and current inventory. |
| `deposit [amount]` | Starts the deposit process. |
| `fix-deposit` | Commits the inserted cash (moves from Escrow to main storage). |
| `end-deposit` | Completes the deposit process and updates inventory. |
| `dispense <amount>` | Executes a dispense operation for the specified amount. |
| `read-counts` | Reloads current inventory information from the device. |
| `adjust-counts` | Manually adjusts inventory using the format "1000:5,500:10". |
| `history` | Displays transaction history. |
| `run-script <path>` | Executes the specified JSON scenario file. |
| `config list` | Lists all current configuration values. |
| `help` | Displays all available commands and detailed usage. |

## Documentation

- [Architecture Overview](docs/Architecture.md): High-level system design.
- [UPOS Compliance Mapping](docs/UposComplianceMapping.md): Status of UPOS interface implementation.
- [Operating Instructions (CLI)](docs/CliOperatingInstructions.md): Detailed guide for CLI usage.

---
*For the Japanese version, see [README_JP.md](README_JP.md).*

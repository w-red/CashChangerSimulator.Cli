using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.Virtual;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using R3;

namespace CashChangerSimulator.Cli.Services;

public class CliCashService : CliServiceBase
{
    private readonly ICashChangerDevice _device;
    private readonly Inventory _inventory;
    private readonly ICurrencyMetadataProvider _metadata;
    private readonly CliSessionOptions _options;

    public CliCashService(
        ICashChangerDevice device,
        Inventory inventory,
        ICurrencyMetadataProvider metadata,
        CliSessionOptions options,
        IAnsiConsole console,
        IStringLocalizer localizer) : base(console, localizer)
    {
        _device = device;
        _inventory = inventory;
        _metadata = metadata;
        _options = options;
    }

    /// <summary>現在の在庫数を読み取ります。</summary>
    public virtual void ReadCashCounts()
    {
        try
        {
            // ICashChangerDevice.ReadInventoryAsync returns the Inventory object.
            // In the virtual/simulator case, it might just be the injected inventory.
            var inventory = _device.ReadInventoryAsync().GetAwaiter().GetResult();
            ReportSuccess(_L["messages.cash_counts_updated"]);

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn(_L["messages.denomination_label"]);
            table.AddColumn(new TableColumn(_L["messages.count_label"]).RightAligned());
            table.AddColumn(new TableColumn(_L["messages.amount_label"]).RightAligned());

            var currencyCode = _metadata.CurrencyCode;
            var prefix = _metadata.SymbolPrefix.CurrentValue;
            var suffix = _metadata.SymbolSuffix.CurrentValue;

            foreach (var key in _metadata.SupportedDenominations)
            {
                var count = inventory.GetCount(key);
                var amount = key.Value * count;
                table.AddRow(
                    key.ToDenominationString(),
                    count.ToString(),
                    $"{prefix}{amount:N0}{suffix}"
                );
            }

            table.Caption($"{_L["messages.total_caption"]}: {prefix}{_inventory.CalculateTotal(currencyCode):N0}{suffix}");
            _console.Write(table);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    /// <summary>入金トレイ（Escrow）の状態を表示します。</summary>
    public virtual void ShowDepositTray()
    {
        var escrow = _inventory.EscrowCounts.ToList();
        // ICashChangerDevice might not have IsDepositInProgress directly, 
        // we check if the state is anything other than Idle/Closed if relevant.
        var isIdle = _device.State.CurrentValue == DeviceControlState.Idle;
        if (!escrow.Any() && isIdle) return;

        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Yellow);
        table.Title($"[yellow]{_L["messages.deposit_tray_label"]}[/]");
        table.AddColumn(_L["messages.denomination_label"]);
        table.AddColumn(new TableColumn(_L["messages.count_label"]).RightAligned());
        table.AddColumn(new TableColumn(_L["messages.amount_label"]).RightAligned());

        var prefix = _metadata.SymbolPrefix.CurrentValue;
        var suffix = _metadata.SymbolSuffix.CurrentValue;
        decimal trayTotal = 0;

        foreach (var key in _metadata.SupportedDenominations)
        {
            var count = escrow.FirstOrDefault(e => e.Key.Value == key.Value && e.Key.Type == key.Type).Value;
            if (count > 0)
            {
                var amount = key.Value * count;
                trayTotal += amount;
                table.AddRow(
                    key.ToDenominationString(),
                    count.ToString(),
                    $"{prefix}{amount:N0}{suffix}"
                );
            }
        }

        // Handle RequiredAmount if it is a Simulator
        decimal required = 0;
        if (_device is VirtualCashChangerDevice simulator)
        {
            // Note: If RequiredAmount property exists in simulator, use it
            // Assuming for now it is handled internally or we expose it.
        }

        var remaining = Math.Max(0, required - trayTotal);

        var caption = $"{_L["messages.total_caption"]}: {prefix}{trayTotal:N0}{suffix}";
        if (required > 0)
        {
            caption += $" / {_L["messages.required_amount_label"]}: {prefix}{required:N0}{suffix} ([red]{_L["messages.remaining_label"]}: {prefix}{remaining:N0}{suffix}[/])";
        }
        table.Caption(caption);
        _console.Write(table);
    }

    /// <summary>入金を開始します。</summary>
    /// <param name="amount">目標金額（省略時は全ての投入を受け入れ）。</param>
    public virtual void Deposit(int? amount)
    {
        try
        {
            if (_options.IsAsync)
            {
                _console.MarkupLine(_L["messages.deposit_started", amount?.ToString() ?? "All", "True"]);
                // If amount is specified, we might need a way to pass it to the device.
                // ICashChangerDevice might need a way to set target amount if that is a standard requirement.
                _device.BeginDepositAsync().GetAwaiter().GetResult();
                _console.MarkupLine(_L["messages.deposit_async_warning"]);
                ShowDepositTray();
            }
            else
            {
                _console.MarkupLine(_L["messages.deposit_started", amount?.ToString() ?? "All", "False"]);
                _device.BeginDepositAsync().GetAwaiter().GetResult();
                _device.FixDepositAsync().GetAwaiter().GetResult();
                ShowDepositTray();
                _device.EndDepositAsync(DepositAction.NoChange).GetAwaiter().GetResult();
                ReportSuccess(_L["messages.deposit_completed"]);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    /// <summary>入金を確定（Escrow から本体へ移動）します。</summary>
    public virtual void FixDeposit()
    {
        try
        {
            _device.FixDepositAsync().GetAwaiter().GetResult();
            ShowDepositTray();
            ReportSuccess(_L["messages.deposit_fixed"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    /// <summary>入金処理を終了します。</summary>
    public virtual void EndDeposit()
    {
        try
        {
            ShowDepositTray();
            _device.EndDepositAsync(DepositAction.NoChange).GetAwaiter().GetResult();
            ReportSuccess(_L["messages.end_deposit_completed"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    /// <summary>在庫数を直接調整します。</summary>
    /// <param name="input">"1000:5,500:10" 形式の文字列。</param>
    public virtual void AdjustCashCounts(string input)
    {
        try
        {
            var currencyCode = _options.CurrencyCode;
            // factor is no longer needed if we use DenominationKey directly or decimal.
            var availableKeys = _metadata.SupportedDenominations
                .Select(d => new DenominationKey(d.Value, d.Type, currencyCode))
                .ToList();

            // Simple parsing for CLI demo purposes
            var counts = new List<CashDenominationCount>();
            var parts = input.Split(',');
            foreach (var part in parts)
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && decimal.TryParse(kv[0], out var val) && int.TryParse(kv[1], out var count))
                {
                    counts.Add(new CashDenominationCount(val, count));
                }
            }

            if (counts.Any())
            {
                _device.AdjustInventoryAsync(counts).GetAwaiter().GetResult();
                ReportSuccess(_L["messages.adjust_cash_counts_success", input]);
            }
            else
            {
                _console.MarkupLine(_L["messages.invalid_adjust_format"]);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    /// <summary>指定された金額の払い出しを実行します。</summary>
    /// <param name="amount">払い出し金額。</param>
    public virtual void Dispense(int amount)
    {
        try
        {
            _device.DispenseChangeAsync(amount).GetAwaiter().GetResult();
            ReportSuccess(_L["messages.dispensed_success", amount]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }
}

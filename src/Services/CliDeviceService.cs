using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Managers;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Core;
using CashChangerSimulator.Device.Virtual;

namespace CashChangerSimulator.Cli.Services;

public class CliDeviceService(
    ICashChangerDevice device,
    IAnsiConsole console,
    IStringLocalizer localizer) : CliServiceBase(console, localizer)
{
    private readonly ICashChangerDevice _device = device;

    /// <summary>デバイスをオープンします。</summary>
    public virtual void Open()
    {
        try
        {
            _device.OpenAsync().GetAwaiter().GetResult();
            ReportSuccess(_L["messages.device_opened"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    /// <summary>デバイスを占有します。</summary>
    /// <param name="timeout">タイムアウト(ms)。</param>
    public virtual void Claim(int timeout)
    {
        try
        {
            _device.ClaimAsync(timeout).GetAwaiter().GetResult();
            ReportSuccess(_L["messages.device_claimed"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    /// <summary>デバイスを有効化します。</summary>
    public virtual void Enable()
    {
        try
        {
            _device.EnableAsync().GetAwaiter().GetResult();
            ReportSuccess(_L["messages.device_enabled"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    /// <summary>デバイスを無効化します。</summary>
    public virtual void Disable()
    {
        try
        {
            _device.DisableAsync().GetAwaiter().GetResult();
            ReportSuccess(_L["messages.device_disabled"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    /// <summary>デバイスの占有を解除します。</summary>
    public virtual void Release()
    {
        try
        {
            _device.ReleaseAsync().GetAwaiter().GetResult();
            ReportSuccess(_L["messages.device_released"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    /// <summary>デバイスをクローズします。</summary>
    public virtual void Close()
    {
        try
        {
            _device.CloseAsync().GetAwaiter().GetResult();
            ReportSuccess(_L["messages.device_closed"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    /// <summary>回収庫の取り外し状態を設定します。</summary>
    /// <param name="removed">取り外されている場合は true。</param>
    public virtual void SetCollectionBoxRemoved(bool removed)
    {
        try
        {
            // Note: VirtualCashChangerDevice (ICashChangerDevice) might need an extension to allow simulator-specific controls
            // if they are not part of the standard interface.
            // For now, if we cannot access specialized hardware status, we report not supported or cast if safe.
            if (_device is VirtualCashChangerDevice simulator)
            {
                // Note: Use the simulator's own hardware status directly to ensure we are targeting the correct instance.
                simulator.HardwareStatus.SetCollectionBoxRemoved(removed);

                var msg = removed ? _L["messages.box_removed"] : _L["messages.box_inserted"];
                ReportSuccess(msg);
            }
            else
            {
                _console.MarkupLine("[yellow]Collection box control is only supported on virtual devices.[/]");
            }
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    /// <summary>回収庫の状態をリセット（挿入状態）にします。</summary>
    public virtual void ResetBox()
    {
        SetCollectionBoxRemoved(false);
    }

    protected override string GetSummary(DeviceErrorCode errorCode, int errorCodeExtended = 0)
    {
        if (errorCode == DeviceErrorCode.Extended)
        {
            // UPOS Standard: Full = 206, Empty = 205
            if (errorCodeExtended == 206) return (string)_L["messages.error_summary_full"];
            if (errorCodeExtended == 205) return (string)_L["messages.error_summary_empty"];
        }

        var summaryKey = $"messages.error_summary_{errorCode.ToString().ToLowerInvariant()}";
        var summary = _L[summaryKey];
        if (summary.ResourceNotFound)
        {
            return (string)_L["messages.error_summary_generic"];
        }
        return summary;
    }

    protected override string GetHint(DeviceErrorCode errorCode, int errorCodeExtended = 0)
    {
        var hintKey = $"messages.error_hint_{errorCode.ToString().ToLowerInvariant()}";
        var hint = _L[hintKey];
        if (hint.ResourceNotFound)
        {
            return (string)_L["messages.error_hint_generic"];
        }
        return hint;
    }
}

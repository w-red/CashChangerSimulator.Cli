using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Exceptions;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using R3;

namespace CashChangerSimulator.Cli.Services;

/// <summary>
/// CLI の各サービスクラスの基底クラス。
/// </summary>
public abstract class CliServiceBase(IAnsiConsole console, IStringLocalizer localizer) : IDisposable
{
    protected readonly IAnsiConsole _console = console;
    protected readonly IStringLocalizer _L = localizer;
    protected readonly CompositeDisposable _disposables = new();
    private bool _disposed;

    public void ReportSuccess(string? message = null)
    {
        _console.MarkupLine($"[green][[{_L["messages.success_label"]}]][/] {(message ?? string.Empty)}");
    }

    public void HandleException(Exception ex)
    {
        if (ex is DeviceException pex)
        {
            var summary = GetSummary(pex.ErrorCode, pex.ErrorCodeExtended);
            var hint = GetHint(pex.ErrorCode, pex.ErrorCodeExtended);
            var errorLabel = _L["messages.error_label"];
            var summaryLabel = _L["messages.summary_label"];
            var codeLabel = _L["messages.code_label"];

            _console.MarkupLine($"[bold red][[{errorLabel}]][/] [red]{pex.Message}[/]");
            _console.MarkupLine($"  [yellow]{summaryLabel}:[/] {summary}");
            _console.MarkupLine($"  [yellow]{codeLabel}   :[/] {(int)pex.ErrorCode} ({pex.ErrorCode}) / {pex.ErrorCodeExtended}");

            if (!string.IsNullOrEmpty(hint))
            {
                _console.MarkupLine(_L["messages.hint_format", hint]);
            }
        }
        else
        {
            _console.MarkupLine($"[red][[{_L["messages.error_label"]}]][/] {ex.Message}");
        }
    }

    protected virtual string GetSummary(DeviceErrorCode errorCode, int errorCodeExtended = 0)
    {
        var summaryKey = $"messages.error_summary_{errorCode.ToString().ToLowerInvariant()}";
        var summary = _L[summaryKey];
        return summary.ResourceNotFound ? (string)_L["messages.error_summary_generic"] : (string)summary;
    }

    protected virtual string GetHint(DeviceErrorCode errorCode, int errorCodeExtended = 0)
    {
        var hintKey = $"messages.error_hint_{errorCode.ToString().ToLowerInvariant()}";
        var hint = _L[hintKey];
        return hint.ResourceNotFound ? (string)_L["messages.error_hint_generic"] : (string)hint;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _disposables.Dispose();
        }

        _disposed = true;
    }
}

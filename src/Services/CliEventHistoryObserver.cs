using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Opos;
using R3;

namespace CashChangerSimulator.Cli.Services;

/// <summary>
/// ICashChangerDevice から発行される R3 イベントを購読し、
/// 取引履歴 (TransactionHistory) を記録するクロスプラットフォーム対応のオブザーバー。
/// </summary>
public class CliEventHistoryObserver : IDisposable
{
    private readonly ICashChangerDevice _device;
    private readonly TransactionHistory _history;
    private readonly CompositeDisposable _disposables = new();

    public CliEventHistoryObserver(ICashChangerDevice device, TransactionHistory history)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _history = history ?? throw new ArgumentNullException(nameof(history));

        // Data Events
        _device.DataEvents
            .Subscribe(_ =>
            {
                _history.Add(new TransactionEntry(
                    DateTimeOffset.Now,
                    TransactionType.DataEvent,
                    0,
                    new Dictionary<DenominationKey, int>()));
            })
            .AddTo(_disposables);

        // Status Update Events
        _device.StatusUpdateEvents
            .Subscribe(e =>
            {
                if (e.Status == (int)UposCashChangerStatusUpdateCode.Jam)
                {
                    _history.Add(new TransactionEntry(
                        DateTimeOffset.Now,
                        TransactionType.HardwareError,
                        0,
                        new Dictionary<DenominationKey, int>()));
                }
                else if (e.Status == (int)UposCashChangerStatusUpdateCode.Ok)
                {
                    _history.Add(new TransactionEntry(
                        DateTimeOffset.Now,
                        TransactionType.ErrorRecovery,
                        0,
                        new Dictionary<DenominationKey, int>()));
                }
            })
            .AddTo(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}

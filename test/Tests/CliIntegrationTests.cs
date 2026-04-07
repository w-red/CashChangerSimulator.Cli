using Moq;
using Shouldly;
using Spectre.Console.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Virtual;
using Spectre.Console;
using R3;
using Xunit;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CLI アプリケーションの統合動作を検証するためのテストクラス。</summary>
[Collection("SequentialTests")]
public class CliIntegrationTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TestConsole _console;
    private readonly Mock<ILineReader> _mockReader;
    private readonly ICashChangerDevice _device;
    private readonly ICliCommandDispatcher _dispatcher;

    public CliIntegrationTests()
    {
        var services = new ServiceCollection();
        _console = new TestConsole();
        _mockReader = new Mock<ILineReader>();

        // CLI の DI 設定を流用
        CliDIContainer.ConfigureServices(services, Array.Empty<string>());
        
        // テスト用に一部のサービスを差し替え
        services.AddSingleton<IAnsiConsole>(_console);
        services.AddSingleton<ILineReader>(_mockReader.Object);
        
        // ローカライズのモック
        var mockLocalizer = new Mock<IStringLocalizer>();
        mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, $"[[{s}]]"));
        mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string s, object[] args) => 
            new LocalizedString(s, args != null && args.Length > 0 ? $"[[{s}]] {string.Join(" ", args)}" : $"[[{s}]]"));
        services.AddSingleton<IStringLocalizer>(mockLocalizer.Object);

        // 前もって CliSessionOptions を登録し、IsAsync を設定しておく
        var options = new CliSessionOptions { IsAsync = true };
        services.AddSingleton(options);

        _serviceProvider = services.BuildServiceProvider();
        _device = _serviceProvider.GetRequiredService<ICashChangerDevice>();
        _dispatcher = _serviceProvider.GetRequiredService<ICliCommandDispatcher>();

        // デバイスの初期化
        _device.OpenAsync().GetAwaiter().GetResult();
        _device.ClaimAsync(1000).GetAwaiter().GetResult();
        _device.EnableAsync().GetAwaiter().GetResult();

        // 在庫をクリア
        var inventory = _serviceProvider.GetRequiredService<Inventory>();
        inventory.Clear();
    }

    public void Dispose()
    {
        _device.CloseAsync().GetAwaiter().GetResult();
    }

    /// <summary>入金の開始から確定、終了までの一連のフローをテストし、在庫が増加することを確認します。</summary>
    [Fact]
    public async Task DepositCycleShouldIncreaseInventoryCorrectly()
    {
        // Arrange
        var inventory = _serviceProvider.GetRequiredService<Inventory>();
        var tenYen = new DenominationKey(10, CurrencyCashType.Coin, "JPY");
        inventory.GetCount(tenYen).ShouldBe(0);

        // Act: 入金開始
        await _dispatcher.DispatchAsync("deposit 100");
        
        // 入金中に計数をシミュレート
        if (_device is VirtualCashChangerDevice simulator)
        {
            simulator.DepositController.TrackDeposit(new DenominationKey(100, CurrencyCashType.Bill, "JPY"));
            
            // Assert: 入金中の状態を確認
            simulator.DepositController.DepositStatus.ShouldBe(DeviceDepositStatus.Counting);

            // Act: 入金確定 (Fix)
            await _dispatcher.DispatchAsync("fix-deposit");
            simulator.DepositController.DepositStatus.ShouldBe(DeviceDepositStatus.Counting);

            // Act: 入金終了 (End)
            await _dispatcher.DispatchAsync("end-deposit");
            simulator.DepositController.DepositStatus.ShouldBe(DeviceDepositStatus.End);
        }

        // Assert: 在庫が反映されているか
        inventory.CalculateTotal("JPY").ShouldBeGreaterThan(0);
        _console.Output.ShouldContain("messages.deposit_started");
        _console.Output.ShouldContain("messages.deposit_fixed");
        _console.Output.ShouldContain("messages.end_deposit_completed");
    }

    /// <summary>金額指定なしの出金時、在高不足の場合に適切なエラーメッセージが表示されることを確認します。</summary>
    [Fact]
    public async Task DispenseWithInsufficientFundsShouldShowError()
    {
        // Arrange: 在庫が空の状態
        var inventory = _serviceProvider.GetRequiredService<Inventory>();
        inventory.Clear();

        // Act: 1000円出金を試行
        await _dispatcher.DispatchAsync("dispense 1000");

        // Assert: エラーメッセージが含まれていること
        _console.Output.ShouldContain("[[messages.error_label]]");
    }

    /// <summary>対話型シェル経由で一連の入金操作を行い、最終的に終了することをテストします。</summary>
    [Fact]
    public async Task ShellDepositWorkflowShouldCompleteAndExit()
    {
        // Arrange
        var inputs = new Queue<string>(new[] { "deposit 500", "fix-deposit", "end-deposit", "exit" });
        _mockReader.Setup(r => r.Read(It.IsAny<string>())).Returns(() => inputs.Dequeue());
        
        // ConfirmExit() 用の入力を TestConsole に仕込む
        _console.Input.PushKey(ConsoleKey.Y);
        _console.Input.PushKey(ConsoleKey.Enter);
        
        // シェルを構成
        var shell = new CliInteractiveShell(
            _dispatcher,
            _device,
            _console,
            _serviceProvider.GetRequiredService<IStringLocalizer>(),
            _serviceProvider.GetRequiredService<CliSessionOptions>(),
            _mockReader.Object);

        // Act
        await shell.RunAsync();

        // Assert
        _console.Output.ShouldContain("messages.deposit_fixed");
        _console.Output.ShouldContain("messages.end_deposit_completed");
        
        if (_device is VirtualCashChangerDevice simulator)
        {
            simulator.DepositController.DepositStatus.ShouldBe(DeviceDepositStatus.End);
        }
    }

    /// <summary>入金中に返却（Repay）を指示し、在庫が変化しないことを確認します。</summary>
    [Fact]
    public async Task RepayDepositShouldReturnCashCorrectly()
    {
        // Arrange
        var inventory = _serviceProvider.GetRequiredService<Inventory>();
        inventory.Clear();

        // Act: 入金開始
        await _dispatcher.DispatchAsync("deposit 1000");
        
        // 計数をシミュレート
        if (_device is VirtualCashChangerDevice simulator)
        {
            simulator.DepositController.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"));
            simulator.DepositController.DepositAmount.ShouldBe(1000);

            // Act: 返却指示
            await _device.RepayDepositAsync();

            // Assert
            simulator.DepositController.DepositStatus.ShouldBe(DeviceDepositStatus.End);
            inventory.CalculateTotal("JPY").ShouldBe(0);
        }
    }

    /// <summary>入金中に一時停止と再開を試行します。</summary>
    [Fact]
    public async Task PauseResumeDepositShouldWorkCorrectly()
    {
        // Act: 入金開始
        await _dispatcher.DispatchAsync("deposit 1000");
        
        if (_device is VirtualCashChangerDevice simulator)
        {
            simulator.DepositController.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"));
            
            // Act: 一時停止
            await _device.PauseDepositAsync(DeviceDepositPause.Pause);
            simulator.DepositController.IsPaused.ShouldBeTrue();

            // Act: 再開
            await _device.PauseDepositAsync(DeviceDepositPause.Resume);
            simulator.DepositController.IsPaused.ShouldBeFalse();
        }
    }

    /// <summary>デバイスがジャム状態のときに入金を開始しようとしてエラーになることを確認します。</summary>
    [Fact]
    public async Task BeginDepositWhenJammedShouldShowError()
    {
        // Arrange: ジャム状態にする
        var hardwareStatus = _serviceProvider.GetRequiredService<HardwareStatusManager>();
        hardwareStatus.SetJammed(true);

        // Act: 入金開始試行
        await _dispatcher.DispatchAsync("deposit 1000");

        // Assert: エラーメッセージが表示されること
        _console.Output.ShouldContain("[[messages.error_label]]");
        
        if (_device is VirtualCashChangerDevice simulator)
        {
             simulator.DepositController.DepositStatus.ShouldNotBe(DeviceDepositStatus.Counting);
        }
    }
}

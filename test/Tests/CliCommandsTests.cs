using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using CashChangerSimulator.Cli.Services;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Cli.Tests;

/// <summary>CliCommands の各種コマンド実行機能を検証するためのテストクラス。</summary>
[Collection("SequentialTests")]
public class CliCommandsTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CliCommands _commands;
    private readonly ICashChangerDevice _device;

    public CliCommandsTests()
    {
        // Cleanup singleton or static state if any
        var builder = Host.CreateApplicationBuilder();
        CliDIContainer.ConfigureServices(builder.Services, ["--verbose"]);

        // テストごとに一意な Mutex 名を使用するように ICashChangerDevice を再登録（並列実行やクリーンアップ遅延による競合回避）
        var testMutexName = $"Local\\CashChangerSimulator_Test_{Guid.NewGuid()}";
        builder.Services.AddSingleton<ICashChangerDevice>(sp =>
        {
            var factory = (VirtualCashChangerDeviceFactory)sp.GetRequiredService<ICashChangerDeviceFactory>();
            var manager = sp.GetRequiredService<CashChangerManager>();
            var statusManager = sp.GetRequiredService<HardwareStatusManager>();
            var inventory = sp.GetRequiredService<Inventory>();
            
            var device = (VirtualCashChangerDevice)factory.Create(manager, inventory, statusManager, testMutexName);
            device.OpenAsync().GetAwaiter().GetResult();
            return device;
        });

        _serviceProvider = builder.Services.BuildServiceProvider();

        // Ensure static service provider is initialized for internal resolutions
        SimulatorServices.Provider = new TestServiceProvider(_serviceProvider);

        _commands = _serviceProvider.GetRequiredService<CliCommands>();
        _device = _serviceProvider.GetRequiredService<ICashChangerDevice>();
    }

    public void Dispose()
    {
        _device.CloseAsync().GetAwaiter().GetResult();
        _device.Dispose();
        SimulatorServices.Provider = null;
        GC.SuppressFinalize(this);
    }

    private sealed class TestServiceProvider(IServiceProvider provider) : ISimulatorServiceProvider
    {
        public T Resolve<T>() where T : class => provider.GetRequiredService<T>();
    }

    /// <summary>Open コマンドが正常に実行されることを検証します。</summary>
    [Fact]
    public void OpenShouldCompleteSuccessfully()
    {
        // Arrange
        // (Constructor already opens)

        // Act & Assert
        Should.NotThrow(() => _commands.Open());
    }

    /// <summary>Status コマンドが例外を投げずに実行されることを検証します。</summary>
    [Fact]
    public void StatusShouldCompleteSuccessfully()
    {
        // Act & Assert
        Should.NotThrow(() => _commands.Status());
    }

    /// <summary>Claim コマンドが正常に実行されることを検証します。</summary>
    [Fact]
    public void ClaimShouldCompleteSuccessfully()
    {
        // Act
        _commands.Claim(1000);

        // Assert
        if (_device is VirtualCashChangerDevice simulator)
        {
            // 自分が占有しても IsClaimedByAnother (他者が占有) は False のまま
            simulator.HardwareStatus.IsClaimedByAnother.Value.ShouldBeFalse();
        }
    }

    /// <summary>Release コマンドが正常に実行されることを検証します。</summary>
    [Fact]
    public void ReleaseShouldCompleteSuccessfully()
    {
        // Arrange
        _commands.Claim(1000);

        // Act
        _commands.Release();

        // Assert
        if (_device is VirtualCashChangerDevice simulator)
        {
            simulator.HardwareStatus.IsClaimedByAnother.Value.ShouldBeFalse();
        }
    }

    /// <summary>Enable コマンドが正常に実行されることを検証します。</summary>
    [Fact]
    public void EnableShouldCompleteSuccessfully()
    {
        // Act & Assert
        Should.NotThrow(() => _commands.Claim(1000), "Claim should not throw and should succeed.");
        
        // Wait for state cleanup if any (FireAndForget behavior in some systems)
        Thread.Sleep(100);

        Should.NotThrow(() => _commands.Enable(), "Enable should not throw and should succeed.");

        // Assert
        if (_device is VirtualCashChangerDevice simulator)
        {
            // 同期的にセットされるはずだが、念のため SpinUntil を継続
            SpinWait.SpinUntil(() => simulator.HardwareStatus.DeviceEnabled.Value, 2000).ShouldBeTrue("DeviceEnabled property should be true after Enable command.");
        }
    }

    /// <summary>Disable コマンドが正常に実行されることを検証します。</summary>
    [Fact]
    public void DisableShouldCompleteSuccessfully()
    {
        // Arrange
        Should.NotThrow(() => _commands.Claim(1000));
        Should.NotThrow(() => _commands.Enable());
        
        if (_device is VirtualCashChangerDevice simulator)
        {
             SpinWait.SpinUntil(() => simulator.HardwareStatus.DeviceEnabled.Value, 2000).ShouldBeTrue();
        }

        // Act
        Should.NotThrow(() => _commands.Disable());

        // Assert
        if (_device is VirtualCashChangerDevice simulator2)
        {
            SpinWait.SpinUntil(() => !simulator2.HardwareStatus.DeviceEnabled.Value, 2000).ShouldBeTrue("DeviceEnabled property should be false after Disable command.");
        }
    }

    /// <summary>Deposit コマンドが正常に実行されることを検証します。</summary>
    [Fact]
    public void DepositShouldCompleteSuccessfully()
    {
        // Arrange
        _commands.Claim(1000);
        _commands.Enable();

        // Act & Assert
        Should.NotThrow(() => _commands.Deposit(1000));
    }

    /// <summary>EndDeposit コマンドが正常に実行されることを検証します。</summary>
    [Fact]
    public void EndDepositShouldCompleteSuccessfully()
    {
        // Arrange
        _commands.Claim(1000);
        _commands.Enable();
        _commands.Deposit(1000);

        // Act & Assert
        Should.NotThrow(() => _commands.EndDeposit());
    }

    /// <summary>Dispense コマンドが正常に実行されることを検証します。</summary>
    [Fact]
    public void DispenseShouldCompleteSuccessfully()
    {
        // Arrange
        _commands.Claim(1000);
        _commands.Enable();

        // Act & Assert
        Should.NotThrow(() => _commands.Dispense(500));
    }


    /// <summary>BoxRemoved コマンドがシミュレートされたデバイスの状態を更新することを検証します。</summary>
    [Fact]
    public void BoxRemovedShouldCompleteSuccessfully()
    {
        // Act
        // Act
        _commands.SetBoxRemoved(true);

        // Assert
        if (_device is VirtualCashChangerDevice simulator)
        {
            simulator.HardwareStatus.IsCollectionBoxRemoved.Value.ShouldBeTrue();
        }
    }

}

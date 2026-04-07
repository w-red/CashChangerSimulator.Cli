using Moq;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Cli.Services;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Virtual;
using R3;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Cli.Tests;

/// <summary>CliDeviceService のデバイス制御機能を検証するためのテストクラス。</summary>
[Collection("SequentialTests")]
public class CliDeviceServiceTests
{
    private readonly Mock<ICashChangerDevice> _mockDevice;
    private readonly Mock<IAnsiConsole> _mockConsole;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly CliDeviceService _service;

    public CliDeviceServiceTests()
    {
        _mockDevice = new Mock<ICashChangerDevice>();
        _mockConsole = new Mock<IAnsiConsole>();
        _mockLocalizer = new Mock<IStringLocalizer>();

        // Mock localizer to return keys
        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, s));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string s, object[] args) => new LocalizedString(s, s));

        // Mock State property
        _mockDevice.Setup(d => d.State).Returns(new ReactiveProperty<DeviceControlState>(DeviceControlState.Idle));

        _service = new CliDeviceService(
            _mockDevice.Object,
            _mockConsole.Object,
            _mockLocalizer.Object);
    }

    /// <summary>Open 操作が OpenAsync を呼び出し、成功メッセージを表示することを検証します。</summary>
    [Fact]
    public void OpenShouldInvokeOpenAsync()
    {
        // Arrange
        _mockDevice.Setup(d => d.OpenAsync()).Returns(Task.CompletedTask);

        // Act
        _service.Open();

        // Assert
        _mockDevice.Verify(c => c.OpenAsync(), Times.Once);
        _mockLocalizer.Verify(l => l["messages.device_opened"], Times.Once);
    }

    /// <summary>Claim 操作が ClaimAsync を呼び出し、成功メッセージを表示することを検証します。</summary>
    [Fact]
    public void ClaimShouldInvokeClaimAsync()
    {
        // Arrange
        _mockDevice.Setup(d => d.ClaimAsync(It.IsAny<int>())).Returns(Task.CompletedTask);

        // Act
        _service.Claim(1000);

        // Assert
        _mockDevice.Verify(c => c.ClaimAsync(1000), Times.Once);
        _mockLocalizer.Verify(l => l["messages.device_claimed"], Times.Once);
    }

    /// <summary>Enable 操作が EnableAsync を呼び出し、成功メッセージを表示することを検証します。</summary>
    [Fact]
    public void EnableShouldInvokeEnableAsync()
    {
        // Arrange
        _mockDevice.Setup(d => d.EnableAsync()).Returns(Task.CompletedTask);

        // Act
        _service.Enable();

        // Assert
        _mockDevice.Verify(c => c.EnableAsync(), Times.Once);
        _mockLocalizer.Verify(l => l["messages.device_enabled"], Times.Once);
    }

    /// <summary>SetCollectionBoxRemoved 操作が VirtualCashChangerDevice のステータスを更新することを検証します。</summary>
    [Fact]
    public void SetCollectionBoxRemovedShouldUpdateVirtualDeviceStatus()
    {
        // Arrange
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var hardwareStatusManager = new HardwareStatusManager();
        var manager = new CashChangerManager(inventory, history, null);

        // VirtualCashChangerDevice はファクトリ経由で生成
        var factory = new VirtualCashChangerDeviceFactory(new ConfigurationProvider(), NullLoggerFactory.Instance);
        var simulator = factory.Create(manager, inventory, hardwareStatusManager);

        // SimulatorServices のスタティック解決に備えて登録（テスト環境用）
        var provider = new ServiceCollection()
            .AddSingleton(hardwareStatusManager)
            .BuildServiceProvider();
        SimulatorServices.Provider = new TestServiceProvider(provider);

        var service = new CliDeviceService(simulator, _mockConsole.Object, _mockLocalizer.Object);

        // Act
        service.SetCollectionBoxRemoved(true);

        // Assert
        hardwareStatusManager.IsCollectionBoxRemoved.Value.ShouldBeTrue();
        _mockLocalizer.Verify(l => l["messages.box_removed"], Times.Once);
    }

    /// <summary>理論テスト: 各メソッドで例外が発生した場合に HandleException が呼ばれることを検証します。</summary>
    [Theory]
    [InlineData("Open")]
    [InlineData("Claim")]
    [InlineData("Enable")]
    [InlineData("Disable")]
    [InlineData("Release")]
    [InlineData("Close")]
    public void MethodsShouldHandleExceptions(string methodName)
    {
        // Arrange
        var exception = new DeviceException("Error", DeviceErrorCode.Failure);
        switch (methodName)
        {
            case "Open": _mockDevice.Setup(c => c.OpenAsync()).ThrowsAsync(exception); break;
            case "Claim": _mockDevice.Setup(c => c.ClaimAsync(It.IsAny<int>())).ThrowsAsync(exception); break;
            case "Enable": _mockDevice.Setup(c => c.EnableAsync()).ThrowsAsync(exception); break;
            case "Disable": _mockDevice.Setup(c => c.DisableAsync()).ThrowsAsync(exception); break;
            case "Release": _mockDevice.Setup(c => c.ReleaseAsync()).ThrowsAsync(exception); break;
            case "Close": _mockDevice.Setup(c => c.CloseAsync()).ThrowsAsync(exception); break;
        }

        // Act
        switch (methodName)
        {
            case "Open": _service.Open(); break;
            case "Claim": _service.Claim(1000); break;
            case "Enable": _service.Enable(); break;
            case "Disable": _service.Disable(); break;
            case "Release": _service.Release(); break;
            case "Close": _service.Close(); break;
        }

        // Assert
        _mockLocalizer.Verify(l => l["messages.error_label"], Times.Once);
    }

    private class TestServiceProvider(IServiceProvider provider) : ISimulatorServiceProvider
    {
        public T Resolve<T>() where T : class => provider.GetRequiredService<T>();
    }
}

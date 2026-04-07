using Moq;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using R3;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CliCashService の現金操作機能を検証するためのテストクラス。</summary>
public class CliCashServiceTests
{
    private readonly Mock<ICashChangerDevice> _mockDevice;
    private readonly Mock<IAnsiConsole> _mockConsole;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly Mock<ICurrencyMetadataProvider> _mockMetadata;
    private readonly Inventory _inventory;
    private readonly CliCashService _service;

    public CliCashServiceTests()
    {
        _mockDevice = new Mock<ICashChangerDevice>();
        _mockConsole = new Mock<IAnsiConsole>();
        _mockLocalizer = new Mock<IStringLocalizer>();
        _mockMetadata = new Mock<ICurrencyMetadataProvider>();
        _inventory = new Inventory();
        
        // Setup metadata with R3 properties
        _mockMetadata.Setup(m => m.CurrencyCode).Returns("JPY");
        
        var prefix = new ReactiveProperty<string>("¥");
        _mockMetadata.Setup(m => m.SymbolPrefix).Returns(prefix);
        
        var suffix = new ReactiveProperty<string>("");
        _mockMetadata.Setup(m => m.SymbolSuffix).Returns(suffix);
        
        var denominations = new List<DenominationKey>
        {
            new DenominationKey(1000, CurrencyCashType.Bill, "JPY"),
            new DenominationKey(500, CurrencyCashType.Bill, "JPY")
        };
        _mockMetadata.Setup(m => m.SupportedDenominations).Returns(denominations);

        // Mock localizer to return keys
        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, s));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string s, object[] args) => new LocalizedString(s, s));

        // Mock State property
        _mockDevice.Setup(d => d.State).Returns(new ReactiveProperty<DeviceControlState>(DeviceControlState.Idle));

        _service = new CliCashService(
            _mockDevice.Object,
            _inventory,
            _mockMetadata.Object,
            new CliSessionOptions(),
            _mockConsole.Object,
            _mockLocalizer.Object);
    }

    /// <summary>ReadCashCounts 操作でコンソールに表が出力されることを検証します。</summary>
    [Fact]
    public void ReadCashCountsShouldWriteTableToConsole()
    {
        // Arrange
        _mockDevice.Setup(c => c.ReadInventoryAsync()).ReturnsAsync(_inventory);

        // Act
        _service.ReadCashCounts();

        // Assert
        _mockDevice.Verify(c => c.ReadInventoryAsync(), Times.Once);
        _mockConsole.Verify(c => c.Write(It.IsAny<Table>()), Times.Once);
    }

    /// <summary>Deposit 操作が BeginDepositAsync を呼び出すことを検証します。</summary>
    [Fact]
    public void DepositShouldInvokeBeginDeposit()
    {
        // Arrange
        _mockDevice.Setup(d => d.BeginDepositAsync()).Returns(Task.CompletedTask);
        _mockDevice.Setup(d => d.FixDepositAsync()).Returns(Task.CompletedTask);
        _mockDevice.Setup(d => d.EndDepositAsync(It.IsAny<DepositAction>())).Returns(Task.CompletedTask);

        // Act
        _service.Deposit(1000);

        // Assert
        _mockDevice.Verify(c => c.BeginDepositAsync(), Times.Once);
    }

    /// <summary>EndDeposit 操作が EndDepositAsync を呼び出すことを検証します。</summary>
    [Fact]
    public void EndDepositShouldInvokeEndDeposit()
    {
        // Arrange
        _mockDevice.Setup(d => d.EndDepositAsync(It.IsAny<DepositAction>())).Returns(Task.CompletedTask);

        // Act
        _service.EndDeposit();

        // Assert
        _mockDevice.Verify(c => c.EndDepositAsync(DepositAction.NoChange), Times.Once);
    }

    /// <summary>Dispense 操作が DispenseChangeAsync を呼び出すことを検証します。</summary>
    [Fact]
    public void DispenseShouldInvokeDispenseChange()
    {
        // Arrange
        _mockDevice.Setup(d => d.DispenseChangeAsync(It.IsAny<int>())).Returns(Task.CompletedTask);

        // Act
        _service.Dispense(1000);

        // Assert
        _mockDevice.Verify(c => c.DispenseChangeAsync(1000), Times.Once);
    }

    /// <summary>理論テスト: 各メソッドで例外が発生した場合に HandleException が呼ばれることを検証します。</summary>
    [Theory]
    [InlineData("ReadCashCounts")]
    [InlineData("Deposit")]
    [InlineData("FixDeposit")]
    [InlineData("EndDeposit")]
    [InlineData("Dispense")]
    public void MethodsShouldHandleExceptions(string methodName)
    {
        // Arrange
        var exception = new DeviceException("Error", DeviceErrorCode.Failure);
        switch (methodName)
        {
            case "ReadCashCounts": _mockDevice.Setup(c => c.ReadInventoryAsync()).ThrowsAsync(exception); break;
            case "Deposit": _mockDevice.Setup(c => c.BeginDepositAsync()).ThrowsAsync(exception); break;
            case "FixDeposit": _mockDevice.Setup(c => c.FixDepositAsync()).ThrowsAsync(exception); break;
            case "EndDeposit": _mockDevice.Setup(d => d.EndDepositAsync(It.IsAny<DepositAction>())).ThrowsAsync(exception); break;
            case "Dispense": _mockDevice.Setup(d => d.DispenseChangeAsync(It.IsAny<int>())).ThrowsAsync(exception); break;
        }

        // Act
        switch (methodName)
        {
            case "ReadCashCounts": _service.ReadCashCounts(); break;
            case "Deposit": _service.Deposit(1000); break;
            case "FixDeposit": _service.FixDeposit(); break;
            case "EndDeposit": _service.EndDeposit(); break;
            case "Dispense": _service.Dispense(1000); break;
        }

        // Assert
        _mockLocalizer.Verify(l => l["messages.error_label"], Times.Once);
    }
}

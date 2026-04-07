using Moq;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Cli.Services;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using R3;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Cli.Tests;

/// <summary>CliViewService の表示機能を検証するためのテストクラス。</summary>
public class CliViewServiceTests
{
    private readonly Mock<ICashChangerDevice> _mockDevice;
    private readonly Mock<IAnsiConsole> _mockConsole;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly Mock<ICurrencyMetadataProvider> _mockMetadata;
    private readonly CliViewService _service;

    public CliViewServiceTests()
    {
        _mockDevice = new Mock<ICashChangerDevice>();
        _mockConsole = new Mock<IAnsiConsole>();
        _mockLocalizer = new Mock<IStringLocalizer>();
        _mockMetadata = new Mock<ICurrencyMetadataProvider>();

        // Setup metadata with R3 properties
        _mockMetadata.Setup(m => m.SupportedDenominations).Returns([new DenominationKey(1000, CurrencyCashType.Bill, "JPY")]);
        _mockMetadata.Setup(m => m.SymbolPrefix).Returns(new ReactiveProperty<string>("¥").ToReadOnlyReactiveProperty());
        _mockMetadata.Setup(m => m.SymbolSuffix).Returns(new ReactiveProperty<string>("").ToReadOnlyReactiveProperty());

        // Mock localizer to return keys
        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, s));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string s, object[] args) => new LocalizedString(s, s));

        // Mock State property
        _mockDevice.Setup(d => d.State).Returns(new ReactiveProperty<DeviceControlState>(DeviceControlState.Idle).ToReadOnlyReactiveProperty());

        _service = new CliViewService(
            _mockDevice.Object,
            new Inventory(),
            _mockMetadata.Object,
            new TransactionHistory(),
            new Mock<IHistoryExportService>().Object,
            _mockConsole.Object,
            _mockLocalizer.Object);
    }

    /// <summary>ShowStatus 操作が例外を投げずに実行されることを検証します。</summary>
    [Fact]
    public void StatusShouldWriteToConsole()
    {
        // Act & Assert
        Should.NotThrow(() => _service.Status());
    }

}

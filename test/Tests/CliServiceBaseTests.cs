using Moq;
using Spectre.Console;
using Spectre.Console.Testing;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CliServiceBase の共通エラー処理およびレポート機能を検証するためのテストクラス。</summary>
public class CliServiceBaseTests
{
    private readonly TestConsole _console;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly TestService _service;

    public CliServiceBaseTests()
    {
        _console = new TestConsole();
        _mockLocalizer = new Mock<IStringLocalizer>();
        _service = new TestService(_console, _mockLocalizer.Object);

        // Mock localizer to return keys
        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, s));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string s, object[] args) => new LocalizedString(s, s));
    }

    /// <summary>ReportSuccess が適切なマークアップを出力することを検証します。</summary>
    [Fact]
    public void ReportSuccessShouldMarkupToConsole()
    {
        // Act
        _service.DoReportSuccess("Success Message");

        // Assert
        _console.Output.ShouldContain("Success Message");
    }

    /// <summary>DeviceException 例外が適切に処理されることを検証します。</summary>
    [Fact]
    public void HandleDeviceExceptionShouldReportDetailedError()
    {
        // Arrange
        var ex = new DeviceException("Device Error", DeviceErrorCode.Failure);

        // Act
        _service.DoHandleException(ex);

        // Assert
        _mockLocalizer.Verify(l => l["messages.error_label"], Times.Once);
        _console.Output.ShouldContain("Device Error");
    }

    /// <summary>一般的な例外が適切にエラー報告されることを検証します。</summary>
    [Fact]
    public void HandleGenericExceptionShouldReportError()
    {
        // Arrange
        var ex = new Exception("Generic Error");

        // Act
        _service.DoHandleException(ex);

        // Assert
        _mockLocalizer.Verify(l => l["messages.error_label"], Times.Once);
        _console.Output.ShouldContain("Generic Error");
    }

    private class TestService(IAnsiConsole console, IStringLocalizer localizer) : CliServiceBase(console, localizer)
    {
        public void DoReportSuccess(string message) => ReportSuccess(message);
        public void DoHandleException(Exception ex) => HandleException(ex);
    }
}

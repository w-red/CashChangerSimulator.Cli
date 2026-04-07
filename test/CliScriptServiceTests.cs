using Moq;
using Spectre.Console;
using Spectre.Console.Testing;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Cli.Services;
using CashChangerSimulator.Device.Virtual.Services;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Cli.Tests;

/// <summary>CliScriptService のスクリプト実行機能を検証するためのテストクラス。</summary>
public class CliScriptServiceTests
{
    private readonly Mock<IScriptExecutionService> _mockScriptService;
    private readonly TestConsole _console;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly CliScriptService _service;

    public CliScriptServiceTests()
    {
        _mockScriptService = new Mock<IScriptExecutionService>();
        _console = new TestConsole();
        _mockLocalizer = new Mock<IStringLocalizer>();

        // Mock localizer to return keys
        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, s));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string s, object[] args) => new LocalizedString(s, s));

        _service = new CliScriptService(_mockScriptService.Object, _console, _mockLocalizer.Object);
    }

    /// <summary>存在しないファイルパスでのスクリプト実行が適切にエラー報告されることを検証します。</summary>
    [Fact]
    public async Task RunScriptWithNonExistentFileShouldReportError()
    {
        // Act
        await _service.RunScriptAsync("non-existent-file.json");

        // Assert
        _console.Output.ShouldContain("messages.file_not_found");
        _mockScriptService.Verify(s => s.ExecuteScriptAsync(It.IsAny<string>(), It.IsAny<Action<string>>()), Times.Never);
    }
}

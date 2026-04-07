using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.Virtual;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CliDIContainer の依存関係注入設定を検証するためのテストクラス。</summary>
public class CliDIContainerTests
{
    private readonly IServiceProvider _serviceProvider;

    public CliDIContainerTests()
    {
        var builder = Host.CreateApplicationBuilder();
        CliDIContainer.ConfigureServices(builder.Services, ["--verbose"]);
        _serviceProvider = builder.Services.BuildServiceProvider();
    }

    /// <summary>ICashChangerDevice が VirtualMockDevice として解決されることを検証します。</summary>
    [Fact]
    public void ICashChangerDeviceShouldResolveToVirtualMockDevice()
    {
        // Act
        var device = _serviceProvider.GetService<ICashChangerDevice>();

        // Assert
        device.ShouldNotBeNull();
        device.ShouldBeOfType<VirtualCashChangerDevice>();
    }

    /// <summary>主要な CLI サービスが正常に解決されることを検証します。</summary>
    [Theory]
    [InlineData(typeof(CliDeviceService))]
    [InlineData(typeof(CliCashService))]
    [InlineData(typeof(CliViewService))]
    [InlineData(typeof(CliCommands))]
    [InlineData(typeof(CliInteractiveShell))]
    public void ServicesShouldBeResolvable(Type serviceType)
    {
        // Act
        var service = _serviceProvider.GetService(serviceType);

        // Assert
        service.ShouldNotBeNull();
    }
}

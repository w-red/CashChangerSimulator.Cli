using Microsoft.Extensions.DependencyInjection;
using CashChangerSimulator.Cli.Services;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.Virtual;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Cli.Tests;

/// <summary>CliDIContainer の依存関係注入設定を検証するためのテストクラス。</summary>
public class CliDIContainerTests
{
    private readonly IServiceProvider _serviceProvider;

    public CliDIContainerTests()
    {
        var services = new ServiceCollection();
        CliDIContainer.ConfigureServices(services, ["--verbose"]);
        _serviceProvider = services.BuildServiceProvider();
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

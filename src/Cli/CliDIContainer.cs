using Spectre.Console;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Virtual;
using CashChangerSimulator.Device.Virtual.Services;
using CashChangerSimulator.Cli.Services;
using CashChangerSimulator.Cli.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Cli;

public static class CliDIContainer
{
    private static IServiceProvider _serviceProvider = null!;

    public static void Initialize(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services, args);
        _serviceProvider = services.BuildServiceProvider();

        SimulatorServices.Provider = new CliResolverServiceProvider(_serviceProvider);
    }

    public static void ConfigureServices(IServiceCollection services, string[] args)
    {
        // Logging
        var isVerbose = args.Contains("--verbose");
        LogProvider.Initialize(new LoggingSettings
        {
            LogLevel = "Information",
            EnableConsole = isVerbose,
            EnableFile = true,
            LogFileName = "cli-app.log"
        });

        // Providers
        services.AddSingleton<ConfigurationProvider, CliConfigurationProvider>();
        services.AddSingleton<ICurrencyMetadataProvider, CurrencyMetadataProvider>();
        services.AddSingleton<MonitorsProvider>();
        services.AddSingleton<OverallStatusAggregatorProvider>();
        services.AddSingleton<INotifyService, CliNotifyService>();
        services.AddSingleton<CliSessionOptions>();
        services.AddSingleton(AnsiConsole.Console);

        // Logging provider for DI
        services.AddLogging(builder => 
        {
            builder.AddConsole();
            builder.SetMinimumLevel(isVerbose ? LogLevel.Debug : LogLevel.Information);
        });

        // Localization
        services.AddSingleton<IStringLocalizerFactory, TomlStringLocalizerFactory>();
        services.AddTransient(sp => sp.GetRequiredService<IStringLocalizerFactory>().Create(typeof(CliCommands)));

        // Core Services
        services.AddSingleton<Inventory>();
        services.AddSingleton<TransactionHistory>();
        services.AddSingleton<HistoryPersistenceService>();
        services.AddSingleton<CashChangerManager>();
        services.AddSingleton<HardwareStatusManager>();
        services.AddSingleton<DiagnosticController>();
        services.AddSingleton<IHistoryExportService, CsvHistoryExportService>();

        // Controllers from Device (must register after device is set up)
        services.AddSingleton(sp => ((VirtualCashChangerDevice)sp.GetRequiredService<ICashChangerDevice>()).DepositController);
        services.AddSingleton(sp => ((VirtualCashChangerDevice)sp.GetRequiredService<ICashChangerDevice>()).DispenseController);

        // Simulator / Devices
        services.AddSingleton<ICashChangerDeviceFactory, VirtualCashChangerDeviceFactory>();
        services.AddSingleton<ICashChangerDevice>(sp =>
        {
            var factory = sp.GetRequiredService<ICashChangerDeviceFactory>();
            var manager = sp.GetRequiredService<CashChangerManager>();
            var inventory = sp.GetRequiredService<Inventory>();
            var statusManager = sp.GetRequiredService<HardwareStatusManager>();

            var device = factory.Create(manager, inventory, statusManager);

            // CLI では起動時にオープンを試行
            device.OpenAsync().GetAwaiter().GetResult();
            return device;
        });

        services.AddSingleton<IDeviceSimulator, CliHardwareSimulator>();
        services.AddSingleton<CliEventHistoryObserver>();
        services.AddSingleton<IScriptExecutionService, ScriptExecutionService>();
        
        // CLI Services
        services.AddSingleton<CliDeviceService>();
        services.AddSingleton<CliCashService>();
        services.AddSingleton<CliConfigService>();
        services.AddSingleton<CliViewService>();
        services.AddSingleton<CliScriptService>();

        services.AddSingleton<ILineReader, ReadLineReader>();
        services.AddSingleton<ICliCommandDispatcher, CliCommandDispatcher>();
        services.AddSingleton<CliInteractiveShell>();
        services.AddTransient<CliCommands>();
    }

    public static void PostInitialize(IServiceProvider provider, string[] args)
    {
        _serviceProvider = provider;

        // Override Configuration from args
        var configProvider = provider.GetRequiredService<ConfigurationProvider>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--currency" && i + 1 < args.Length)
            {
                var currencyCode = args[++i].ToUpperInvariant();
                configProvider.Config.System.CurrencyCode = currencyCode;
            }
        }

        // Initialize Inventory
        var inventory = provider.GetRequiredService<Inventory>();
        var state = ConfigurationLoader.LoadInventoryState();
        if (state?.Counts != null && state.Counts.Count > 0)
        {
            inventory.LoadFromDictionary(state.Counts);
        }
        else
        {
            foreach (var currencyEntry in configProvider.Config.Inventory)
            {
                var currencyCode = currencyEntry.Key;
                foreach (var item in currencyEntry.Value.Denominations)
                {
                    if (DenominationKey.TryParse(item.Key, currencyCode, out var key) && key != null)
                    {
                        inventory.SetCount(key, item.Value.InitialCount);
                    }
                }
            }
        }

        // Initialize History
        var history = provider.GetRequiredService<TransactionHistory>();
        var persistence = provider.GetRequiredService<HistoryPersistenceService>();
        
        var historyState = persistence.Load();
        if (historyState.Entries.Count > 0)
        {
            history.FromState(historyState);
        }

        // Start Auto-Save
        persistence.StartAutoSave();

        // Initialize Observers
        provider.GetRequiredService<CliEventHistoryObserver>();
    }

    public static IServiceProvider ServiceProvider => _serviceProvider;

    public static T Resolve<T>() where T : notnull => _serviceProvider.GetRequiredService<T>();
}

internal sealed class CliResolverServiceProvider(IServiceProvider provider) : ISimulatorServiceProvider, IServiceProvider
{
    public T Resolve<T>() where T : class => provider.GetRequiredService<T>();

    public object? GetService(Type serviceType) => provider.GetService(serviceType);
}

public class CliNotifyService(IAnsiConsole console) : INotifyService
{
    private readonly IAnsiConsole _console = console;

    public void ShowWarning(string message, string title = "Warning")
    {
        _console.MarkupLine($"[yellow][[{title}]] {message}[/]");
    }

    public void ShowError(string message, string title = "Error")
    {
        _console.MarkupLine($"[red][[{title}]] {message}[/]");
    }

    public void ShowInfo(string message, string title = "Info")
    {
        _console.MarkupLine($"[blue][[{title}]] {message}[/]");
    }
}

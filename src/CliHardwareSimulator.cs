using CashChangerSimulator.Device.Virtual;
using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.Cli;

/// <summary>CLI環境専用の HardwareSimulator 派生クラス。</summary>
public class CliHardwareSimulator : HardwareSimulator
{
    public CliHardwareSimulator(ConfigurationProvider configProvider) : base(configProvider)
    {
    }
}

using System.Reflection;
using System.Collections;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.UI.Cli.Services;

public class CliConfigService(
    ConfigurationProvider configProvider,
    IAnsiConsole console,
    IStringLocalizer localizer) : CliServiceBase(console, localizer)
{
    private readonly ConfigurationProvider _configProvider = configProvider;

    /// <summary>設定項目を一覧表示します。</summary>
    public virtual void List()
    {
        _console.Write(new Rule($"[cyan]{_L["messages.config_header"]}[/]").LeftJustified());
        PrintObject(_configProvider.Config, "");
    }

    private void PrintObject(object obj, string prefix)
    {
        var type = obj.GetType();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = prop.GetValue(obj);
            var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

            if (value == null)
            {
                _console.MarkupLine($"{key} = [grey]null[/]");
            }
            else if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || prop.PropertyType.IsEnum)
            {
                _console.MarkupLine($"{key} = [green]{value}[/]");
            }
            else if (value is IDictionary)
            {
                _console.MarkupLine($"{key} = [grey](Dictionary)[/]");
            }
            else
            {
                PrintObject(value, key);
            }
        }
    }

    /// <summary>特定の設定値を取得します。</summary>
    /// <param name="key">設定キー（例: "Device.Name"）。</param>
    public virtual void Get(string key)
    {
        var result = GetPropertyByPath(_configProvider.Config, key);
        if (result.Success)
        {
            _console.MarkupLine($"{key} = [green]{result.Value}[/]");
        }
        else
        {
            _console.MarkupLine(_L["messages.invalid_config_key", key]);
        }
    }

    /// <summary>設定値を更新します。</summary>
    /// <param name="key">設定キー。</param>
    /// <param name="value">新しい値。</param>
    public virtual void Set(string key, string value)
    {
        var result = SetPropertyByPath(_configProvider.Config, key, value);
        if (result)
        {
            ReportSuccess(_L["messages.config_updated", key, value]);
        }
        else
        {
            _console.MarkupLine(_L["messages.invalid_config_key", key]);
        }
    }

    /// <summary>現在の設定値を TOML ファイルに保存します。</summary>
    public virtual void Save()
    {
        try
        {
            ConfigurationLoader.Save(_configProvider.Config);
            ReportSuccess(_L["messages.config_saved"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    /// <summary>設定をファイルから再読み込みします。</summary>
    public virtual void Reload()
    {
        try
        {
            _configProvider.Reload();
            ReportSuccess(_L["messages.config_reloaded"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static (bool Success, object? Value) GetPropertyByPath(object obj, string path)
    {
        var parts = path.Split('.');
        object? current = obj;

        foreach (var part in parts)
        {
            if (current == null) return (false, null);
            var prop = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null) return (false, null);
            current = prop.GetValue(current);
        }

        return (true, current);
    }

    private static bool SetPropertyByPath(object obj, string path, string value)
    {
        var parts = path.Split('.');
        object? current = obj;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (current == null) return false;
            var prop = current.GetType().GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null) return false;
            current = prop.GetValue(current);
        }

        if (current == null) return false;
        var finalProp = current.GetType().GetProperty(parts.Last(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (finalProp == null || !finalProp.CanWrite) return false;

        try
        {
            object? convertedValue = finalProp.PropertyType.IsEnum
                ? Enum.Parse(finalProp.PropertyType, value, true)
                : Convert.ChangeType(value, finalProp.PropertyType);
            finalProp.SetValue(current, convertedValue);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

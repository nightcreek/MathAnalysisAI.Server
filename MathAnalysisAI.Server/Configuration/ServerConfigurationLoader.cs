using MathAnalysisAI.Server.Services.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MathAnalysisAI.Server.Configuration;

public static class ServerConfigurationLoader
{
    public static void Configure(
        ConfigurationManager configuration,
        IHostEnvironment environment,
        string[]? args = null)
    {
        configuration.Sources.Clear();

        configuration
            .SetBasePath(environment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddInMemoryCollection(LoadServerEnvValues(environment.ContentRootPath))
            .AddEnvironmentVariables();

        if (args is { Length: > 0 })
        {
            configuration.AddCommandLine(args);
        }
    }

    public static IConfigurationRoot Build(string contentRootPath, string environmentName)
    {
        return new ConfigurationBuilder()
            .SetBasePath(contentRootPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddInMemoryCollection(LoadServerEnvValues(contentRootPath))
            .AddEnvironmentVariables()
            .Build();
    }

    private static Dictionary<string, string?> LoadServerEnvValues(string contentRootPath)
    {
        var envFilePath = Path.Combine(contentRootPath, "server.env");
        if (!File.Exists(envFilePath))
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        var encryptionKey = ConfigEncryptionService.LoadEncryptionKey();
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadAllLines(envFilePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();
            if (value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal))
            {
                value = value[1..^1];
            }

            if (ConfigEncryptionService.IsEncrypted(value))
            {
                if (encryptionKey == null)
                {
                    throw new InvalidOperationException(
                        $"Config key '{key}' uses an encrypted value (ENC:) but MATHANALYSIS_ENCRYPTION_KEY is not set.");
                }

                value = ConfigEncryptionService.Decrypt(value[4..], encryptionKey);
            }

            data[key] = value;
        }

        return data;
    }
}

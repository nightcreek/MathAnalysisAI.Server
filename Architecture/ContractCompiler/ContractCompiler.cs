using System.Text.Encodings.Web;
using System.Text.Json;

public sealed class ContractCompiler
{
    public ContractCompilationResult Compile(string repoRoot, string contractsPath, string outputPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(contractsPath));
        var warnings = new List<string>();

        if (!document.RootElement.TryGetProperty("modules", out var modules))
        {
            warnings.Add("Contracts/api.contract.json does not contain a top-level 'modules' object.");
        }

        var compiled = BuildCompiledCatalog(document.RootElement, modules);
        warnings.AddRange(CollectModuleFileWarnings(repoRoot, contractsPath, compiled.Modules));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var js = "window.CompiledApiContracts = " + JsonSerializer.Serialize(compiled, CreateJsonOptions()) + ";" + Environment.NewLine;
        File.WriteAllText(outputPath, js);

        return new ContractCompilationResult(compiled.Modules.Count, warnings);
    }

    private static CompiledApiContracts BuildCompiledCatalog(JsonElement root, JsonElement modules)
    {
        var version = root.TryGetProperty("version", out var versionProp)
            ? versionProp.GetString() ?? "1.0.0"
            : "1.0.0";

        var compiledModules = new SortedDictionary<string, object>(StringComparer.Ordinal);
        if (modules.ValueKind == JsonValueKind.Object)
        {
            foreach (var module in modules.EnumerateObject())
            {
                var moduleObject = JsonSerializer.Deserialize<object>(module.Value.GetRawText(), CreateJsonOptions()) ?? new object();
                compiledModules[module.Name] = moduleObject;
            }
        }

        return new CompiledApiContracts(version, compiledModules);
    }

    private static IEnumerable<string> CollectModuleFileWarnings(string repoRoot, string contractsPath, IReadOnlyDictionary<string, object> compiledModules)
    {
        var warnings = new List<string>();
        var contractsDirectory = Path.GetDirectoryName(contractsPath) ?? Path.Combine(repoRoot, "Contracts");
        var moduleDirectory = Path.Combine(contractsDirectory, "modules");
        if (!Directory.Exists(moduleDirectory))
        {
            return warnings;
        }

        foreach (var file in Directory.GetFiles(moduleDirectory, "*.contract.json", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (!document.RootElement.TryGetProperty("moduleName", out var moduleNameProperty))
            {
                warnings.Add($"Module contract file '{Path.GetRelativePath(repoRoot, file)}' is missing 'moduleName'.");
                continue;
            }

            var moduleName = moduleNameProperty.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                warnings.Add($"Module contract file '{Path.GetRelativePath(repoRoot, file)}' has an empty 'moduleName'.");
                continue;
            }

            if (!compiledModules.ContainsKey(moduleName))
            {
                warnings.Add($"Module contract file '{Path.GetRelativePath(repoRoot, file)}' references '{moduleName}', but that module is missing from Contracts/api.contract.json.");
            }
        }

        return warnings;
    }

    private static JsonSerializerOptions CreateJsonOptions()
        => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };
}

public sealed record CompiledApiContracts(string Version, IReadOnlyDictionary<string, object> Modules);

public sealed record ContractCompilationResult(int ModuleCount, IReadOnlyList<string> Warnings);

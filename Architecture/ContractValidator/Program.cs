using System.Text.Json;

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var contractsPath = args.Length > 0 ? Path.GetFullPath(args[0], Directory.GetCurrentDirectory()) : Path.Combine(repoRoot, "Contracts", "api.contract.json");
var outputPath = args.Length > 1 ? Path.GetFullPath(args[1], Directory.GetCurrentDirectory()) : Path.Combine(repoRoot, "contract-mismatches.json");

var validator = new ContractValidator();
var result = validator.Validate(repoRoot, contractsPath);

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(outputPath, json);

Console.WriteLine($"Contract registry: {contractsPath}");
Console.WriteLine($"Contract mismatches report: {outputPath}");
Console.WriteLine($"Controller endpoints discovered: {result.ControllerEndpoints.Count}");
Console.WriteLine($"Contract endpoints discovered: {result.ContractEndpoints.Count}");
Console.WriteLine($"Mismatches found: {result.Mismatches.Count}");

return 0;

static string FindRepoRoot(string startDirectory)
{
    var current = new DirectoryInfo(startDirectory);
    while (current != null)
    {
        if (Directory.Exists(Path.Combine(current.FullName, "MathAnalysisAI.Server"))
            && Directory.Exists(Path.Combine(current.FullName, "Contracts")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    return startDirectory;
}

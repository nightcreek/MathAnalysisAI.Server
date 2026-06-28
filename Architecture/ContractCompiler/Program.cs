using System.Text.Json;

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var contractsPath = args.Length > 0 ? Path.GetFullPath(args[0], Directory.GetCurrentDirectory()) : Path.Combine(repoRoot, "Contracts", "api.contract.json");
var outputPath = args.Length > 1 ? Path.GetFullPath(args[1], Directory.GetCurrentDirectory()) : Path.Combine(repoRoot, "MathAnalysisAI.Server", "wwwroot", "js", "api", "compiled-contracts.js");

var compiler = new ContractCompiler();
var result = compiler.Compile(repoRoot, contractsPath, outputPath);

Console.WriteLine($"Contract registry: {contractsPath}");
Console.WriteLine($"Compiled frontend contracts: {outputPath}");
Console.WriteLine($"Modules compiled: {result.ModuleCount}");
Console.WriteLine($"Warnings: {result.Warnings.Count}");

foreach (var warning in result.Warnings)
{
    Console.WriteLine($"[warning] {warning}");
}

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

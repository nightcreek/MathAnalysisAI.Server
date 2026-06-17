using MathAnalysisAI.Server.Services.Security;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: dotnet run -- <plaintext>");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  Encrypts a plaintext value for use in server.env.");
    Console.Error.WriteLine("  Set MATHANALYSIS_ENCRYPTION_KEY environment variable (64 hex chars) first.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  Example:");
    Console.Error.WriteLine("    export MATHANALYSIS_ENCRYPTION_KEY=$(openssl rand -hex 32)");
    Console.Error.WriteLine("    dotnet run -- \"my-secret-password\"");
    return 1;
}

var plaintext = args[0];
var key = ConfigEncryptionService.LoadEncryptionKey();

if (key == null)
{
    Console.Error.WriteLine("Error: MATHANALYSIS_ENCRYPTION_KEY environment variable is not set.");
    Console.Error.WriteLine("Generate one with: openssl rand -hex 32");
    return 1;
}

try
{
    var encrypted = ConfigEncryptionService.EncryptToConfigValue(plaintext, key);
    Console.WriteLine(encrypted);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

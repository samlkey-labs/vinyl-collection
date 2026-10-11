// Shared by the tools in this folder (linked into each project).
// Resolves Cosmos DB settings from, in order of precedence:
//   1. a value passed on the command line
//   2. an environment variable (Cosmos__ConnectionString / Cosmos__DatabaseName)
//   3. the repo's .env file (the same one docker-compose uses), found by searching
//      upwards from the current directory

internal static class CosmosSettings
{
    public const string ConnectionStringKey = "Cosmos__ConnectionString";
    public const string DatabaseNameKey = "Cosmos__DatabaseName";

    private static readonly Lazy<Dictionary<string, string>> DotEnv = new(LoadDotEnv);

    public static string? ConnectionString(string? fromArgs = null) => Resolve(ConnectionStringKey, fromArgs);

    public static string DatabaseName(string? fromArgs = null) => Resolve(DatabaseNameKey, fromArgs) ?? "VinylCollection";

    private static string? Resolve(string key, string? fromArgs)
    {
        if (!string.IsNullOrWhiteSpace(fromArgs))
            return fromArgs;

        var fromEnvironment = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        return DotEnv.Value.TryGetValue(key, out var fromFile) && !string.IsNullOrWhiteSpace(fromFile)
            ? fromFile
            : null;
    }

    private static Dictionary<string, string> LoadDotEnv()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, ".env");
            if (!File.Exists(path))
                continue;

            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                // Split on the first '=' only: connection strings contain '=' themselves
                var separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();
                if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
                    value = value[1..^1];

                values[key] = value;
            }

            break; // nearest .env wins
        }

        return values;
    }
}

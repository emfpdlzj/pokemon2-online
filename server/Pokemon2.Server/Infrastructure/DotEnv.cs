namespace Pokemon2.Server.Infrastructure;

public static class DotEnv
{
    public static void LoadForServer()
    {
        var root = FindRepositoryRoot(Directory.GetCurrentDirectory());
        if (root is null) return;

        var loadedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Load(Path.Combine(root, ".env"), loadedKeys);
        Load(Path.Combine(root, "server", ".env"), loadedKeys);
    }

    private static void Load(string path, HashSet<string> loadedKeys)
    {
        if (!File.Exists(path)) return;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var separator = line.IndexOf('=');
            if (separator <= 0) continue;

            var key = line[..separator].Trim();
            var value = Unquote(line[(separator + 1)..].Trim());
            if (Environment.GetEnvironmentVariable(key) is not null && !loadedKeys.Contains(key)) continue;

            Environment.SetEnvironmentVariable(key, value);
            loadedKeys.Add(key);
        }
    }

    private static string? FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")) &&
                Directory.Exists(Path.Combine(directory.FullName, "client")) &&
                Directory.Exists(Path.Combine(directory.FullName, "server")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }
}

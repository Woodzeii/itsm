namespace itsm.Infrastructure;

public static class DotEnvLoader
{
    public static void LoadIfPresent()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
            {
                var envPath = Path.Combine(directory.FullName, ".env");
                if (!File.Exists(envPath))
                    continue;

                DotNetEnv.Env.Load(envPath);
                return;
            }
        }
    }
}
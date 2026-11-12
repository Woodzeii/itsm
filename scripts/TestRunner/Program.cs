using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

internal static class Program
{
    private static readonly Regex DurationSegmentRegex = new(
        @"(\d+(?:\.\d+)?)(ms|s|m|h)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static async Task<int> Main()
    {
        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is null)
        {
            Console.Error.WriteLine("Не удалось найти корень репозитория с папками backend и scripts.");
            return 1;
        }

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== ITSM: запуск тестов ===");
            Console.WriteLine("1. Запустить .NET-тесты");
            Console.WriteLine("2. Запустить скрипт из scripts");
            Console.WriteLine("0. Выход");

            var selection = ReadMenuSelection("Выберите пункт: ", 0, 2);
            if (selection is null || selection == 0)
            {
                return 0;
            }

            if (selection == 1)
            {
                await RunDotNetTests(repositoryRoot);
            }
            else
            {
                await SelectAndRunScript(repositoryRoot);
            }
        }
    }

    private static string? FindRepositoryRoot()
    {
        var startingPoints = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var startingPoint in startingPoints)
        {
            var directory = new DirectoryInfo(startingPoint);
            while (directory is not null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "backend"))
                    && Directory.Exists(Path.Combine(directory.FullName, "scripts")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static async Task RunDotNetTests(string repositoryRoot)
    {
        var projectPath = Path.Combine(repositoryRoot, "backend", "itsm.Tests", "itsm.Tests.csproj");
        if (!File.Exists(projectPath))
        {
            Console.Error.WriteLine($"Проект тестов не найден: {projectPath}");
            return;
        }

        await RunProcess(
            "dotnet",
            ["test", projectPath],
            repositoryRoot);
    }

    private static async Task SelectAndRunScript(string repositoryRoot)
    {
        var scriptsDirectory = Path.Combine(repositoryRoot, "scripts");
        var scripts = Directory
            .EnumerateFiles(scriptsDirectory, "*.js", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (scripts.Length == 0)
        {
            Console.WriteLine($"В папке {scriptsDirectory} нет JavaScript-скриптов.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Скрипты:");
        for (var index = 0; index < scripts.Length; index++)
        {
            Console.WriteLine($"{index + 1}. {Path.GetFileName(scripts[index])}");
        }
        Console.WriteLine("0. Назад");

        var selection = ReadMenuSelection("Выберите скрипт: ", 0, scripts.Length);
        if (selection is null || selection == 0)
        {
            return;
        }

        var selectedScript = scripts[selection.Value - 1];
        var selectedScriptName = Path.GetFileName(selectedScript);
        if (selectedScriptName.Equals("auth-smoke-e2e.js", StringComparison.OrdinalIgnoreCase))
        {
            var authSmokeEnvironment = ReadAuthSmokeSettings();
            await RunNpmScript("test:auth-smoke", scriptsDirectory, authSmokeEnvironment);
            return;
        }

        if (selectedScriptName.Equals("tenant-isolation-api.js", StringComparison.OrdinalIgnoreCase))
        {
            var tenantIsolationEnvironment = ReadTenantIsolationSettings();
            await RunNpmScript("test:tenant-isolation", scriptsDirectory, tenantIsolationEnvironment);
        if (Path.GetFileName(selectedScript).Equals("auth-smoke-e2e.js", StringComparison.OrdinalIgnoreCase))
        {
            var authSmokeEnvironment = ReadAuthSmokeSettings();
            if (OperatingSystem.IsWindows())
            {
                var commandProcessor = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                await RunProcess(
                    commandProcessor,
                    ["/d", "/c", "npm.cmd run test:auth-smoke"],
                    scriptsDirectory,
                    authSmokeEnvironment);
            }
            else
            {
                await RunProcess(
                    "npm",
                    ["run", "test:auth-smoke"],
                    scriptsDirectory,
                    authSmokeEnvironment);
            }

            return;
        }

        IReadOnlyDictionary<string, string>? environment = null;
        if (Path.GetFileName(selectedScript).Contains("load", StringComparison.OrdinalIgnoreCase))
        {
            environment = ReadLoadTestSettings();
        }

        await RunProcess(
            "k6",
            ["run", selectedScript],
            repositoryRoot,
            environment);
    }

    private static Dictionary<string, string> ReadTenantIsolationSettings()
    {
        Console.WriteLine();
        Console.WriteLine("Настройка API-тестов изоляции тенантов");

        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TENANT_A_ADMIN_USERNAME"] = ReadRequiredValue(
                "Логин администратора тенанта A",
                Environment.GetEnvironmentVariable("TENANT_A_ADMIN_USERNAME")),
            ["TENANT_A_ADMIN_PASSWORD"] = ReadRequiredValue(
                "Пароль администратора тенанта A (ввод скрыт)",
                Environment.GetEnvironmentVariable("TENANT_A_ADMIN_PASSWORD"),
                hideInput: true),
            ["TENANT_A_TOTP_SECRET"] = ReadRequiredValue(
                "TOTP-секрет администратора тенанта A (ввод скрыт)",
                Environment.GetEnvironmentVariable("TENANT_A_TOTP_SECRET"),
                hideInput: true),
            ["TENANT_B_ADMIN_USERNAME"] = ReadRequiredValue(
                "Логин администратора тенанта B",
                Environment.GetEnvironmentVariable("TENANT_B_ADMIN_USERNAME")),
            ["TENANT_B_ADMIN_PASSWORD"] = ReadRequiredValue(
                "Пароль администратора тенанта B (ввод скрыт)",
                Environment.GetEnvironmentVariable("TENANT_B_ADMIN_PASSWORD"),
                hideInput: true),
            ["TENANT_B_TOTP_SECRET"] = ReadRequiredValue(
                "TOTP-секрет администратора тенанта B (ввод скрыт)",
                Environment.GetEnvironmentVariable("TENANT_B_TOTP_SECRET"),
                hideInput: true),
        };

        settings["TENANT_ISOLATION_API_URL"] = ReadValue(
            "Базовый URL API",
            Environment.GetEnvironmentVariable("TENANT_ISOLATION_API_URL") ?? "http://localhost:5062/api",
            value => Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));

        return settings;
    }

    private static async Task RunNpmScript(
        string npmScript,
        string scriptsDirectory,
        IReadOnlyDictionary<string, string> environment)
    {
        if (OperatingSystem.IsWindows())
        {
            var commandProcessor = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            await RunProcess(
                commandProcessor,
                ["/d", "/c", $"npm.cmd run {npmScript}"],
                scriptsDirectory,
                environment);
        }
        else
        {
            await RunProcess(
                "npm",
                ["run", npmScript],
                scriptsDirectory,
                environment);
        }
    }

    private static Dictionary<string, string> ReadAuthSmokeSettings()
    {
        Console.WriteLine();
        Console.WriteLine("Настройка smoke E2E авторизации");

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["E2E_ADMIN_USERNAME"] = ReadRequiredValue(
                "Логин тестового администратора",
                Environment.GetEnvironmentVariable("E2E_ADMIN_USERNAME")),
            ["E2E_ADMIN_PASSWORD"] = ReadRequiredValue(
                "Пароль тестового администратора (ввод скрыт)",
                Environment.GetEnvironmentVariable("E2E_ADMIN_PASSWORD"),
                hideInput: true),
            ["E2E_TOTP_SECRET"] = ReadRequiredValue(
                "Секрет TOTP в формате Base32 (ввод скрыт)",
                Environment.GetEnvironmentVariable("E2E_TOTP_SECRET"),
                hideInput: true),
        };
    }

    private static string ReadRequiredValue(string prompt, string? defaultValue, bool hideInput = false)
    {
        while (true)
        {
            var defaultHint = string.IsNullOrEmpty(defaultValue)
                ? string.Empty
                : hideInput ? " (Enter — использовать значение из окружения)" : $" [{defaultValue}]";
            Console.Write($"{prompt}{defaultHint}: ");

            var input = hideInput ? ReadSecret() : Console.ReadLine() ?? string.Empty;
            var value = string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            Console.WriteLine("Значение обязательно. Введите его или задайте соответствующую переменную окружения.");
        }
    }

    private static Dictionary<string, string> ReadLoadTestSettings()
    {
        Console.WriteLine();
        Console.WriteLine("Настройка нагрузочного теста");

        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BASE_URL"] = ReadValue(
                "Адрес API",
                "http://localhost:5062",
                value => Uri.TryCreate(value, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)),
            ["VUS"] = ReadValue(
                "Количество виртуальных пользователей",
                "5",
                value => int.TryParse(value, out var vus) && vus > 0),
            ["DURATION"] = ReadValue(
                "Длительность (например, 2m или 30s)",
                "2m",
                IsValidDuration),
            ["RESPONSE_LIMIT_MS"] = ReadValue(
                "Порог времени ответа в миллисекундах",
                "2000",
                value => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var limit)
                    && double.IsFinite(limit)
                    && limit > 0),
        };

        var dictionaryId = ReadValue(
            "ID справочника (пусто — проверять все)",
            "",
            value => string.IsNullOrEmpty(value) || int.TryParse(value, out var id) && id > 0);
        if (!string.IsNullOrEmpty(dictionaryId))
        {
            settings["DICTIONARY_ID"] = dictionaryId;
        }

        Console.Write("JWT-токен (Enter — взять LOAD_TEST_JWT_TOKEN из backend/.env): ");
        var token = ReadSecret();
        if (!string.IsNullOrWhiteSpace(token))
        {
            settings["JWT_TOKEN"] = token;
        }

        return settings;
    }

    private static string ReadValue(string prompt, string defaultValue, Func<string, bool> isValid)
    {
        while (true)
        {
            Console.Write($"{prompt} [{defaultValue}]: ");
            var input = Console.ReadLine();
            if (input is null)
            {
                return defaultValue;
            }

            var value = string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
            if (isValid(value))
            {
                return value;
            }

            Console.WriteLine("Некорректное значение, попробуйте ещё раз.");
        }
    }

    private static string ReadSecret()
    {
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var characters = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return new string(characters.ToArray());
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (characters.Count > 0)
                {
                    characters.RemoveAt(characters.Count - 1);
                }
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                characters.Add(key.KeyChar);
            }
        }
    }

    private static int? ReadMenuSelection(string prompt, int minimum, int maximum)
    {
        while (true)
        {
            Console.Write(prompt);
            var input = Console.ReadLine();
            if (input is null)
            {
                return null;
            }

            if (int.TryParse(input, out var selection) && selection >= minimum && selection <= maximum)
            {
                return selection;
            }

            Console.WriteLine($"Введите число от {minimum} до {maximum}.");
        }
    }

    private static bool IsValidDuration(string value)
    {
        var matches = DurationSegmentRegex.Matches(value);
        if (matches.Count == 0 || string.Concat(matches.Select(match => match.Value)) != value)
        {
            return false;
        }

        return matches.Any(match =>
            double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)
            && double.IsFinite(amount)
            && amount > 0);
    }

    private static async Task RunProcess(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                Console.Error.WriteLine($"Не удалось запустить команду {fileName}.");
                return;
            }

            await process.WaitForExitAsync();
            Console.WriteLine(process.ExitCode == 0
                ? "Тест завершился успешно."
                : $"Тест завершился с кодом {process.ExitCode}.");
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            Console.Error.WriteLine($"Команда «{fileName}» не найдена или не может быть запущена: {exception.Message}");
        }
    }
}

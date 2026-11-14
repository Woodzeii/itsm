using System.Security.Cryptography;

namespace itsm.Domain.Constants;

/// <summary>
/// Генерация публичного номера заявки в формате XXX-000-00000.
/// </summary>
public static class TicketNumberGenerator
{
    public const string Mask = "XXX-000-00000";

    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int MaxAttempts = 32;

    /// <summary>Генерирует новый публичный номер заявки.</summary>
    public static string Generate() =>
        $"{RandomString(3, Letters)}-{RandomDigits(3)}-{RandomDigits(5)}";

    /// <summary>
    /// Генерирует номер, которого нет среди уже занятых.
    /// Бросает исключение, если за отведённое число попыток уникальный код не найден.
    /// </summary>
    public static string GenerateUnique(Func<string, bool> isTaken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = Generate();
            if (!isTaken(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            "Не удалось сгенерировать уникальный номер заявки. Повторите попытку.");
    }

    /// <summary>Проверяет соответствие строки маске XXX-000-00000.</summary>
    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != Mask.Length)
            return false;

        for (var index = 0; index < Mask.Length; index++)
        {
            var character = value[index];
            var expectsDigit = Mask[index] == '0';
            var expectsLetter = Mask[index] == 'X';

            if (expectsDigit && !char.IsAsciiDigit(character))
                return false;
            if (expectsLetter && (character < 'A' || character > 'Z'))
                return false;
        }

        return true;
    }

    private static string RandomString(int length, string alphabet)
    {
        var buffer = new char[length];
        for (var index = 0; index < length; index++)
            buffer[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

        return new string(buffer);
    }

    private static string RandomDigits(int length)
    {
        var buffer = new char[length];
        for (var index = 0; index < length; index++)
            buffer[index] = (char)('0' + RandomNumberGenerator.GetInt32(10));

        return new string(buffer);
    }
}

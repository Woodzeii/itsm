using System.Security.Cryptography;
using System.Text;

namespace itsm.Infrastructure.Security;

public static class TokenGenerator
{
    public static (string Plain, string Hash) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var plain = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return (plain, Hash(plain));
    }

    public static string Hash(string plain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        return Convert.ToHexString(bytes);
    }
}
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace DevManager.Core;

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        return $"argon2id${Convert.ToBase64String(salt)}${Convert.ToBase64String(Compute(password, salt))}";
    }

    public static bool Verify(string password, string stored)
    {
        var p = stored.Split('$');
        if (p.Length != 3 || p[0] != "argon2id") return false;
        var salt = Convert.FromBase64String(p[1]);
        var expected = Convert.FromBase64String(p[2]);
        return CryptographicOperations.FixedTimeEquals(Compute(password, salt), expected);
    }

    // 정책 위반 시 사유를, 통과하면 null을 반환한다.
    public static string? Validate(string password, string loginId)
    {
        if (password.Length < 10) return "비밀번호는 10자 이상이어야 합니다";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "비밀번호는 영문과 숫자를 모두 포함해야 합니다";
        if (string.Equals(password, loginId, StringComparison.OrdinalIgnoreCase)) return "비밀번호는 아이디와 같을 수 없습니다";
        return null;
    }

    private static byte[] Compute(string password, byte[] salt)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt, DegreeOfParallelism = 2, MemorySize = 19456, Iterations = 2
        };
        return argon.GetBytes(32);
    }
}

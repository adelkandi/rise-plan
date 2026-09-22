using System.Globalization;

namespace backend.Data;

public static class ConnectionStringNormalizer
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length != 2)
            throw new InvalidOperationException("The PostgreSQL URI must include a username and password.");

        return string.Join(";",
            $"Host={uri.Host}",
            $"Port={uri.Port.ToString(CultureInfo.InvariantCulture)}",
            $"Database={uri.AbsolutePath.TrimStart('/')}",
            $"Username={Uri.UnescapeDataString(userInfo[0])}",
            $"Password={Uri.UnescapeDataString(userInfo[1])}",
            "SSL Mode=Require");
    }
}

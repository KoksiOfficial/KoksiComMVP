namespace KoksiCom.Api.Guest;

public static class DisplayName
{
    public const int MinLength = 2;
    public const int MaxLength = 10;

    public static bool IsValid(string? name)
    {
        if (name is null || name.Length < MinLength || name.Length > MaxLength)
        {
            return false;
        }

        foreach (var c in name)
        {
            if (c is not ((>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9')))
            {
                return false;
            }
        }

        return true;
    }
}

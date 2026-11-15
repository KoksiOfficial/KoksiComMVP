namespace KoksiCom.Api.Rooms;

public static class RoomPassword
{
    public const int MinLength = 6;
    public const int MaxLength = 10;

    public static bool IsValid(string? password)
    {
        if (password is null || password.Length < MinLength || password.Length > MaxLength)
        {
            return false;
        }

        var hasDigit = false;
        var hasUpper = false;
        var hasLower = false;
        foreach (var c in password)
        {
            if (char.IsDigit(c))
            {
                hasDigit = true;
            }
            else if (char.IsAsciiLetterUpper(c))
            {
                hasUpper = true;
            }
            else if (char.IsAsciiLetterLower(c))
            {
                hasLower = true;
            }
        }

        return hasDigit && hasUpper && hasLower;
    }
}

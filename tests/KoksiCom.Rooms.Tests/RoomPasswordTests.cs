using KoksiCom.Api.Rooms;

namespace KoksiCom.Rooms.Tests;

public class RoomPasswordTests
{
    [Theory]
    [InlineData("Abcd12")]
    [InlineData("A1b2c3")]
    [InlineData("Abcdef1xyZ")]
    public void Accepts_valid_passwords(string password)
    {
        Assert.True(RoomPassword.IsValid(password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ab1")]
    [InlineData("Abcdefghij1")]
    [InlineData("abcdef1")]
    [InlineData("ABCDEF1")]
    [InlineData("Abcdef")]
    public void Rejects_invalid_passwords(string? password)
    {
        Assert.False(RoomPassword.IsValid(password));
    }
}

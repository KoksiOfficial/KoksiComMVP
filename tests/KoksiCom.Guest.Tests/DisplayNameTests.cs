using KoksiCom.Api.Guest;

namespace KoksiCom.Guest.Tests;

public class DisplayNameTests
{
    [Theory]
    [InlineData("Ab")]
    [InlineData("Ada2")]
    [InlineData("Abcdefghij")]
    public void Accepts_letters_and_digits_length_2_to_10(string name)
    {
        Assert.True(DisplayName.IsValid(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("Abcdefghijk")]
    [InlineData("Ada 2")]
    [InlineData("Ada-2")]
    [InlineData("Ada_2")]
    public void Rejects_invalid_names(string? name)
    {
        Assert.False(DisplayName.IsValid(name));
    }
}

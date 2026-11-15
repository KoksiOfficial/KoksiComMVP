using KoksiCom.Api.Guest;

namespace KoksiCom.Guest.Tests;

public class GuestRegistryTests
{
    [Fact]
    public void TryAdd_rejects_duplicate_while_active()
    {
        var registry = new GuestRegistry();
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddHours(12);

        Assert.True(registry.TryAdd("Ada2", expires, now));
        Assert.False(registry.TryAdd("Ada2", expires, now));
        Assert.True(registry.ContainsActive("Ada2", now));
    }

    [Fact]
    public void TryAdd_allows_name_after_expiry()
    {
        var registry = new GuestRegistry();
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddHours(12);

        Assert.True(registry.TryAdd("Ada2", expires, now));
        Assert.True(registry.TryAdd("Ada2", expires.AddHours(12), now.AddHours(12).AddSeconds(1)));
    }

    [Fact]
    public void Remove_frees_name()
    {
        var registry = new GuestRegistry();
        var now = DateTimeOffset.UtcNow;

        Assert.True(registry.TryAdd("Ada2", now.AddHours(12), now));
        Assert.True(registry.Remove("Ada2"));
        Assert.False(registry.ContainsActive("Ada2", now));
        Assert.True(registry.TryAdd("Ada2", now.AddHours(12), now));
    }

    [Fact]
    public void TryAdd_does_not_write_files()
    {
        var temp = Directory.CreateTempSubdirectory("koksicom-guest-");
        try
        {
            var before = Directory.GetFileSystemEntries(temp.FullName);
            var registry = new GuestRegistry();
            var now = DateTimeOffset.UtcNow;
            registry.TryAdd("Ada2", now.AddHours(12), now);
            var after = Directory.GetFileSystemEntries(temp.FullName);
            Assert.Equal(before, after);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }
}

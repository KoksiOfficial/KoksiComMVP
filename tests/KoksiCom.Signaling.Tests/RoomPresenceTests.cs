using KoksiCom.Api.Signaling;

namespace KoksiCom.Signaling.Tests;

public class RoomPresenceTests
{
    [Fact]
    public void Attach_defaults_mic_off_headphones_on_share_off()
    {
        var presence = new RoomPresence();

        var info = presence.Attach("Ada2", "c1");

        Assert.Equal("Ada2", info.Name);
        Assert.False(info.Mic);
        Assert.True(info.Headphones);
        Assert.False(info.Share);
        Assert.Equal("c1", presence.ConnectionId("Ada2"));
    }

    [Fact]
    public void UpdateState_returns_copy_and_roster_includes_attached_members_only()
    {
        var presence = new RoomPresence();
        presence.Attach("Ada2", "c1");
        presence.Attach("Bob3", "c2");

        var updated = presence.UpdateState("Ada2", true, false, true);
        var roster = presence.Roster(["Ada2", "Bob3", "Cal4"]);

        Assert.NotNull(updated);
        Assert.True(updated!.Mic);
        Assert.False(updated.Headphones);
        Assert.True(updated.Share);
        Assert.Equal(2, roster.Count);
        Assert.DoesNotContain(roster, peer => peer.Name == "Cal4");
    }

    [Fact]
    public void Detach_drops_connection_lookup()
    {
        var presence = new RoomPresence();
        presence.Attach("Ada2", "c1");
        presence.Detach("Ada2");

        Assert.Null(presence.ConnectionId("Ada2"));
        Assert.Empty(presence.Roster(["Ada2"]));
    }
}

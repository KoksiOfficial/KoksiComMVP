using KoksiCom.Api.Rooms;

namespace KoksiCom.Rooms.Tests;

public class RoomRegistryTests
{
    [Fact]
    public void Create_then_second_guest_joins()
    {
        var rooms = new RoomRegistry();

        Assert.Equal(CreateResult.Created, rooms.TryCreate("Ada2", "Lab1", "Abcd12"));
        Assert.Equal(JoinResult.Joined, rooms.TryJoin("Bob3", "Lab1", "Abcd12"));
        var list = rooms.List();
        Assert.Single(list);
        Assert.Equal("Lab1", list[0].Name);
        Assert.Equal(2, list[0].Users);
    }

    [Fact]
    public void Leave_last_member_removes_room()
    {
        var rooms = new RoomRegistry();
        rooms.TryCreate("Ada2", "Lab1", "Abcd12");

        Assert.Equal(LeaveResult.Left, rooms.TryLeave("Ada2"));
        Assert.Empty(rooms.List());
        Assert.Equal(CreateResult.Created, rooms.TryCreate("Ada2", "Lab1", "Abcd12"));
    }

    [Fact]
    public void Creator_can_leave_while_others_remain()
    {
        var rooms = new RoomRegistry();
        rooms.TryCreate("Ada2", "Lab1", "Abcd12");
        rooms.TryJoin("Bob3", "Lab1", "Abcd12");

        Assert.Equal(LeaveResult.Left, rooms.TryLeave("Ada2"));
        var list = rooms.List();
        Assert.Single(list);
        Assert.Equal(1, list[0].Users);
        Assert.Equal("Lab1", rooms.RoomForGuest("Bob3"));
        Assert.Null(rooms.RoomForGuest("Ada2"));
    }

    [Fact]
    public void TryCreate_does_not_write_files()
    {
        var temp = Directory.CreateTempSubdirectory("koksicom-rooms-");
        try
        {
            var before = Directory.GetFileSystemEntries(temp.FullName);
            var rooms = new RoomRegistry();
            rooms.TryCreate("Ada2", "Lab1", "Abcd12");
            var after = Directory.GetFileSystemEntries(temp.FullName);
            Assert.Equal(before, after);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }
}

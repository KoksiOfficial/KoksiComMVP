using System.Text.Json;
using KoksiCom.Api.Guest;
using KoksiCom.Api.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace KoksiCom.Api.Signaling;

[Authorize]
public sealed class RoomHub(
    GuestRegistry guests,
    RoomRegistry rooms,
    RoomPresence presence,
    TimeProvider time) : Hub
{
    public async Task Enter()
    {
        var name = GuestName();
        if (name is null || !guests.ContainsActive(name, time.GetUtcNow()))
        {
            throw new HubException("unauthorized");
        }

        var room = rooms.RoomForGuest(name);
        if (room is null)
        {
            throw new HubException("not_in_room");
        }

        Context.Items["guest"] = name;
        Context.Items["room"] = room;
        var self = presence.Attach(name, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, room);

        var roster = presence.Roster(rooms.MembersInRoom(room));
        await Clients.Caller.SendAsync("peers", roster);
        await Clients.OthersInGroup(room).SendAsync("peerJoined", self);
    }

    public async Task Leave()
    {
        Context.Items["intentionalLeave"] = true;
        await NotifyAndDetach(removeGuest: false, leaveRoom: false);
    }

    public async Task Signal(string toName, JsonElement payload)
    {
        var from = GuestName();
        if (from is null)
        {
            throw new HubException("unauthorized");
        }

        var connectionId = presence.ConnectionId(toName);
        if (connectionId is null)
        {
            return;
        }

        await Clients.Client(connectionId).SendAsync("signal", from, payload);
    }

    public async Task SetState(bool mic, bool headphones, bool share)
    {
        var name = GuestName();
        var room = Context.Items["room"] as string;
        if (name is null || room is null)
        {
            throw new HubException("not_in_room");
        }

        var state = presence.UpdateState(name, mic, headphones, share);
        if (state is null)
        {
            return;
        }

        await Clients.OthersInGroup(room).SendAsync("peerState", name, state);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var intentional = Context.Items["intentionalLeave"] as bool? == true;
        await NotifyAndDetach(removeGuest: !intentional, leaveRoom: !intentional);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task NotifyAndDetach(bool removeGuest, bool leaveRoom)
    {
        if (Context.Items["left"] as bool? == true)
        {
            return;
        }

        Context.Items["left"] = true;
        var name = Context.Items["guest"] as string;
        var room = Context.Items["room"] as string;
        if (name is null)
        {
            return;
        }

        if (room is not null)
        {
            await Clients.OthersInGroup(room).SendAsync("peerLeft", name);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, room);
        }

        if (leaveRoom)
        {
            rooms.TryLeave(name);
        }

        presence.Detach(name);
        if (removeGuest)
        {
            guests.Remove(name);
        }

        Context.Items.Remove("guest");
        Context.Items.Remove("room");
    }

    private string? GuestName()
    {
        return Context.User?.FindFirst(GuestTokens.NameClaim)?.Value;
    }
}

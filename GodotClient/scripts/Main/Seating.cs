using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// The level's benches, chairs, stools and beds (Core's Seats), and who is on which. A player's machine asks to sit
// on the seat in reach (E) and to get up (any move, skill, dash or E again); the host decides, so two never take one
// seat, and tells every machine. A player who goes down or leaves gets up. A machine that joins later is told who sits
// where. Present on every machine at the same path, for its RPCs. Design: Docs/Design/level-layouts.md, "Seats".
public partial class Seating : Node
{
    public const string NodeName = "Seating";

    private readonly SeatOccupancy _occupancy = new();

    // Who sits where on this machine, and where each stood as it asked: so one who arrives later is seated as it was.
    private readonly Dictionary<long, (Seat Seat, Vector3 From)> _seatOf = new();
    private IReadOnlyList<Seat> _seats = Array.Empty<Seat>();

    // On every machine, as a player sits or lies down (with where its body goes), and as one gets up.
    public static event Action<PlayerCharacter, Seat, Vector3>? Sat;

    public static event Action<PlayerCharacter>? Stood;

    public IReadOnlyList<Seat> All => _seats;

    public static Seating In(SceneTree tree) => tree.CurrentScene.GetNode<Seating>(NodeName);

    public void Build(ArenaMap map) => _seats = Core.Level.Seats.In(map.Layouts);

    public bool IsTaken(int seat) => _occupancy.IsTaken(seat);

    public int Taken => _occupancy.Taken.Count();

    // The free seat nearest a player standing there, within reach on its floor, if any.
    public Seat? InReachOf(Vector3 position)
    {
        var at = new System.Numerics.Vector3(position.X, position.Y, position.Z);
        return _seats.Where(s => !_occupancy.IsTaken(s.Id) && Core.Level.Seats.InReach(s, at))
            .MinBy(s => System.Numerics.Vector2.Distance(new(s.Spot.X, s.Spot.Z), new(at.X, at.Z)));
    }

    // From the player's own machine.
    public void Sit(Seat seat) => RpcId(1, MethodName.RequestSit, seat.Id);

    public void GetUp() => RpcId(1, MethodName.RequestGetUp);

    public override void _PhysicsProcess(double delta)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        foreach (var (_, peer) in _occupancy.Taken.ToList())
        {
            if (PlayerCharacter.Find(GetTree(), peer) is not { IsDowned: false })
            {
                Release(peer);
            }
        }
    }

    // Host: a machine that has just joined learns who sits where.
    public void SendAllTo(long peer)
    {
        foreach (var (seat, player) in _occupancy.Taken)
        {
            RpcId(peer, MethodName.SatDown, player, seat, _seatOf[player].From);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestSit(int seatId)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        long peer = Multiplayer.Sender();
        var player = PlayerCharacter.Find(GetTree(), peer);
        if (player == null || seatId < 0 || seatId >= _seats.Count)
        {
            GD.PushError($"[Seating] peer {peer} asked for seat {seatId}: no such player or seat here");
            return;
        }

        // The position the player last reported, as for a trade; a seat taken a moment before, or a player down or
        // out of reach, is refused without a word: its machine stays as it is.
        var from = new System.Numerics.Vector3(player.NetPosition.X, player.NetPosition.Y, player.NetPosition.Z);
        if (player.IsDowned || player.IsTrading || !Core.Level.Seats.InReach(_seats[seatId], from) || !_occupancy.TrySit(peer, seatId))
        {
            return;
        }

        Rpc(MethodName.SatDown, peer, seatId, player.NetPosition);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestGetUp()
    {
        if (Multiplayer.IsServer())
        {
            Release(Multiplayer.Sender());
        }
    }

    private void Release(long peer)
    {
        if (_occupancy.Stand(peer) != null)
        {
            Rpc(MethodName.GotUp, peer);
        }
    }

    // `from` is where the player stood as it asked: where it gets back up to.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SatDown(long peer, int seatId, Vector3 from)
    {
        if (!Multiplayer.IsServer())
        {
            _occupancy.Stand(peer);
            _occupancy.TrySit(peer, seatId);
        }

        _seatOf[peer] = (_seats[seatId], from);
        if (PlayerCharacter.Find(GetTree(), peer) is { } player)
        {
            Seat(player);
        }
    }

    // A player who comes into this machine's game already sitting, as a machine that joins later sees those who
    // were: it is seated as it arrives.
    public void SeatIfSitting(PlayerCharacter player)
    {
        if (_seatOf.ContainsKey(player.PeerId))
        {
            Seat(player);
        }
    }

    private void Seat(PlayerCharacter player)
    {
        var (seat, from) = _seatOf[player.PeerId];
        var pose = Core.Level.Seats.PoseOn(seat, new System.Numerics.Vector3(from.X, from.Y, from.Z));
        var origin = new Vector3(pose.Origin.X, pose.Origin.Y, pose.Origin.Z);
        player.OnSat(seat.Kind, origin, pose.Yaw, from);
        Sat?.Invoke(player, seat, origin);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void GotUp(long peer)
    {
        if (!Multiplayer.IsServer())
        {
            _occupancy.Stand(peer);
        }

        _seatOf.Remove(peer);
        if (PlayerCharacter.Find(GetTree(), peer) is { } player)
        {
            player.OnGotUp();
            Stood?.Invoke(player);
        }
    }
}

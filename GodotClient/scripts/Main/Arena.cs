using System;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Dev;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Main;

// Solo is a host nobody joins: Godot's default offline peer is its own server, so the same spawn path runs.
public partial class Arena : Node3D
{
    public const int MaxPlayers = 8;

    private const float SpawnRingRadius = 3f;

    private Node3D _players = null!;
    private MultiplayerSpawner _spawner = null!;
    private MultiplayerSpawner _enemySpawner = null!;
    private Node3D _enemies = null!;
    private ArenaCamera _camera = null!;
    private Hud _hud = null!;
    private LaunchOptions _options = null!;
    private NetSelfTest? _selfTest;
    private int _nextSpawnSlot;

    public override void _Ready()
    {
        _players = GetNode<Node3D>("Players");
        _spawner = GetNode<MultiplayerSpawner>("PlayerSpawner");
        _camera = GetNode<ArenaCamera>("Camera");
        _enemies = GetNode<Node3D>("Enemies");
        _enemySpawner = GetNode<MultiplayerSpawner>("EnemySpawner");
        _spawner.SpawnFunction = Callable.From<Variant, Node>(BuildPlayer);
        _enemySpawner.SpawnFunction = Callable.From<Variant, Node>(BuildEnemy);
        _hud = new Hud { Name = "Hud" };
        _hud.Bars.Camera = _camera;
        AddChild(_hud);
        _camera.ModeChanged += mode => _hud.ShowNotice($"Camera {mode.Label()}  -  C to change, wheel to zoom");
        _camera.SetMode(CameraModes.Default);
        _players.ChildEnteredTree += OnPlayerEntered;

        try
        {
            _options = LaunchOptions.Parse(OS.GetCmdlineUserArgs());
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            Fail(e.Message);
            return;
        }

        if (_options.Bot)
        {
            _selfTest = new NetSelfTest(_players, _hud) { Name = "NetSelfTest" };
            AddChild(_selfTest);
        }

        if (_options.CameraCheck)
        {
            AddChild(new CameraSelfTest(_camera, _players, _hud) { Name = "CameraSelfTest" });
        }

        if (_options.QuitAfter > 0f)
        {
            GetTree().CreateTimer(_options.QuitAfter).Timeout += () => Quit(0);
        }

        // The host's choice; clients get it from the host as they join (ReceiveRules). --pvp on a client is ignored.
        if (_options.Mode != SessionMode.Join)
        {
            SessionRules.Pvp = _options.Pvp;
            AnnounceRules();
        }

        switch (_options.Mode)
        {
            case SessionMode.Solo:
                SpawnPlayerFor(Multiplayer.GetUniqueId());
                break;
            case SessionMode.Host:
                StartHost();
                break;
            case SessionMode.Join:
                StartClient();
                break;
        }

        if (_options.Mode != SessionMode.Join && !_options.NoEnemies)
        {
            AddChild(new EnemyDirector(_enemySpawner, _enemies) { Name = "EnemyDirector" });
        }
    }

    private void StartHost()
    {
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateServer(_options.Port, MaxPlayers - 1);
        if (error != Error.Ok)
        {
            Fail($"Could not host on port {_options.Port}: {error}");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        Multiplayer.PeerConnected += id =>
        {
            GD.Print($"[net] peer {id} joined");
            RpcId(id, MethodName.ReceiveRules, SessionRules.Pvp);
            SpawnPlayerFor(id);
        };
        Multiplayer.PeerDisconnected += id =>
        {
            GD.Print($"[net] peer {id} left");
            _players.GetNodeOrNull(id.ToString())?.QueueFree();
        };
        GD.Print($"[net] hosting on port {_options.Port}");
        SpawnPlayerFor(Multiplayer.GetUniqueId());
    }

    private void StartClient()
    {
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateClient(_options.Address, _options.Port);
        if (error != Error.Ok)
        {
            Fail($"Could not start connecting to {_options.Address}:{_options.Port}: {error}");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        Multiplayer.ConnectedToServer += () => GD.Print($"[net] connected as peer {Multiplayer.GetUniqueId()}");
        Multiplayer.ConnectionFailed += () => Fail($"Could not connect to {_options.Address}:{_options.Port}");
        Multiplayer.ServerDisconnected += () => Fail("Host closed the session");
        GD.Print($"[net] joining {_options.Address}:{_options.Port}");
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveRules(bool pvp)
    {
        SessionRules.Pvp = pvp;
        AnnounceRules();
    }

    private void AnnounceRules()
    {
        GD.Print($"[net] rules: pvp={SessionRules.Pvp}");
        if (SessionRules.Pvp)
        {
            _hud.ShowNotice("PvP: players can hit each other");
        }
    }

    private void SpawnPlayerFor(long peerId)
    {
        float angle = _nextSpawnSlot++ % MaxPlayers * Mathf.Tau / MaxPlayers;
        var position = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * SpawnRingRadius;
        _spawner.Spawn(new Godot.Collections.Array { peerId, position });
    }

    // Runs on every peer: on the host through Spawn(), on clients when the spawn replicates.
    private Node BuildPlayer(Variant data)
    {
        var args = data.AsGodotArray();
        return PlayerCharacter.Create(args[0].AsInt64(), args[1].AsVector3());
    }

    private Node BuildEnemy(Variant data)
    {
        var args = data.AsGodotArray();
        return EnemyCharacter.Create(args[0].AsString(), Enemies.ById(args[1].AsString()), args[2].AsVector3(), args[3].AsSingle());
    }

    private void OnPlayerEntered(Node node)
    {
        if (node is not PlayerCharacter player || !player.IsMultiplayerAuthority())
        {
            return;
        }

        player.Controls = _options.Bot ? new BotControls(Multiplayer.GetUniqueId()) : new HumanControls(_camera);
        _camera.Target = player;
        _hud.Player = player;
    }

    private void Quit(int exitCode)
    {
        _selfTest?.PrintSummary();
        GetTree().Quit(exitCode);
    }

    private void Fail(string message)
    {
        GD.PushError($"[net] {message}");
        Quit(1);
    }
}

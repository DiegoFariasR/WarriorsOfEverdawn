using System;
using System.Linq;
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

    private const float BotSwapMargin = 5f;

    private Node3D _players = null!;
    private MultiplayerSpawner _spawner = null!;
    private MultiplayerSpawner _enemySpawner = null!;
    private Node3D _enemies = null!;
    private ArenaCamera _camera = null!;
    private Hud _hud = null!;
    private ArenaMap _map = null!;
    private LaunchOptions _options = null!;
    private NetSelfTest? _selfTest;
    private int _nextSpawnSlot;

    public override void _Ready()
    {
        try
        {
            _options = LaunchOptions.Parse(OS.GetCmdlineUserArgs());
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            Fail(e.Message);
            return;
        }

        if (_options.AiPlaytest)
        {
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Minimized);
            GD.Print("[window] --ai-playtest: minimized, no focus");
        }

        WorldLook.Apply(GetNode<WorldEnvironment>("WorldEnvironment"), GetNode<DirectionalLight3D>("Sun"));
        _players = GetNode<Node3D>("Players");
        _spawner = GetNode<MultiplayerSpawner>("PlayerSpawner");
        _camera = GetNode<ArenaCamera>("Camera");
        _enemies = GetNode<Node3D>("Enemies");
        _enemySpawner = GetNode<MultiplayerSpawner>("EnemySpawner");
        _spawner.SpawnFunction = Callable.From<Variant, Node>(BuildPlayer);
        _enemySpawner.SpawnFunction = Callable.From<Variant, Node>(BuildEnemy);
        try
        {
            _map = new ArenaMap { Name = ArenaMap.NodeName, Camera = _camera };
            AddChild(_map);
            _map.Build();
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException)
        {
            Fail(e.Message);
            return;
        }

        AddChild(new Arrows { Name = Arrows.NodeName });
        AddChild(new Bolts { Name = Bolts.NodeName });
        AddChild(new GroundWeapons { Name = GroundWeapons.NodeName });
        AddChild(new Loot { Name = Loot.NodeName, OrbChance = _options.OrbChance });
        var market = new Market { Name = Market.NodeName };
        AddChild(market);
        market.Build(_map);
        _hud = new Hud { Name = "Hud", Visible = !_options.NoUi };
        _hud.Bars.Camera = _camera;
        _hud.GroundLabels.Camera = _camera;
        AddChild(_hud);

        // The window asks, the market carries it to the host and brings back the answer; the player stands still
        // for as long as the window is open.
        _hud.Shop.BuyAsked += market.Buy;
        _hud.Shop.Opened += () => SetTrading(true);
        _hud.Shop.Closed += () => SetTrading(false);
        market.Bought += (_, item) => _hud.Shop.ShowBought(item);
        market.Refused += _hud.Shop.ShowRefused;
        _camera.ModeChanged += mode => _hud.ShowNotice($"Camera {mode.Label()}  -  C to change, wheel to zoom");
        _camera.SetMode(_options.Camera ?? CameraModes.Default);
        _camera.Zoom = _options.Zoom ?? _camera.Zoom;
        _players.ChildEnteredTree += OnPlayerEntered;

        if (_options.Bot)
        {
            _selfTest = new NetSelfTest(_players, _hud) { Name = "NetSelfTest", TradeDrill = _options.TradeDrill, BarrierDrill = _options.BarrierDrill };
            AddChild(_selfTest);
        }

        if (_options.Screenshot is { } shot)
        {
            // With --quit-after the session length is set elsewhere (the playtest), so the last frame doesn't end it.
            AddChild(new ScreenshotCapture(shot, Quit, quitWhenDone: _options.QuitAfter <= 0f) { Name = "ScreenshotCapture" });
        }

        if (_options.ArmourLineup is { } outfits)
        {
            AddChild(Lineup.OfArmour(outfits));
        }

        if (_options.WeaponLineup is { } shown)
        {
            AddChild(Lineup.OfWeapon(shown));
        }

        if (_options.MagicLineup is { } elements)
        {
            AddChild(Lineup.OfMagic(elements, _options.MagicBarriers));
        }

        if (_options.SwingSurvey)
        {
            AddChild(new SwingSurvey(Quit) { Name = "SwingSurvey" });
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
            AddChild(new EnemyDirector(_enemySpawner, _enemies, _options.WaveScale) { Name = "EnemyDirector" });
        }

        if (_options.Mode != SessionMode.Join && _options.DownAt > 0f)
        {
            GetTree().CreateTimer(_options.DownAt).Timeout += DownOnePlayer;
        }
    }

    // --down-at: a client's player when there is one, so the down and the revive also cross the network.
    private void DownOnePlayer()
    {
        var up = _players.GetChildren().OfType<PlayerCharacter>().Where(p => !p.IsDowned).ToList();
        var target = up.Where(p => p.PeerId != Multiplayer.GetUniqueId()).OrderBy(p => p.PeerId).FirstOrDefault() ?? up.FirstOrDefault();
        if (target == null)
        {
            GD.PushError("[net] --down-at: no player is up to take down");
            return;
        }

        GD.Print($"[net] --down-at: taking down player {target.Name}");
        target.Vitals.TakeHit(target.Vitals.Hp);
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
            GroundWeapons.In(GetTree()).SendAllTo(id);
            Loot.In(GetTree()).SendAllTo(id);
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
        _spawner.Spawn(new Godot.Collections.Array { peerId, _options.StartAt ?? _map.PlayerSpawnFor(_nextSpawnSlot++) });
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

    private void SetTrading(bool trading)
    {
        if (_hud.Player is { Controls: { } controls } player && IsInstanceValid(player))
        {
            player.IsTrading = trading;
            controls.Suspended = trading;
        }
    }

    private void OnPlayerEntered(Node node)
    {
        if (node is not PlayerCharacter player)
        {
            return;
        }

        if (Multiplayer.IsServer())
        {
            player.Vitals.EarnGold(_options.StartGold);
            player.Vitals.EarnOrbs(_options.StartOrbs);
            player.Vitals.Wear(Armours.AtTier(_options.StartArmour));
        }

        if (!player.IsMultiplayerAuthority())
        {
            return;
        }

        // A bot's last swap finishes before a timed session ends (BotControls).
        float swapUntil = _options.QuitAfter > 0f ? _options.QuitAfter - BotSwapMargin : float.PositiveInfinity;
        player.Controls = _options.Bot ? new BotControls(Multiplayer.GetUniqueId(), swapUntil) : new HumanControls(_camera);
        if (_options.Sets is { } sets)
        {
            player.Carry(sets);
        }

        _camera.Target = player;
        _map.CameraSubject = player;
        _hud.Player = player;
        _hud.GroundLabels.Player = player;
        player.TradeAsked += seller => _hud.Shop.Open(seller, player);
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

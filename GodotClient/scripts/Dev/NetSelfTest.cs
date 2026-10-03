using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Locomotion;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Dev;

// Attached in --bot runs. Records what this peer sees of every player, how far the local player's chest points
// from its aim, and the combat it takes part in, then prints the [*-check] lines ./dev.sh net-test asserts on.
// Each group of checks lives in its own part, NetSelfTest.<Area>.cs, with its fields, measurements and printed
// lines; this part runs the frame loop, tracks players as they appear and prints [net-check].
public partial class NetSelfTest : Node
{
    private const float SampleInterval = 0.5f;

    // What net-check prints for an empty slot.
    private const string NoWeapon = "none";

    private readonly Hud _hud;
    private readonly Dictionary<string, Observation> _seen = new();
    private float _sinceSample;
    private float _time;

    public NetSelfTest(Hud hud)
    {
        _hud = hud;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public NetSelfTest()
        : this(null!)
    {
    }

    public override void _EnterTree()
    {
        EnemyCharacter.Died += OnEnemyDied;
        EnemyCharacter.DamageTaken += OnEnemyDamaged;
        HitFlash.Started += OnFlash;
        PlayerVitals.DamagedByPlayer += OnPlayerDamagedByPlayer;
        Arrows.Loosed += OnArrowLoosed;
        Arrows.HitPlayer += OnArrowHit;
        PlayerVitals.Guarded += OnGuarded;
        EnemyCharacter.Parried += OnEnemyParried;
        EnemyCharacter.Died += CountDeathForLoot;
        PlayerVitals.Drank += OnDrank;
        TrackMagic();
        TrackStatus();
        TrackTown();
    }

    public override void _Ready()
    {
        TrackGround();
        TrackLoot();
        TrackMap();
        TrackTrade();
    }

    public override void _ExitTree()
    {
        EnemyCharacter.Died -= OnEnemyDied;
        EnemyCharacter.DamageTaken -= OnEnemyDamaged;
        HitFlash.Started -= OnFlash;
        PlayerVitals.DamagedByPlayer -= OnPlayerDamagedByPlayer;
        Arrows.Loosed -= OnArrowLoosed;
        Arrows.HitPlayer -= OnArrowHit;
        PlayerVitals.Guarded -= OnGuarded;
        PlayerVitals.Drank -= OnDrank;
        UntrackMagic();
        UntrackStatus();
        UntrackTown();
        EnemyCharacter.Parried -= OnEnemyParried;
        EnemyCharacter.Died -= CountDeathForLoot;
    }

    public override void _PhysicsProcess(double delta)
    {
        foreach (var player in PlayerCharacter.All(GetTree()))
        {
            if (!_seen.ContainsKey(player.Name))
            {
                Track(player);
            }
        }

        MeasureTurn((float)delta);
        MeasureEnemyHelmet();
        MeasureSpinMovement();
        MeasureGuardSpeed();

        _sinceSample += (float)delta;
        if (_sinceSample < SampleInterval)
        {
            return;
        }

        _sinceSample = 0f;
        foreach (var player in PlayerCharacter.All(GetTree()))
        {
            var seen = _seen[player.Name];
            var position = player.GlobalPosition;
            if (seen.Last is { } last)
            {
                seen.Travel += last.DistanceTo(position);
            }

            seen.Last = position;
            seen.Weapon = player.Weapon?.Id ?? NoWeapon;
            seen.Back = player.StowedWeapon?.Id ?? NoWeapon;
            if (player.Legs is { } legs)
            {
                seen.Legs.Add(legs);
            }
        }
    }

    // Runs after the animation trees in the same frame (this node sits after Players), so each reading is the
    // swing clip's position after that frame's advance.
    public override void _Process(double delta)
    {
        _time += (float)delta;
        MeasureGhosts();
        MeasureSpinDash();
        MeasureSwingRate((float)delta);
        MeasureSpinTurn((float)delta);
        MeasureTrails((float)delta);
        MeasureFlashes();
        MeasureHud();
        MeasurePickups();
        MeasureLoot();
        MeasureMap();
        MeasureTrade((float)delta);
        MeasureTown((float)delta);
        MeasureBot((float)delta);
        MeasureSession((float)delta);
        MeasureDowns();
        MeasureArrows();
        MeasureMagic((float)delta);
        MeasureStatus((float)delta);
    }

    public void PrintSummary()
    {
        long me = Multiplayer.GetUniqueId();
        foreach (var (name, seen) in _seen.OrderBy(pair => pair.Key))
        {
            GD.Print($"[net-check] me={me} player={name} weapon={seen.Weapon} back={seen.Back} weapon_changes={seen.WeaponChanges} travel={seen.Travel:F1} attacks={seen.Attacks} legs={string.Join(",", seen.Legs.OrderBy(l => l))}");
        }

        PrintCombatChecks(me);
        PrintBodyChecks(me);
        PrintSpinCheck(me);
        PrintWeaponChecks(me);
        PrintHudCheck(me);
        PrintDashCheck(me);
        PrintSessionChecks(me);
        PrintDownCheck(me);
        PrintRangedCheck(me);
        PrintGuardCheck(me);
        PrintLungeCheck(me);
        PrintPickupCheck(me);
        PrintLootCheck(me);
        PrintMapCheck(me);
        PrintTradeCheck(me);
        PrintTownCheck(me);
        PrintBotCheck(me);
        PrintMagicCheck(me);
        PrintStatusCheck(me);
    }

    private void Track(PlayerCharacter player)
    {
        var seen = new Observation();
        _seen[player.Name] = seen;
        TrackStatusOf(player);
        player.AttackStarted += _ => seen.Attacks++;
        player.WeaponsChanged += _ => seen.WeaponChanges++;
        TrackDashes(player);
        TrackLunges(player);
        TrackDowns(player);
        TrackGuard(player);
        if (!player.IsMultiplayerAuthority())
        {
            return;
        }

        TrackCombat(player);
        TrackSafety(player);
        TrackPickups(player);
        TrackBody(player);
        TrackSpin(player);
        TrackWeapon(player);
        TrackCasts(player);
    }

    private PlayerCharacter? LocalPlayer() => PlayerCharacter.Local(GetTree());

    // NaN with no samples, or none taken at all.
    private static float Median(IReadOnlyCollection<float>? values) =>
        values is { Count: > 0 } ? values.OrderBy(v => v).ElementAt(values.Count / 2) : float.NaN;

    private static string PerPeer(Dictionary<long, int> counts) => string.Join(",", counts.OrderBy(c => c.Key).Select(c => $"{c.Key}:{c.Value}"));

    private sealed class Observation
    {
        public Vector3? Last { get; set; }

        public float Travel { get; set; }

        public int Attacks { get; set; }

        // The weapons this machine last showed the player holding and carrying; players who leave keep theirs.
        public string Weapon { get; set; } = NoWeapon;

        public string Back { get; set; } = NoWeapon;

        // Changes to either weapon this machine saw after it first saw the player.
        public int WeaponChanges { get; set; }

        public HashSet<LegDirection> Legs { get; } = new();
    }
}

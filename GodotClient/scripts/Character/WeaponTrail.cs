using Godot;
using WarriorsOfEverdawn.Core.Combat;

namespace WarriorsOfEverdawn.Character;

// Ribbon behind a held weapon: while recording, each frame samples an edge (from partway out towards the striking
// point to the point itself) and the mesh joins the edges sampled in the last Lifetime seconds, fading with age. Adapted from Everdawn's
// WeaponTrail; edges expire by age rather than one per frame, so the trail looks the same at any frame rate.
public partial class WeaponTrail : MeshInstance3D
{
    private const int MaxEdges = 32;
    private const float LeadIn = 0.1f;
    private const float FollowThrough = 0.08f;
    private const float Lifetime = 0.15f;
    private const float MinEdgeDistance = 0.04f;

    // The trail starts this far out towards the striking point, so it follows the blade rather than the grip.
    private const float BaseFraction = 0.4f;

    private readonly Vector3[] _bases = new Vector3[MaxEdges];
    private readonly Vector3[] _tips = new Vector3[MaxEdges];
    private readonly float[] _born = new float[MaxEdges];
    private readonly ArrayMesh _mesh = new();
    private readonly BoneAttachment3D _hand;
    private readonly StandardMaterial3D _material;
    private Vector3[] _points = System.Array.Empty<Vector3>();
    private int _edgeCount;
    private float _clock;

    public WeaponTrail(BoneAttachment3D hand, Color tint)
    {
        _hand = hand;
        _material = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo = true,
            AlbedoColor = tint,
        };
        CastShadow = ShadowCastingSetting.Off;
        Mesh = _mesh;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public WeaponTrail()
        : this(null!, Colors.White)
    {
    }

    public bool Recording { get; set; }

    // Seconds a trail outlives the last edge it recorded.
    public static float FadeTime => Lifetime;

    public int EdgeCount => _edgeCount;

    // The weapon's striking point in the hand's space (CharacterRig.WeaponTip).
    public Vector3 TipPoint { get; private set; }

    public float TipLength => TipPoint.Length();

    public Vector3 LatestTip => _edgeCount > 0 ? _tips[_edgeCount - 1] : Vector3.Zero;

    // Where the striking point is right now, from the hand's current pose.
    public Vector3 CurrentTip => _hand.GlobalTransform * TipPoint;

    // The trail marks the swing's hit window, so it shows exactly when the weapon deals damage, with a short lead-in
    // and follow-through. swingTime is real seconds since the swing started.
    public static bool Shows(SkillDefinition skill, float swingTime, float attackSpeed) =>
        swingTime >= CombatTiming.HitDelay(skill, attackSpeed) - LeadIn
        && swingTime <= CombatTiming.HitWindowEnd(skill, attackSpeed) + FollowThrough;

    public override void _Ready()
    {
        // Vertices are world positions. TopLevel keeps the parent's transform off them; the identity basis matters
        // too, since TopLevel copies the old global transform into the local one (Everdawn's "trail in random places").
        TopLevel = true;
        Transform = Transform3D.Identity;
        Retarget();
    }

    // After the hand takes a different weapon: the trail follows the new one's striking point.
    public void Retarget()
    {
        _points = CharacterRig.WeaponPoints(_hand);
        TipPoint = CharacterRig.WeaponTip(_points);
        _edgeCount = 0;
    }

    // How far the held weapon reaches across the ground from a centre right now (CharacterRig.WeaponReach).
    public float ReachFrom(Vector3 centre) => CharacterRig.WeaponReach(_points, _hand.GlobalTransform, centre);

    public override void _Process(double delta)
    {
        _clock += (float)delta;
        int expired = 0;
        while (expired < _edgeCount && _clock - _born[expired] > Lifetime)
        {
            expired++;
        }

        Shift(expired);
        if (Recording)
        {
            Sample();
        }

        Rebuild();
    }

    private void Sample()
    {
        var hand = _hand.GlobalTransform;
        var tip = hand * TipPoint;
        if (_edgeCount > 0 && _tips[_edgeCount - 1].DistanceTo(tip) < MinEdgeDistance)
        {
            return;
        }

        if (_edgeCount == MaxEdges)
        {
            Shift(1);
        }

        _bases[_edgeCount] = hand * (TipPoint * BaseFraction);
        _tips[_edgeCount] = tip;
        _born[_edgeCount] = _clock;
        _edgeCount++;
    }

    private void Shift(int count)
    {
        if (count <= 0)
        {
            return;
        }

        for (int i = count; i < _edgeCount; i++)
        {
            _bases[i - count] = _bases[i];
            _tips[i - count] = _tips[i];
            _born[i - count] = _born[i];
        }

        _edgeCount -= count;
    }

    private void Rebuild()
    {
        _mesh.ClearSurfaces();
        if (_edgeCount < 2)
        {
            return;
        }

        var vertices = new Vector3[_edgeCount * 2];
        var colors = new Color[_edgeCount * 2];
        var indices = new int[(_edgeCount - 1) * 6];
        for (int i = 0; i < _edgeCount; i++)
        {
            // Fresh edges are bright, and the tip edge brighter than the base, so the ribbon reads as the blade's path.
            float freshness = 1f - Mathf.Clamp((_clock - _born[i]) / Lifetime, 0f, 1f);
            float alpha = freshness * freshness;
            vertices[i * 2] = _bases[i];
            vertices[i * 2 + 1] = _tips[i];
            colors[i * 2] = new Color(1f, 1f, 1f, alpha * 0.15f);
            colors[i * 2 + 1] = new Color(1f, 1f, 1f, alpha * 0.7f);
        }

        for (int i = 0; i < _edgeCount - 1; i++)
        {
            int v = i * 2;
            indices[i * 6] = v;
            indices[i * 6 + 1] = v + 2;
            indices[i * 6 + 2] = v + 1;
            indices[i * 6 + 3] = v + 2;
            indices[i * 6 + 4] = v + 3;
            indices[i * 6 + 5] = v + 1;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Godot.Mesh.ArrayType.Max);
        arrays[(int)Godot.Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Godot.Mesh.ArrayType.Color] = colors;
        arrays[(int)Godot.Mesh.ArrayType.Index] = indices;
        _mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
        _mesh.SurfaceSetMaterial(0, _material);
    }
}

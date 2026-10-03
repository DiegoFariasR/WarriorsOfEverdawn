using Godot;

namespace WarriorsOfEverdawn.Util;

public static class Replication
{
    // Each of the node's own properties goes whole to a peer as the node spawns there, then by `mode`.
    public static SceneReplicationConfig Sending(this SceneReplicationConfig config, SceneReplicationConfig.ReplicationMode mode, params StringName[] properties)
    {
        foreach (var property in properties)
        {
            var path = new NodePath($".:{property}");
            config.AddProperty(path);
            config.PropertySetSpawn(path, true);
            config.PropertySetReplicationMode(path, mode);
        }

        return config;
    }

    // The peer the RPC being run came from. Godot gives 0 for one called on this machine (the host's own player
    // asking the host), which is this machine's peer.
    public static long Sender(this MultiplayerApi multiplayer)
    {
        long sender = multiplayer.GetRemoteSenderId();
        return sender == 0 ? multiplayer.GetUniqueId() : sender;
    }
}

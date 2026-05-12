#nullable enable
using System;
using System.Dynamic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

public static class RagdollSettings
{
    private static readonly string ConfigPath = Path.ChangeExtension(
        Assembly.GetExecutingAssembly().Location, ".cfg");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public sealed class Data
    {
        public float ExplodeGravity { get; set; } = 2500f;
        public float ExplodeSpeed { get; set; } = 1100f;
        public float ExplodeAngleDirectionDeg { get; set; } = 270f;
        public float ExplodeAngleSpreadDeg { get; set; } = 60f;
        public float ExplodeAngularSpeed { get; set; } = 30f;
        public float RagdollGravity { get; set; } = 2500f;
        public float RagdollSpeed { get; set; } = 600f;
        public float RagdollAngleDirectionDeg { get; set; } = 315f;
        public float RagdollAngleSpreadDeg { get; set; } = 20f;
        public float RagdollAngularSpeed { get; set; } = -10f;
        public Boolean ZeroGravity { get; set; } = false;
        public Boolean ForcedExplosionMode { get; set; } = false;
        public Boolean OverkillForce { get; set; } = false;
        public Boolean SmallPartExplosionExclude { get; set; } = true;
        public Boolean ExcludeAllies { get; set; } = false;
    }

    public static Data Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            var json = File.ReadAllText(ConfigPath);
            Current = JsonSerializer.Deserialize<Data>(json, JsonOptions) ?? new();
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[Ragdoll] Failed to load config: {e.Message}");
        }
    }

    public static void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[Ragdoll] Failed to save config: {e.Message}");
        }
    }
}

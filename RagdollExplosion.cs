#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

public static class RagdollExplosion
{
    private static readonly Random _rng = new Random();

    public static void Spawn(NCreature creature)
    {
        if (!creature.HasSpineAnimation) return;

        var body = creature.Body;
        var skelDataRes = body.Get("skeleton_data_res").AsGodotObject();
        if (skelDataRes == null) return;

        var atlasRes = skelDataRes.Get("atlas_res").AsGodotObject();
        if (atlasRes == null) return;

        var sourcePath = atlasRes.Get("resource_path").AsString();
        if (string.IsNullOrEmpty(sourcePath)) return;

        var atlasText = ReadAtlasText(sourcePath);
        if (atlasText == null)
        {
            GD.PrintErr($"[Ragdoll] Failed to read atlas text for {sourcePath}");
            return;
        }

        var texturesArray = atlasRes.Get("textures").AsGodotArray();
        if (texturesArray == null || texturesArray.Count == 0) return;

        var texture = texturesArray[0].As<Texture2D>();
        if (texture == null) return;

        var (_, _, allRegions) = SpineAtlasParser.Parse(atlasText);
        var regionMap = new Dictionary<string, AtlasRegion>();
        foreach (var r in allRegions) regionMap.TryAdd(r.Name, r);

        var visuals = creature.Visuals;
        float partScale = visuals.Scale.X;
        var customConfig = RagdollConfigs.Get(creature.Entity.ModelId.Entry);
        var partParent = RagdollPatch.CombatContainer ?? creature.GetTree().CurrentScene;
        var fallbackPos = visuals.GlobalPosition;

        var boundsNode = visuals.Bounds;
        float floorY = boundsNode.GlobalPosition.Y + boundsNode.Size.Y;

        var (boneTransforms, _, _) =
            RagdollPreloader.Pop(creature) ?? RagdollPreloader.GetBoneInfo(body);
        const int minSize = 20;

        foreach (var attachName in boneTransforms.Keys)
        {
            if (!regionMap.TryGetValue(attachName, out var region)) continue;
            if (region.Bounds.Size.X < minSize || region.Bounds.Size.Y < minSize) continue;
            if (customConfig != null && customConfig.ExcludedRegions.Contains(attachName)) continue;

            var startPos = boneTransforms.TryGetValue(region.Name, out var bt) ? bt.Origin : fallbackPos;
            var effect = customConfig?.Effects.GetValueOrDefault(region.Name);
            var finishEffect = customConfig?.FinishEffects.GetValueOrDefault(region.Name);
            SpawnPart(partParent, texture, region, startPos, partScale, floorY, effect, finishEffect);
        }
    }

    private static string? ReadAtlasText(string atlasResPath)
    {
        var importPath = atlasResPath + ".import";
        var importFile = FileAccess.Open(importPath, FileAccess.ModeFlags.Read);
        if (importFile == null) return null;

        var importText = importFile.GetAsText();
        importFile.Close();

        string? spatlasPath = null;
        foreach (var line in importText.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("path="))
            {
                spatlasPath = trimmed.Substring(5).Trim('"');
                break;
            }
        }
        if (spatlasPath == null) return null;

        var spatlasFile = FileAccess.Open(spatlasPath, FileAccess.ModeFlags.Read);
        if (spatlasFile == null) return null;

        var json = spatlasFile.GetAsText();
        spatlasFile.Close();

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("atlas_data", out var atlasDataEl))
            return atlasDataEl.GetString();

        return null;
    }

    private static Vector2 Gravity => new Vector2(0, RagdollSettings.Current.Gravity);
    private const float AirDamp          = 0.985f;
    private const float BounceDamp       = 0.65f;
    private const float Friction         = 0.90f;
    private const float StopVelThreshold = 30f;

    private static void SpawnPart(Node parent, Texture2D texture, AtlasRegion region, Vector2 spawnPos, float scale, float floorY, Action<Node2D, Sprite2D>? effect = null, Action<Node2D, Sprite2D>? finishEffect = null)
    {
        var node = new Node2D();
        node.ProcessMode = Node.ProcessModeEnum.Always;

        var atlasTexture = new AtlasTexture { Atlas = texture, FilterClip = true };
        var bounds = region.Bounds;
        var sprite = new Sprite2D { Texture = atlasTexture, Scale = Vector2.One * scale };

        if (region.Rotated)
        {
            sprite.Rotation = -Mathf.Pi / 2f;
            atlasTexture.Region = new Rect2(bounds.Position.X, bounds.Position.Y, bounds.Size.Y, bounds.Size.X);
        }
        else
        {
            atlasTexture.Region = new Rect2(bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y);
        }

        node.AddChild(sprite);
        parent.AddChild(node);
        node.GlobalPosition = spawnPos;
        effect?.Invoke(node, sprite);

        var s = RagdollSettings.Current;
        float spread = s.AngleSpreadDeg * Mathf.Pi / 180f;
        float angle  = (float)(_rng.NextDouble() * spread - spread * 0.85f);
        float speed  = (float)(_rng.NextDouble() * s.Speed * 0.6f + s.Speed);
        var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        float angVel = (float)(_rng.NextDouble() * s.AngularSpeed * 2 - s.AngularSpeed);

        Simulate(node, sprite, velocity, angVel, floorY, finishEffect);
    }

    private static async void Simulate(Node2D node, Sprite2D sprite, Vector2 velocity, float angVel, float floorY, Action<Node2D, Sprite2D>? onFinish = null)
    {
        ulong lastTick = Time.GetTicksUsec();

        while (GodotObject.IsInstanceValid(node))
        {
            await node.ToSignal(node.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(node)) return;

            ulong now = Time.GetTicksUsec();
            float delta = Mathf.Min((now - lastTick) / 1_000_000f, 0.05f);
            lastTick = now;

            velocity += Gravity * delta;
            velocity *= Mathf.Pow(AirDamp, delta * 60f);
            angVel   *= Mathf.Pow(AirDamp, delta * 60f);

            var newPos = node.GlobalPosition + velocity * delta;

            if (newPos.Y >= floorY && velocity.Y > 0f)
            {
                newPos.Y    = floorY;
                velocity.Y  = -velocity.Y * BounceDamp;
                velocity.X *= Friction;
                angVel     *= 0.5f;
                if (Mathf.Abs(velocity.Y) < StopVelThreshold) velocity.Y = 0f;
            }

            node.GlobalPosition = newPos;
            sprite.Rotation    += angVel * delta;

            if (velocity.LengthSquared() < 100f && Mathf.Abs(angVel) < 0.1f) break;
        }

        if (!GodotObject.IsInstanceValid(node)) return;

        onFinish?.Invoke(node, sprite);

        var tween = node.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(sprite, "modulate:a", 0f, 0.3f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(node)) node.QueueFree();
        }));
    }
}

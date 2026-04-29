#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

public static class RagdollSpawner
{
    private static readonly Random _rng = new Random();
    public static Node? CombatContainer;

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

        var (_, _, regions) = SpineAtlasParser.Parse(atlasText);

        var visuals = creature.Visuals;
        float partScale = visuals.Scale.X;
        var partParent = CombatContainer ?? creature.GetTree().CurrentScene;
        var fallbackPos = visuals.GlobalPosition;

        var boundsNode = visuals.Bounds;
        float floorY = boundsNode.GlobalPosition.Y + boundsNode.Size.Y;

        var bonePositions = GetBonePositions(body, visuals.Scale, visuals.GlobalPosition);

        const int minSize = 20;
        foreach (var region in regions)
        {
            if (region.Bounds.Size.X < minSize || region.Bounds.Size.Y < minSize) continue;

            var startPos = bonePositions.TryGetValue(region.Name, out var bonePos) ? bonePos : fallbackPos;
            SpawnPart(partParent, texture, region, startPos, partScale, floorY);
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

    private static Dictionary<string, Vector2> GetBonePositions(Node2D body, Vector2 nodeScale, Vector2 nodeGlobalPos)
    {
        var result = new Dictionary<string, Vector2>();
        try
        {
            var skeleton = body.Call("get_skeleton").AsGodotObject();
            if (skeleton == null) return result;

            var slots = skeleton.Call("get_slots").AsGodotArray();
            foreach (var slotVar in slots)
            {
                var slot = slotVar.AsGodotObject();
                if (slot == null) continue;

                var attachment = slot.Call("get_attachment").AsGodotObject();
                if (attachment == null) continue;

                var attachName = attachment.Call("get_name").AsString();
                if (string.IsNullOrEmpty(attachName)) continue;

                var bone = slot.Call("get_bone").AsGodotObject();
                if (bone == null) continue;

                float wx = bone.Call("get_world_x").AsSingle();
                float wy = bone.Call("get_world_y").AsSingle();

                // Spine Y-up → Godot Y-down, Visuals.Scale 반영
                var localPos = new Vector2(wx, -wy) * nodeScale;
                result[attachName] = nodeGlobalPos + localPos;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[Ragdoll] GetBonePositions error: {e.Message}");
        }
        return result;
    }

    private static Vector2 Gravity => new Vector2(0, RagdollSettings.Current.Gravity);
    private const float BounceDamp = 0.65f;
    private const float Friction = 0.90f;
    private const float StopVelThreshold = 30f;

    private static void SpawnPart(Node parent, Texture2D texture, AtlasRegion region, Vector2 spawnPos, float scale, float floorY)
    {
        var node = new Node2D();
        node.ProcessMode = Node.ProcessModeEnum.Always;

        var atlasTexture = new AtlasTexture();
        atlasTexture.Atlas = texture;
        atlasTexture.FilterClip = true;

        var bounds = region.Bounds;
        atlasTexture.Region = new Rect2(bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y);

        var sprite = new Sprite2D();
        sprite.Texture = atlasTexture;
        sprite.Scale = Vector2.One * scale;
        if (region.Rotated)
            sprite.Rotation = -Mathf.Pi / 2f;

        node.AddChild(sprite);
        parent.AddChild(node);
        node.GlobalPosition = spawnPos;

        var s = RagdollSettings.Current;
        float spread = s.AngleSpreadDeg * Mathf.Pi / 180f;
        float angle = (float)(_rng.NextDouble() * spread - spread * 0.85f);
        float speed = (float)(_rng.NextDouble() * s.Speed * 0.6f + s.Speed);
        var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        float angVel = (float)(_rng.NextDouble() * s.AngularSpeed * 2 - s.AngularSpeed);

        Simulate(node, sprite, velocity, angVel, floorY);
    }

    private const float AirDamp = 0.985f;

    private static async void Simulate(Node2D node, Sprite2D sprite, Vector2 velocity, float angVel, float floorY)
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
            angVel *= Mathf.Pow(AirDamp, delta * 60f);

            var newPos = node.GlobalPosition + velocity * delta;

            if (newPos.Y >= floorY && velocity.Y > 0f)
            {
                newPos.Y = floorY;
                velocity.Y = -velocity.Y * BounceDamp;
                velocity.X *= Friction;
                angVel *= 0.5f;

                if (Mathf.Abs(velocity.Y) < StopVelThreshold)
                    velocity.Y = 0f;
            }

            node.GlobalPosition = newPos;
            sprite.Rotation += angVel * delta;

            if (velocity.LengthSquared() < 100f && Mathf.Abs(angVel) < 0.1f)
                break;
        }

        if (!GodotObject.IsInstanceValid(node)) return;
        var tween = node.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(sprite, "modulate:a", 0f, 0.4f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(node)) node.QueueFree();
        }));
    }
}

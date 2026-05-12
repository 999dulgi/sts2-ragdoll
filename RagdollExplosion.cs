#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

public static class RagdollExplosion
{
    private static readonly Random _rng = new Random();

    public static void Spawn(NCreature creature, int overDamage = 0)
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


        // named skin 기반 fallback 매핑: boneTransform key → atlas region
        // custom-skin이 override한 slot의 경우 default key가 atlas에 없을 수 있음
        var skeleton = body.Call("get_skeleton").AsGodotObject();
        var skelData2 = skeleton?.Call("get_data").AsGodotObject();
        var allDataSkins2 = skelData2?.Call("get_skins").AsGodotArray();
        // slotIdx → slotName
        var slotIdxToName = new Dictionary<int, string>();
        var slots2 = skeleton?.Call("get_slots").AsGodotArray();
        if (slots2 != null)
        {
            for (int i = 0; i < slots2.Count; i++)
            {
                var sd = slots2[i].AsGodotObject()?.Call("get_data").AsGodotObject();
                if (sd != null) slotIdxToName[i] = sd.Call("get_name").AsString();
            }
        }
        // named skin entry key → atlas region (skinName prefix 매칭)
        var fallbackRegionMap = new Dictionary<string, string>();
        if (allDataSkins2 != null)
        {
            foreach (var sv in allDataSkins2)
            {
                var s = sv.AsGodotObject(); if (s == null) continue;
                var sName = s.Call("get_name").AsString();
                if (sName == "default") continue;
                foreach (var ev in s.Call("get_attachments").AsGodotArray())
                {
                    var entry = ev.AsGodotObject(); if (entry == null) continue;
                    var slotIdx = entry.Call("get_slot_index").AsInt32();
                    var key = entry.Call("get_name").AsString();
                    if (fallbackRegionMap.ContainsKey(key)) continue;
                    if (!slotIdxToName.TryGetValue(slotIdx, out var slotName)) continue;
                    // skinName + "/" + slotName 패턴으로 atlas region 탐색
                    var candidate = regionMap.Keys.FirstOrDefault(r =>
                        r.StartsWith(sName + "/", StringComparison.OrdinalIgnoreCase) &&
                        r.EndsWith(slotName, StringComparison.OrdinalIgnoreCase));
                    if (candidate != null)
                        fallbackRegionMap[key] = candidate;
                }
            }
        }

        foreach (var attachName in boneTransforms.Keys)
        {
            if (!regionMap.TryGetValue(attachName, out var region))
            {
                if (!fallbackRegionMap.TryGetValue(attachName, out var fallbackName)) continue;
                if (!regionMap.TryGetValue(fallbackName, out region)) continue;
            }
            bool inSeparate = customConfig?.SeparateRegions.ContainsKey(attachName) == true;
            if (!inSeparate && (region.Bounds.Size.X < minSize || region.Bounds.Size.Y < minSize)) continue;
            if (customConfig != null && customConfig.ExcludedRegions.Contains(attachName)) continue;

            var effect = customConfig?.Effects.GetValueOrDefault(region.Name);
            var finishEffect = customConfig?.FinishEffects.GetValueOrDefault(region.Name);

            if (customConfig?.SeparateRegions.TryGetValue(attachName, out var subRects) == true
                && subRects != null && subRects.Length > 0)
            {
                float logicalW = region.Rotated ? region.Bounds.Size.Y : region.Bounds.Size.X;
                float logicalH = region.Rotated ? region.Bounds.Size.X : region.Bounds.Size.Y;
                var fullLogicalCenter = new Vector2(logicalW / 2f, logicalH / 2f);

                foreach (var (pos, size) in subRects)
                {
                    if (RagdollSettings.Current.SmallPartExplosionExclude && !inSeparate && (size.X < minSize || size.Y < minSize)) continue;

                    Rect2 subAtlasRect;
                    subAtlasRect = new Rect2(
                            region.Bounds.Position.X + pos.X,
                            region.Bounds.Position.Y + pos.Y,
                            size.X, size.Y);

                    var subLogicalCenter = new Vector2(pos.X + size.X / 2f, pos.Y + size.Y / 2f);
                    var logicalOffset = subLogicalCenter - fullLogicalCenter;
                    var spawnOffset = (region.Rotated
                        ? new Vector2(logicalOffset.Y, -logicalOffset.X)
                        : logicalOffset) * partScale;
                    
                    // 파편별로도 약간의 속도 변동을 줄 수 있음
                    SpawnPart(partParent, texture, region, fallbackPos + spawnOffset, partScale, floorY, overDamage, subAtlasRect, effect, finishEffect);
                }
            }
            else
            {
                SpawnPart(partParent, texture, region, fallbackPos, partScale, floorY, overDamage, null, effect, finishEffect);
            }
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

    private static Vector2 Gravity => RagdollSettings.Current.ZeroGravity ? Vector2.Zero : new Vector2(0, RagdollSettings.Current.ExplodeGravity);
    private const float AirDamp          = 0.99f;
    private const float BounceDamp       = 0.4f;
    private const float Friction         = 0.85f;
    private const float StopVelThreshold = 30f;

    public static void SpawnPlayerRelics(NCreature creature)
    {
        var player = creature.Entity.Player;
        if (player == null) return;

        var relics = player.Relics;
        if (relics.Count == 0) return;

        var spawnPos   = creature.Visuals.GlobalPosition;
        var boundsNode = creature.Visuals.Bounds;
        float floorY   = boundsNode.GlobalPosition.Y + boundsNode.Size.Y;
        var partParent = RagdollPatch.CombatContainer ?? creature.GetTree().CurrentScene;

        foreach (var relic in relics)
        {
            var icon = relic.Icon;
            if (icon == null) continue;
            SpawnIcon(partParent, icon, spawnPos, floorY);
        }
    }

    private static void SpawnIcon(Node parent, Texture2D icon, Vector2 spawnPos, float floorY)
    {
        var node   = new Node2D { ProcessMode = Node.ProcessModeEnum.Always };
        var sprite = new Sprite2D { Texture = icon };
        node.AddChild(sprite);
        parent.AddChild(node);
        node.GlobalPosition = spawnPos;

        var s      = RagdollSettings.Current;
        float angle = Mathf.DegToRad(s.ExplodeAngleDirectionDeg + (float)(_rng.NextDouble() * s.ExplodeAngleSpreadDeg - s.ExplodeAngleSpreadDeg / 2f));
        float speed = (float)(_rng.NextDouble() * s.ExplodeSpeed * 0.6f + s.ExplodeSpeed);
        var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        float angVel = (float)(_rng.NextDouble() * s.ExplodeAngularSpeed * 2 - s.ExplodeAngularSpeed);

        Simulate(node, sprite, velocity, angVel, floorY, keepCorpse: true);
    }

    private static void SpawnPart(Node parent, Texture2D texture, AtlasRegion region, Vector2 spawnPos, float scale, float floorY, int overDamage, Rect2? subAtlasRect = null, Action<Node2D, Sprite2D>? effect = null, Action<Node2D, Sprite2D>? finishEffect = null)
    {
        var node = new Node2D();
        node.ProcessMode = Node.ProcessModeEnum.Always;

        var atlasTexture = new AtlasTexture { Atlas = texture, FilterClip = true };
        var sprite = new Sprite2D { Texture = atlasTexture, Scale = Vector2.One * scale };

        if (subAtlasRect.HasValue)
        {
            atlasTexture.Region = subAtlasRect.Value;
            if (region.Rotated)
                sprite.Rotation = -Mathf.Pi / 2f;
        }
        else if (region.Rotated)
        {
            sprite.Rotation = -Mathf.Pi / 2f;
            var b = region.Bounds;
            atlasTexture.Region = new Rect2(b.Position.X, b.Position.Y, b.Size.Y, b.Size.X);
        }
        else
        {
            var b = region.Bounds;
            atlasTexture.Region = new Rect2(b.Position.X, b.Position.Y, b.Size.X, b.Size.Y);
        }

        node.AddChild(sprite);

        parent.AddChild(node);
        node.GlobalPosition = spawnPos;
        effect?.Invoke(node, sprite);

        var s = RagdollSettings.Current;
        
        // 전달받은 속도가 있으면 해당 값을 기준으로 ±20% 무작위성 적용, 없으면 기존 방식 유지

        float angle  = Mathf.DegToRad(s.ExplodeAngleDirectionDeg + (float)(_rng.NextDouble() * s.ExplodeAngleSpreadDeg - s.ExplodeAngleSpreadDeg / 2f));
        float speed  = (float)(_rng.NextDouble() * s.ExplodeSpeed * 0.6f + s.ExplodeSpeed + overDamage * 30f);
        Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        float angVel = (float)(_rng.NextDouble() * s.ExplodeAngularSpeed * 2 - s.ExplodeAngularSpeed);


        Simulate(node, sprite, velocity, angVel, floorY, finishEffect);
    }

    private static async void Simulate(Node2D node, Sprite2D sprite, Vector2 velocity, float angVel, float floorY, Action<Node2D, Sprite2D>? onFinish = null, bool keepCorpse = false)
    {
        ulong lastTick = Time.GetTicksUsec();
        ulong startTick = lastTick;
        var screenRect = node.GetViewportRect();

        while (GodotObject.IsInstanceValid(node) && (Time.GetTicksUsec() - startTick) < 10_000_000ul)
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

            if (newPos.X <= screenRect.Position.X || newPos.X > screenRect.End.X)
                velocity.X = -velocity.X * BounceDamp;

            if (RagdollSettings.Current.ZeroGravity && newPos.Y < screenRect.Position.Y)
            {
                newPos.Y   = screenRect.Position.Y;
                velocity.Y = -velocity.Y * BounceDamp;
            }

            node.GlobalPosition = newPos;
            sprite.Rotation    += angVel * delta;

            if (velocity.LengthSquared() < 100f && Mathf.Abs(angVel) < 0.1f) break;
        }

        if (!GodotObject.IsInstanceValid(node)) return;

        onFinish?.Invoke(node, sprite);

        if (keepCorpse) return;

        var tween = node.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(sprite, "modulate:a", 0f, 0.3f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(node)) node.QueueFree();
        }));
    }
}

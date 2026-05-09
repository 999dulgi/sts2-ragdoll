#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class SpineRagdoll
{
    private const float AirDamp         = 0.99f;
    private const float BounceDamp      = 0.4f;
    private const float Friction        = 0.85f;
    private const float BodyDamp        = 0.972f;
    private const float WobbleDamp      = 0.88f;
    private const float SpringK         = 3f;
    private const float MaxTime    = 2f;
    private const float StopVelSq  = 1f;
    private const float StopAngDeg = 0.5f;

    public static async void Start(Node2D body, float floorY, int overDamage = 0, GodotObject? skin = null)
    {
        var s   = RagdollSettings.Current;
        var rng = new Random();

        var mainPos  = body.GlobalPosition;
        float rawX   = (float)(rng.NextDouble() * 2 - 1);
        float clampedX = rawX >= 0 ? Mathf.Max(rawX, 0.4f) : Mathf.Min(rawX, -0.4f);
        var dir      = new Vector2(clampedX, -1f).Normalized();
        float speed  = (float)(rng.NextDouble() * s.RagdollSpeed * 0.6f + s.RagdollSpeed + overDamage * 30f);
        var mainPrev = mainPos - dir * speed * 0.016f;
        var gravity  = s.ZeroGravity ? Vector2.Zero : new Vector2(0, s.RagdollGravity);

        var tree = body.GetTree();

        // _ready() 실행 대기 (skeleton 초기화 후에 접근)
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (!GodotObject.IsInstanceValid(body)) return;

        body.Call("set_update_mode", 2); // Manual

        var skeleton = body.Call("get_skeleton").AsGodotObject();
        if (skeleton == null) return;

        if (skin != null && GodotObject.IsInstanceValid(skin))
        {
            skeleton.Call("set_skin", skin);
            skeleton.Call("set_slots_to_setup_pose");
        }

        var boneObjects = new Dictionary<string, GodotObject>();
        foreach (var v in skeleton.Call("get_bones").AsGodotArray())
        {
            var bone = v.AsGodotObject(); if (bone == null) continue;
            var data = bone.Call("get_data").AsGodotObject(); if (data == null) continue;
            var name = data.Call("get_bone_name").AsString();
            if (!string.IsNullOrEmpty(name)) boneObjects[name] = bone;
        }

        if (boneObjects.Count == 0) return;

        // skeleton에서 직접 뼈 부모 관계 읽기
        var boneParents = new Dictionary<string, string>();
        foreach (var (name, bone) in boneObjects)
        {
            var parentBone = bone.Call("get_parent").AsGodotObject();
            if (parentBone == null) continue;
            var parentData = parentBone.Call("get_data").AsGodotObject();
            if (parentData == null) continue;
            var parentName = parentData.Call("get_bone_name").AsString();
            if (!string.IsNullOrEmpty(parentName)) boneParents[name] = parentName;
        }

        var rootBoneName = boneObjects.Keys.FirstOrDefault(n => !boneParents.ContainsKey(n));
        var rootBone     = rootBoneName != null ? boneObjects[rootBoneName] : null;

        // clipping attachment 슬롯을 null로 설정해 마스크 비활성화
        foreach (var v in skeleton.Call("get_slots").AsGodotArray())
        {
            var slot       = v.AsGodotObject(); if (slot == null) continue;
            var attachment = slot.Call("get_attachment").AsGodotObject(); if (attachment == null) continue;
            var attachName = attachment.Call("get_attachment_name").AsString();
            if (!attachName.Contains("mask", StringComparison.OrdinalIgnoreCase) &&
                !attachName.Contains("clip", StringComparison.OrdinalIgnoreCase)) continue;
            slot.Call("set_attachment", new Variant());
        }

        // 뼈 계층 깊이 계산
        var boneDepth = new Dictionary<string, int>();
        foreach (var name in boneObjects.Keys)
        {
            int depth = 0;
            var cur = name;
            for (int i = 0; i < 30; i++)
            {
                if (!boneParents.TryGetValue(cur, out var par) || string.IsNullOrEmpty(par)) break;
                depth++; cur = par;
            }
            boneDepth[name] = depth;
        }

        var wobbleRot = new Dictionary<string, float>();
        var wobbleVel = new Dictionary<string, float>();
        foreach (var (name, _) in boneObjects)
        {
            if (name == rootBoneName) continue;
            wobbleRot[name] = dir.X * 10f;
            wobbleVel[name] = dir.X * s.RagdollAngularSpeed * 1.5f
                            + (float)(rng.NextDouble() * s.RagdollAngularSpeed * 0.4f - s.RagdollAngularSpeed * 0.2f);
        }

        float bodyAngVel = dir.X * s.RagdollAngularSpeed * 3f
                         + (float)(rng.NextDouble() * s.RagdollAngularSpeed * 0.4f - s.RagdollAngularSpeed * 0.2f);
        var prevVelVec = mainPos - mainPrev;

        var animState = body.Call("get_animation_state").AsGodotObject();
        try { animState?.Call("set_animation", "idle_loop", true, 0); } catch { }

        // idle_loop 포즈를 skeleton에 반영한 뒤 baseRot 캡처
        body.Call("update_skeleton", 0.0);

        var baseRot = new Dictionary<string, float>();
        foreach (var (name, bone) in boneObjects)
            baseRot[name] = (float)bone.Call("get_rotation").AsDouble();

        foreach (var v in skeleton.Call("get_path_constraints").AsGodotArray())
        {
            var pc = v.AsGodotObject(); if (pc == null) continue;
            pc.Call("set_mix_rotate", 0.0);
            pc.Call("set_mix_x", 0.0);
            pc.Call("set_mix_y", 0.0);
        }

        float bodyAngle  = 0f;
        bool  inHandler  = false;
        Callable handler = Callable.From((GodotObject _) =>
        {
            if (!GodotObject.IsInstanceValid(body) || inHandler) return;
            inHandler = true;

            if (rootBone != null && GodotObject.IsInstanceValid(rootBone))
            {
                var rot = (float)rootBone.Call("get_rotation").AsDouble();
                rootBone.Call("set_rotation", (double)(rot + bodyAngle));
            }

            foreach (var (name, bone) in boneObjects)
            {
                if (name == rootBoneName || !wobbleRot.TryGetValue(name, out var wob)) continue;
                bone.Call("set_rotation", (double)(baseRot[name] + wob));
            }

            inHandler = false;
        });
        body.Connect("before_world_transforms_change", handler);

        void UpdateDebugDraw(BoneDebugDraw dd)
        {
            dd.Lines.Clear();
            dd.Dots.Clear();
            var xform = body.GlobalTransform;
            foreach (var (name, bone) in boneObjects)
            {
                var wx  = (float)bone.Call("get_world_x").AsDouble();
                var wy  = (float)bone.Call("get_world_y").AsDouble();
                var pos = xform * new Vector2(wx, wy);
                dd.Dots.Add((pos, name));
                if (boneParents.TryGetValue(name, out var pName) && boneObjects.TryGetValue(pName, out var pBone))
                {
                    var pPos = xform * new Vector2(
                        (float)pBone.Call("get_world_x").AsDouble(),
                        (float)pBone.Call("get_world_y").AsDouble());
                    dd.Lines.Add((pPos, pos));
                }
            }
            dd.QueueRedraw();
        }

        if (s.FreeMode)
        {
            var viewport    = body.GetViewport();
            bool  dragging  = false;
            Vector2 dragOff = Vector2.Zero;
            float freeAngle = 0f;

            var debugDraw = new BoneDebugDraw();
            debugDraw.ZIndex = 200;
            body.GetParent().AddChild(debugDraw);

            while (GodotObject.IsInstanceValid(body))
            {
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                if (!GodotObject.IsInstanceValid(body)) break;

                var mouse = viewport.GetMousePosition();

                if (Input.IsMouseButtonPressed(MouseButton.Left))
                {
                    if (!dragging) { dragging = true; dragOff = body.GlobalPosition - mouse; }
                    body.GlobalPosition = mouse + dragOff;
                }
                else { dragging = false; }

                if (Input.IsMouseButtonPressed(MouseButton.WheelUp))   freeAngle -= 5f;
                if (Input.IsMouseButtonPressed(MouseButton.WheelDown))  freeAngle += 5f;
                if (Input.IsKeyPressed(Key.R))                          freeAngle  = 0f;

                bodyAngle = freeAngle - (rootBone != null ? (float)rootBone.Call("get_rotation").AsDouble() : 0f);

                body.Call("update_skeleton", 0.016);

                UpdateDebugDraw(debugDraw);
            }

            if (GodotObject.IsInstanceValid(debugDraw)) debugDraw.QueueFree();
            if (body.IsConnected("before_world_transforms_change", handler))
                body.Disconnect("before_world_transforms_change", handler);
            return;
        }

        BoneDebugDraw? physDebug = null;
        if (s.ShowDebugBorder)
        {
            physDebug = new() { ZIndex = 200 };
            body.GetParent().AddChild(physDebug);
        }

        var   screenRect = body.GetViewportRect();
        ulong lastTick   = Time.GetTicksUsec();
        float elapsed    = 0f;
        bool  onFloor    = false;

        while (elapsed < MaxTime && GodotObject.IsInstanceValid(body))
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(body)) break;

            ulong now = Time.GetTicksUsec();
            float dt  = Mathf.Min((now - lastTick) / 1_000_000f, 0.05f);
            lastTick  = now;
            elapsed  += dt;

            // Verlet 위치
            float airDamp = s.ZeroGravity ? 0.998f : AirDamp;
            var vel  = (mainPos - mainPrev) * airDamp;
            mainPrev = mainPos;
            mainPos += vel + gravity * dt * dt;

            var accel = vel - prevVelVec;
            prevVelVec = vel;

            // 텀블링
            bodyAngVel *= Mathf.Pow(BodyDamp, dt * 60f);
            bodyAngle   = bodyAngVel * dt * 60f;

            // 바닥 충돌
            bool justLanded = false;
            if (mainPos.Y > floorY)
            {
                float vx = mainPos.X - mainPrev.X;
                float vy = mainPos.Y - mainPrev.Y;
                mainPos.Y  = floorY;
                mainPrev.Y = floorY + vy * BounceDamp;
                mainPrev.X = mainPos.X - vx * Friction;
                if (!onFloor) { justLanded = true; onFloor = true; bodyAngVel *= 0.4f; }
            }
            else { onFloor = false; }

            if (onFloor) bodyAngVel *= Mathf.Pow(0.80f, dt * 60f);

            // 좌우 벽 충돌
            if (mainPos.X < screenRect.Position.X || mainPos.X > screenRect.End.X)
            {
                float vx   = mainPos.X - mainPrev.X;
                mainPos.X  = Mathf.Clamp(mainPos.X, screenRect.Position.X, screenRect.End.X);
                mainPrev.X = mainPos.X + vx * BounceDamp;
                bodyAngVel = -bodyAngVel * BounceDamp;
            }

            // 천장 충돌 (무중력 시 위로 빠져나가는 것 방지)
            if (s.ZeroGravity && mainPos.Y < screenRect.Position.Y)
            {
                float vy   = mainPos.Y - mainPrev.Y;
                mainPos.Y  = screenRect.Position.Y;
                mainPrev.Y = mainPos.Y + vy * BounceDamp;
                bodyAngVel = -bodyAngVel * BounceDamp;
            }

            // 팔다리 물리
            foreach (var name in wobbleVel.Keys.ToArray())
            {
                int   depth = boneDepth.GetValueOrDefault(name, 1);
                float df    = Mathf.Clamp(0.3f + depth / 3f, 0.3f, 2f);

                wobbleVel[name] -= accel.X * df * 1.5f;
                wobbleVel[name] -= accel.Y * df * 0.5f;

                if (justLanded)
                    wobbleVel[name] += (float)(rng.NextDouble() * s.RagdollAngularSpeed * 80f - s.RagdollAngularSpeed * 40f) * df;

                wobbleVel[name] -= wobbleRot[name] * SpringK * df * dt;
                wobbleVel[name] *= Mathf.Pow(WobbleDamp, dt * 60f);

                wobbleRot[name] += wobbleVel[name] * dt;
                wobbleRot[name] = Math.Clamp(wobbleRot[name], -180f, 180f);
            }

            // Godot 트랜스폼으로 위치 이동
            body.GlobalPosition = mainPos;
            // update_skeleton이 before_world_transforms_change 시그널 발생 → handler에서 뼈 회전 주입
            body.Call("update_skeleton", (double)dt);

            if (physDebug != null && GodotObject.IsInstanceValid(physDebug))
                UpdateDebugDraw(physDebug);

            // 정지 조건
            bool canStop = onFloor || s.ZeroGravity;
            if (canStop)
            {
                var curVel = mainPos - mainPrev;
                if (curVel.LengthSquared() < StopVelSq && Mathf.Abs(bodyAngVel) < StopAngDeg)
                {
                    bool limbsStopped = true;
                    foreach (var name in wobbleVel.Keys)
                        if (Mathf.Abs(wobbleVel[name]) >= StopAngDeg) { limbsStopped = false; break; }
                    if (limbsStopped) break;
                }
            }
        }

        if (!GodotObject.IsInstanceValid(body)) return;

        if (body.IsConnected("before_world_transforms_change", handler))
            body.Disconnect("before_world_transforms_change", handler);

        if (s.ShowDebugBorder) return;

        if (physDebug != null && GodotObject.IsInstanceValid(physDebug)) physDebug.QueueFree();

        var tween = body.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(body, "modulate:a", 0f, 0.5f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(body)) body.QueueFree();
        }));
    }

}

public sealed partial class BoneDebugDraw : Node2D
{
    public readonly List<(Vector2 from, Vector2 to)> Lines = [];
    public readonly List<(Vector2 pos, string name)> Dots  = [];

    public override void _Draw()
    {
        foreach (var (from, to) in Lines)
            DrawLine(from, to, new Color(0f, 1f, 1f, 0.8f), 2f);
        foreach (var (pos, name) in Dots)
        {
            DrawCircle(pos, 4f, Colors.Yellow);
            if (RagdollSettings.Current.ShowBoneNames)
                DrawString(ThemeDB.FallbackFont, pos + new Vector2(6, -4), name,
                    fontSize: 11, modulate: Colors.White);
        }
    }
}

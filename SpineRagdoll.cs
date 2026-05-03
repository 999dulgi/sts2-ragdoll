#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class SpineRagdoll
{
    private const float AirDamp    = 0.99f;
    private const float BounceDamp = 0.4f;
    private const float Friction   = 0.85f;
    private const float BodyDamp   = 0.972f;
    private const float WobbleDamp = 0.88f;
    private const float SpringK    = 3f;
    private const float MaxTime    = 2f;
    private const float StopVelSq  = 1f;
    private const float StopAngDeg = 0.5f;

    public static async void Start(Node2D body, float floorY)
    {
        var s   = RagdollSettings.Current;
        var rng = new Random();

        var mainPos  = body.GlobalPosition;
        var dir      = new Vector2((float)(rng.NextDouble() * 2 - 1), -1f).Normalized();
        float speed  = (float)(rng.NextDouble() * s.Speed * 0.6f + s.Speed);
        var mainPrev = mainPos - dir * speed * 0.016f;
        var gravity  = new Vector2(0, s.Gravity);

        float initSpin = (float)(rng.NextDouble() * s.AngularSpeed * 6f - s.AngularSpeed * 3f);
        initSpin += dir.X * s.AngularSpeed * 2f;

        var tree = body.GetTree();

        // _ready() 실행 대기 (skeleton 초기화 후에 접근)
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (!GodotObject.IsInstanceValid(body)) return;

        body.Call("set_update_mode", 2); // Manual

        var skeleton = body.Call("get_skeleton").AsGodotObject();
        if (skeleton == null) return;

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

        var animState = body.Call("get_animation_state").AsGodotObject();
        try { animState?.Call("set_animation", "idle_loop", true, 0); } catch { }

        var rootBoneName = boneObjects.Keys.FirstOrDefault(n => !boneParents.ContainsKey(n));
        var rootBone     = rootBoneName != null ? boneObjects[rootBoneName] : null;

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

        // 팔다리 wobble 초기값 (루트 뼈 제외)
        var wobbleRot = new Dictionary<string, float>();
        var wobbleVel = new Dictionary<string, float>();
        foreach (var (name, _) in boneObjects)
        {
            if (name == rootBoneName) continue;
            int depth = boneDepth.GetValueOrDefault(name, 1);
            float df  = Mathf.Clamp(0.3f + depth / 3f, 0.3f, 2f);
            wobbleRot[name] = -dir.X * 8f * df;
            wobbleVel[name] = (float)(rng.NextDouble() * s.AngularSpeed * 2f - s.AngularSpeed * 1f) * df;
        }

        float bodyAngle  = 0f;
        float bodyAngVel = initSpin;
        var prevVelVec = mainPos - mainPrev;

        // 애니메이션 기준 회전값 저장 — 매 프레임 절대값으로 덮어씌우기 위해
        var baseRot = new Dictionary<string, float>();
        foreach (var (name, bone) in boneObjects)
            baseRot[name] = (float)bone.Call("get_rotation").AsDouble();

        bool inHandler = false;
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

        ulong lastTick = Time.GetTicksUsec();
        float elapsed  = 0f;
        bool  onFloor  = false;

        while (elapsed < MaxTime && GodotObject.IsInstanceValid(body))
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(body)) break;

            ulong now = Time.GetTicksUsec();
            float dt  = Mathf.Min((now - lastTick) / 1_000_000f, 0.05f);
            lastTick  = now;
            elapsed  += dt;

            // Verlet 위치
            var vel  = (mainPos - mainPrev) * AirDamp;
            mainPrev = mainPos;
            mainPos += vel + gravity * dt * dt;

            var accel = vel - prevVelVec;
            prevVelVec = vel;

            // 텀블링: 이번 프레임 델타만
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
                bodyAngVel *= 0.4f;
                if (!onFloor) { justLanded = true; onFloor = true; }
            }
            else { onFloor = false; }

            // 팔다리 물리
            foreach (var name in wobbleVel.Keys.ToArray())
            {
                int   depth = boneDepth.GetValueOrDefault(name, 1);
                float df    = Mathf.Clamp(0.3f + depth / 3f, 0.3f, 2f);

                wobbleVel[name] -= accel.X * df * 1.5f;
                wobbleVel[name] -= accel.Y * df * 0.5f;

                if (justLanded)
                    wobbleVel[name] += (float)(rng.NextDouble() * s.AngularSpeed * 80f - s.AngularSpeed * 40f) * df;

                wobbleVel[name] -= wobbleRot[name] * SpringK * df * dt;
                wobbleVel[name] *= Mathf.Pow(WobbleDamp, dt * 60f);
                wobbleRot[name] += wobbleVel[name] * dt;
            }

            body.GlobalPosition = mainPos;
            body.Call("update_skeleton", (double)dt);

        }

        if (!GodotObject.IsInstanceValid(body)) return;

        if (body.IsConnected("before_world_transforms_change", handler))
            body.Disconnect("before_world_transforms_change", handler);

        var tween = body.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(body, "modulate:a", 0f, 0.5f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(body)) body.QueueFree();
        }));
    }
}

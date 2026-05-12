#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class SpineRagdoll
{
    private const float AirDamp         = 0.99f;
    private const float BounceDamp      = 0.4f;
    private const float BodyDamp        = 0.972f;
    private const float WobbleDamp      = 0.98f;
    private const float SpringK         = 0.1f;
    private const float MaxTime    = 3f;
    private const float StopVelSq  = 1f;
    private const float StopAngDeg = 0.5f;

    public static async void Start(Node2D body, float floorY, int overDamage = 0, GodotObject? skin = null, string? animName = null, bool keepCorpse = false)
    {
        var s   = RagdollSettings.Current;
        var rng = new Random();

        var mainPos  = body.GlobalPosition;
        float angleRad = Mathf.DegToRad(s.RagdollAngleDirectionDeg + (float)(rng.NextDouble() * s.RagdollAngleSpreadDeg - s.RagdollAngleSpreadDeg / 2f));
        if (keepCorpse) angleRad = Mathf.Pi - angleRad;
        var dir      = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
        float speed  = (float)(rng.NextDouble() * s.RagdollSpeed * 0.6f + s.RagdollSpeed + overDamage * 30f);
        var mainPrev = mainPos - dir * speed * 0.016f;
        var gravity  = s.ZeroGravity ? Vector2.Zero : new Vector2(0, s.RagdollGravity);

        var tree = body.GetTree();
        body.Visible = false;

        // _ready() 실행 대기 (skeleton 초기화 후에 접근)
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (!GodotObject.IsInstanceValid(body)) return;

        body.Visible = true;
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
            float depthScale = 1f + boneDepth.GetValueOrDefault(name, 0) * 0.3f;
            wobbleRot[name] = (dir.X * 12f + dir.Y * 6f) * depthScale;
            wobbleVel[name] = (dir.X * 2.5f + dir.Y * 1.2f) * s.RagdollAngularSpeed * depthScale
                            + (float)(rng.NextDouble() * s.RagdollAngularSpeed * 0.8f - s.RagdollAngularSpeed * 0.4f);
        }

        float bodyAngVel = dir.X * s.RagdollAngularSpeed * 3f
                         + (float)(rng.NextDouble() * s.RagdollAngularSpeed * 0.4f - s.RagdollAngularSpeed * 0.2f);
        var prevVelVec = mainPos - mainPrev;

        if (!string.IsNullOrEmpty(animName))
        {
            var animState = body.Call("get_animation_state").AsGodotObject();
            try { animState?.Call("set_animation", animName, true, 0); } catch { }
        }
        body.Call("update_skeleton", 0.016);

        var baseRot    = new Dictionary<string, float>();
        var currentRot = new Dictionary<string, float>();
        foreach (var (name, bone) in boneObjects)
        {
            float r = (float)bone.Call("get_rotation").AsDouble();
            baseRot[name]    = r;
            currentRot[name] = r;
        }

        // 애니메이션 트랙 제거 → apply()가 매 프레임 뼈/constraint를 덮어쓰는 것 차단
        // constraint mix도 한 번만 0으로 설정하면 animation이 리셋하지 않음
        try
        {
            var animStateForClear = body.Call("get_animation_state").AsGodotObject();
            animStateForClear?.Call("clear_tracks");
            foreach (var v in skeleton.Call("get_ik_constraints").AsGodotArray())
                v.AsGodotObject()?.Call("set_mix", 0.0);
            foreach (var v in skeleton.Call("get_transform_constraints").AsGodotArray())
                v.AsGodotObject()?.Call("set_mix", 0.0);
        }
        catch { }

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

                float target = baseRot[name] + wob;

                // 부모 wob의 40%를 자식에게 전달 → 채찍 연쇄 효과
                if (boneParents.TryGetValue(name, out var parentName) && wobbleRot.TryGetValue(parentName, out var parentWob))
                    target += parentWob * 0.4f;

                // 하위 뼈일수록 느리게 따라옴 → lag 효과
                int   depth       = boneDepth.GetValueOrDefault(name, 0);
                float followSpeed = Mathf.Clamp(1.0f - depth * 0.15f, 0.3f, 1.0f);
                currentRot[name]  = Mathf.Lerp(currentRot[name], target, followSpeed);

                bone.Call("set_rotation", (double)currentRot[name]);
            }
            
            inHandler = false;
        });
        body.Connect("before_world_transforms_change", handler);

        var   screenRect = body.GetViewportRect();
        ulong lastTick   = Time.GetTicksUsec();
        float elapsed    = 0f;
        var   smoothAccel = Vector2.Zero;

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
            prevVelVec  = vel;
            smoothAccel = accel.Lerp(smoothAccel, 0.6f);

            // 텀블링
            bodyAngVel *= Mathf.Pow(BodyDamp, dt * 60f);
            bodyAngle   = bodyAngVel * dt * 60f;

            // 바닥 클램프
            if (mainPos.Y > floorY)
            {
                mainPos.Y  = floorY;
                mainPrev.Y = floorY;
            }

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

                wobbleVel[name] -= smoothAccel.X * df * 5.0f;
                wobbleVel[name] -= smoothAccel.Y * df * 3.0f;
                wobbleVel[name] -= bodyAngVel * df * 4.0f * dt;

                wobbleVel[name] -= wobbleRot[name] * SpringK * df * dt;
                wobbleVel[name] *= Mathf.Pow(WobbleDamp, dt * 60f);

                wobbleRot[name] += wobbleVel[name] * dt;
            }

            // Godot 트랜스폼으로 위치 이동
            body.GlobalPosition = mainPos;
            // update_skeleton이 before_world_transforms_change 시그널 발생 → handler에서 뼈 회전 주입
            body.Call("update_skeleton", (double)dt);

            // 정지 조건
            bool canStop = s.ZeroGravity;
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

        if (keepCorpse) return;

        var tween = body.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(body, "modulate:a", 0f, 0.5f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(body)) body.QueueFree();
        }));
    }

}

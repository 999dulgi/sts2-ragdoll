#nullable enable
using System;
using Godot;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Nodes.Combat;

public static class RagdollPreloader
{
    private record BoneSnapshot(
        Dictionary<string, Transform2D> Transforms,
        Dictionary<string, string> Parents,
        HashSet<string> AttachmentKeys);

    private static readonly Dictionary<ulong, BoneSnapshot> _cache = new();

    public static void CaptureNow(NCreature creature)
    {
        if (!creature.HasSpineAnimation) return;

        var id = creature.GetInstanceId();
        var (transforms, parents, keys) = GetBoneInfo(creature.Body);
        if (transforms.Count > 0)
            _cache[id] = new BoneSnapshot(transforms, parents, keys);
    }

    public static (Dictionary<string, Transform2D>, Dictionary<string, string>, HashSet<string>)?
        Pop(NCreature creature)
    {
        var id = creature.GetInstanceId();
        if (_cache.TryGetValue(id, out var snap))
        {
            _cache.Remove(id);
            return (snap.Transforms, snap.Parents, snap.AttachmentKeys);
        }
        return null;
    }

    public static (Dictionary<string, Transform2D> transforms, Dictionary<string, string> parents, HashSet<string> attachmentKeys)
        GetBoneInfo(Node2D body)
    {
        var transforms = new Dictionary<string, Transform2D>();
        var parents = new Dictionary<string, string>();
        var attachmentKeys = new HashSet<string>();
        try
        {
            var skeleton = body.Call("get_skeleton").AsGodotObject();
            if (skeleton == null) return (transforms, parents, attachmentKeys);

            var slots = skeleton.Call("get_slots").AsGodotArray();
            var boneNameToKey = new Dictionary<string, string>();
            var slotBoneNames = new List<(string key, GodotObject bone)>();

            foreach (var slotVar in slots)
            {
                var slot = slotVar.AsGodotObject();
                if (slot == null) continue;

                var slotData = slot.Call("get_data").AsGodotObject();
                if (slotData == null) continue;

                var bone = slot.Call("get_bone").AsGodotObject();
                if (bone == null) continue;

                var attachName = slotData.Call("get_attachment_name").AsString();
                var slotName   = slotData.Call("get_name").AsString();
                var key = string.IsNullOrEmpty(attachName) ? slotName : attachName;
                if (string.IsNullOrEmpty(key)) continue;
                if (!string.IsNullOrEmpty(attachName)) attachmentKeys.Add(key);

                transforms[key] = bone.Call("get_global_transform").As<Transform2D>();

                var boneData = bone.Call("get_data").AsGodotObject();
                if (boneData != null)
                {
                    var boneName = boneData.Call("get_bone_name").AsString();
                    if (!string.IsNullOrEmpty(boneName))
                        boneNameToKey[boneName] = key;
                }

                slotBoneNames.Add((key, bone));
            }

            foreach (var (key, bone) in slotBoneNames)
            {
                var curBone = bone.Call("get_parent").AsGodotObject();
                while (curBone != null)
                {
                    var curData = curBone.Call("get_data").AsGodotObject();
                    if (curData != null)
                    {
                        var curName = curData.Call("get_bone_name").AsString();
                        if (boneNameToKey.TryGetValue(curName, out var parentKey))
                        {
                            parents[key] = parentKey;
                            break;
                        }
                    }
                    curBone = curBone.Call("get_parent").AsGodotObject();
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[Ragdoll] GetBoneInfo error: {e.Message}");
        }
        return (transforms, parents, attachmentKeys);
    }
}

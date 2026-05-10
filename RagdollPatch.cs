#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

[ModInitializer("ModInit")]
public static class ModStart
{
    public static void ModInit()
    {
        RagdollSettings.Load();
        var harmony = new Harmony("ragdoll");
        harmony.PatchAll();
    }
}

[HarmonyPatch(typeof(NGame), "_Ready")]
public static class NGamePatch
{
    public static void Postfix(NGame __instance)
    {
        RagdollSettingsPanel.Build(__instance);
    }
}

[HarmonyPatch(typeof(NCombatSceneContainer), "_Ready")]
public static class CombatContainerPatch
{
    public static void Postfix(NCombatSceneContainer __instance)
    {
        RagdollPatch.CombatContainer = __instance;
    }
}

[HarmonyPatch(typeof(NModInfoContainer), nameof(NModInfoContainer.Fill))]
public static class ModInfoFillPatch
{
    private const string SettingsBtnName = "RagdollSettingsBtn";

    public static void Postfix(NModInfoContainer __instance, Mod mod)
    {
        if (mod.manifest?.id != "ragdoll") return;

        if (__instance.FindChild(SettingsBtnName, owned: false) != null) return;

        var btn = new Button();
        btn.Name = SettingsBtnName;
        btn.Text = "Ragdoll Settings";
        btn.Pressed += RagdollSettingsPanel.Toggle;
        __instance.AddChild(btn);
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
public static class RagdollPatch
{
    public static Node? CombatContainer;

    private static readonly Dictionary<ulong, string> _animNameCache = [];

    public static void Prefix(NCreature __instance)
    {
        var customConfig = RagdollConfigs.Get(__instance.Entity.ModelId.Entry);
        if (customConfig?.RagdollMode == RagdollMode.Explode)
            RagdollPreloader.CaptureNow(__instance);

        try
        {
            var animState = __instance.Body?.Call("get_animation_state").AsGodotObject();
            var trackEntry = animState?.Call("get_current", 0).AsGodotObject();
            var animName = trackEntry?.Call("get_animation").AsGodotObject()?.Call("get_name").AsString();
            if (!string.IsNullOrEmpty(animName))
                _animNameCache[__instance.GetInstanceId()] = animName;
        }
        catch { }
    }

    public static void Postfix(NCreature __instance)
    {
        var id = __instance.Entity.ModelId.Entry;
        var customConfig = RagdollConfigs.Get(id);
        int overDamage = 0;
        if (RagdollSettings.Current.OverkillForce && OverkillPatch.overkillDict.TryGetValue(id, out var dmg))
            overDamage = dmg;

        if (RagdollSettings.Current.ForcedExplosionMode || customConfig?.RagdollMode == RagdollMode.Explode)
        {
            __instance.Body.Visible = false;
            RagdollExplosion.Spawn(__instance);
            OverkillPatch.overkillDict.Remove(id);
            _animNameCache.Remove(__instance.GetInstanceId());
            return;
        }

        // Ragdoll 모드: Spine skeleton 복사본으로 래그돌 시뮬레이션
        var body = __instance.Body;
        var skelDataRes = body.Get("skeleton_data_res");
        if (skelDataRes.VariantType == Variant.Type.Nil) return;

        var origSkeleton = body.Call("get_skeleton").AsGodotObject();
        var origSkin = origSkeleton?.Call("get_skin").AsGodotObject();

        _animNameCache.TryGetValue(__instance.GetInstanceId(), out var currentAnimName);
        _animNameCache.Remove(__instance.GetInstanceId());

        body.Visible = false;

        var ragdollNode = (Node2D)ClassDB.Instantiate(body.GetClass());
        ragdollNode.Set("skeleton_data_res", skelDataRes);
        ragdollNode.Scale = body.Scale;
        ragdollNode.ZIndex = body.ZIndex;
        ragdollNode.ProcessMode = Node.ProcessModeEnum.Always;

        var partParent = (Node?)CombatContainer ?? body.GetTree().CurrentScene;
        partParent.AddChild(ragdollNode);
        ragdollNode.GlobalPosition = body.GlobalPosition;

        float floorY = __instance.Visuals.Bounds.GlobalPosition.Y + __instance.Visuals.Bounds.Size.Y;
        SpineRagdoll.Start(ragdollNode, floorY, overDamage, origSkin, currentAnimName, keepCorpse: __instance.Entity.IsPlayer);
        if (__instance.Entity.IsPlayer)
            RagdollExplosion.SpawnPlayerRelics(__instance);
        OverkillPatch.overkillDict.Remove(id);
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartReviveAnim))]
public static class RevivePatch
{
    public static void Prefix(NCreature __instance)
    {
        var body = __instance.Body;
        body.Visible = true;
        body.Modulate = Colors.White;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
    new Type[] { typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel) })]
public static class OverkillPatch
{
    public static Dictionary<string, int> overkillDict = new Dictionary<string, int>();
    public static void Postfix(Task<IEnumerable<DamageResult>> __result)
    {
        if (__result == null) return;

        __result.ContinueWith(t =>
        {
            if (t.IsFaulted || t.IsCanceled) return;
            foreach (var result in t.Result)
            {
                if (result.WasTargetKilled && result.OverkillDamage > 0)
                {
                    Creature target = result.Receiver;
                    if (target == null || target.IsPlayer) continue;
                    overkillDict[target.ModelId.Entry] = Math.Clamp(result.OverkillDamage, 0, 300);
                    GD.Print($"{target.ModelId.Entry} : {result.OverkillDamage}");
                }
            }
        });
    }
}


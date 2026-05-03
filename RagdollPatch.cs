#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;

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
    public static void Prefix(NCreature __instance)
    {
        if (__instance.Entity.IsPlayer) return;
        // Explode 모드는 사망 전 뼈 위치 캡처 필요
        var customConfig = RagdollConfigs.Get(__instance.Entity.ModelId.Entry);
        if (customConfig?.RagdollMode == RagdollMode.Explode)
            RagdollPreloader.CaptureNow(__instance);
    }

    public static void Postfix(NCreature __instance)
    {
        if (__instance.Entity.IsPlayer) return;

        var customConfig = RagdollConfigs.Get(__instance.Entity.ModelId.Entry);
        if (customConfig?.RagdollMode == RagdollMode.Explode)
        {
            __instance.Body.Visible = false;
            RagdollExplosion.Spawn(__instance);
            return;
        }

        // Ragdoll 모드: Spine skeleton 복사본으로 래그돌 시뮬레이션
        var body = __instance.Body;
        var skelDataRes = body.Get("skeleton_data_res");
        if (skelDataRes.VariantType == Variant.Type.Nil) return;

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
        SpineRagdoll.Start(ragdollNode, floorY);
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartReviveAnim))]
public static class RevivePatch
{
    public static void Prefix(NCreature __instance)
    {
        if (__instance.Entity.IsPlayer) return;

        var body = __instance.Body;
        body.Visible = true;
        body.Modulate = Colors.White;
    }
}


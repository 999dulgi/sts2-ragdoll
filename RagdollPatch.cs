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
        RagdollSpawner.CombatContainer = __instance;
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
    public static void Postfix(NCreature __instance)
    {
        // 플레이어는 제외
        if (__instance.Entity.IsPlayer) return;

        RagdollSpawner.Spawn(__instance);

        // 파츠가 날아가는 동안 원본 Body 페이드아웃
        var body = __instance.Body;
        var tween = body.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(body, "modulate:a", 0f, 0.2f);
        tween.TweenCallback(Callable.From(() => body.Visible = false));
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

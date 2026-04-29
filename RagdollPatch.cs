#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Combat;

[ModInitializer("ModInit")]
public static class ModStart
{
    public static void ModInit()
    {
        var harmony = new Harmony("ragdoll");
        harmony.PatchAll();
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

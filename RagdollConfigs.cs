#nullable enable
using System;
using System.Collections.Generic;
using Godot;

public enum RagdollMode
{
    Ragdoll = 0,
    Explode = 1
}

public static class RagdollConfigs
{
    public sealed class Config
    {
        public RagdollMode RagdollMode { get; } = RagdollMode.Ragdoll;
        public HashSet<string> ExcludedRegions { get; }
        public Dictionary<string, Action<Node2D, Sprite2D>> Effects { get; }
        public Dictionary<string, Action<Node2D, Sprite2D>> FinishEffects { get; }

        public Config(RagdollMode ragdollMode = RagdollMode.Ragdoll, string[]? exclude = null, Dictionary<string, Action<Node2D, Sprite2D>>? effects = null, Dictionary<string, Action<Node2D, Sprite2D>>? finishEffects = null)
        {
            RagdollMode = ragdollMode;
            ExcludedRegions = exclude != null ? new HashSet<string>(exclude) : new();
            Effects = effects ?? new();
            FinishEffects = finishEffects ?? new();
        }
    }

    private static readonly Dictionary<string, Config> _configs = new()
    {
        // 예시:
        // ["axebot"] = new Config(
        //     exclude: new[] { "bg", "shadow" },
        //     effects: new Dictionary<string, Action<Node2D, Sprite2D>>
        //     {
        //         ["head"] = (node, sprite) => { /* 효과 */ },
        //     }
        // ),
        ["ASSASSIN_RUBY_RAIDER"] = new Config(
            exclude: new[] { "shadow", "death_wrinkle" }
        ),
        ["AXE_RUBY_RAIDER"] = new Config(
            exclude: new[] { "shadow", "shield_inverted" }
        ),
        ["AXEBOT"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "special_swish", "attack_swish_1", "attack_swish_2", "spark 1", "spark burst 1", "thrust_halo", "tummy glow" },
            finishEffects: new Dictionary<string, Action<Node2D, Sprite2D>>
            {
                ["bod"] = (node, sprite) =>
                {
                    var scene = ResourceLoader.Load<PackedScene>("res://scenes/vfx/vfx_fire_smoke_puff.tscn");
                    if (scene == null) { GD.PrintErr("[Ragdoll] vfx_fire_smoke_puff not found"); return; }
                    try
                    {
                        var vfx = scene.Instantiate<Node2D>();
                        var root = node.GetTree().CurrentScene;
                        root.AddChild(vfx);
                        vfx.GlobalPosition = node.GlobalPosition;
                    }
                    catch (Exception e) { GD.PrintErr($"[Ragdoll] vfx error: {e.Message}"); }
                }
            }
        ),
        ["BATTLE_FRIEND_V1"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "eye_sparkle", "orb_energy" }
        ),
        ["BATTLE_FRIEND_V2"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "eye_sparkle", "orb_energy" }
        ),
        ["BATTLE_FRIEND_V3"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "eye_sparkle", "orb_energy" }
        ),
        ["BOWLBUG_ROCK"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "bang", "iris", "iris_hurt", "spit", "dirt"}
        ),
        ["BOWLBUG_SILK"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "bang", "iris", "iris_hurt", "spit", "dirt" }
        ),
        ["BOWLBUG_NECTAR"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "bang", "iris", "iris_hurt", "spit", "dirt" }
        ),
        ["BOWLBUG_EGG"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "bang", "iris", "iris_hurt", "spit", "dirt"}
        ),
        ["BRUTE_RUBY_RAIDER"] = new Config(
            exclude: new[] { "shadow", "hair shadow" }
        ),
        ["BYGONE_EFFIGY"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "blur_v1", "head", "bod", "l arm", "l hand", "r hand", "attack shadow" }
        ),
        ["BYRDONIS"] = new Config(
            exclude: new[] { "shadow", "anger" }
        ),
        ["CEREMONIAL_BEAST"] = new Config(
            exclude: new[] { "shadow", "neck_glow", "shine_line", "top antler glow", "bottom antler glow", "back bod glow", "death_glow", "blastwave", 
            "R front leg top glow", "R font leg bottom glow", "R back leg bottom glow", "R back leg top glow", "L front leg top glow", 
            "L front leg bottom glow", "L back leg bottom glow", "L back leg top glow", "eye anim 2", "eye anim 3", "eye anim 4", "eye anim 5",
            "slide_puff", "FreezeFrame", "FreezeFrameWhite"}
        ),
        ["CHOMPER"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "orb", "orb_add_glow", "orbc crackz", "orb_glow_shapes", "orb shine 1", "orb shine 2" },
            finishEffects: new Dictionary<string, Action<Node2D, Sprite2D>>
            {
                ["orb_cracked"] = (node, sprite) =>
                {
                    var scene = ResourceLoader.Load<PackedScene>("res://scenes/vfx/vfx_hyperbeam_impact.tscn"); 
                    if (scene == null) { GD.PrintErr("[Ragdoll] vfx_hyperbeam_impact.tscn not found"); return; }
                    try
                    {
                        var vfx = scene.Instantiate<Node2D>();
                        var root = node.GetTree().CurrentScene;
                        root.AddChild(vfx);
                        vfx.GlobalPosition = node.GlobalPosition;
                    }
                    catch (Exception e) { GD.PrintErr($"[Ragdoll] vfx error: {e.Message}"); }
                }
            }
        ),
        ["CORPSE_SLUG"] = new Config(
            exclude: new[] { "shadow", "eat" }
        ),
        ["CROSSBOW_RUBY_RAIDER"] = new Config(
            exclude: new[] { "shadow", "string_tight" }
        ),
        ["CUBEX_CONSTRUCT"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "top light", "bottom light", "base_light", "orb_glow", "orb glow 1", "orb glow 2", "orb glow 3", "orb_glow_shapes", "crack",
            "moss3/top", "moss3/bottom", "moss2/top", "moss2/bottom" },
            finishEffects: new Dictionary<string, Action<Node2D, Sprite2D>>
            {
                ["orb"] = (node, sprite) =>
                {
                    var scene = ResourceLoader.Load<PackedScene>("res://scenes/vfx/vfx_hyperbeam_impact.tscn"); 
                    if (scene == null) { GD.PrintErr("[Ragdoll] vfx_hyperbeam_impact.tscn not found"); return; }
                    try
                    {
                        var vfx = scene.Instantiate<Node2D>();
                        var root = node.GetTree().CurrentScene;
                        root.AddChild(vfx);
                        vfx.GlobalPosition = node.GlobalPosition;
                    }
                    catch (Exception e) { GD.PrintErr($"[Ragdoll] vfx error: {e.Message}"); }
                }
            }
        ),
        ["CALCIFIED_CULTIST"] = new Config(
            exclude: new[] { "shadow", "slash", "drip_vfx_1", "drip_vfx_2", "drop_vfx", "dead_body", "dead_body_skin_1", "dead_butt" }
        ),
        ["DAMP_CULTIST"] = new Config(
            exclude: new[] { "shadow", "slash", "drip_vfx_1", "drip_vfx_2", "drop_vfx", "dead_body", "dead_body_skin_1", "dead_butt" }
        ),
        ["DECIMILLIPEDE_SEGMENT_FRONT"] = new Config(
            exclude: new[] { "shadow", "shell", "shell glow", "shell crack", "shell crack glow" }
        ),
        ["DECIMILLIPEDE_SEGMENT_MIDDLE"] = new Config(
            exclude: new[] { "shadow", "shell", "shell glow", "shell crack", "shell crack glow" }
        ),
        ["DECIMILLIPEDE_SEGMENT_BACK"] = new Config(
            exclude: new[] { "shadow", "shell", "shell glow", "shell crack", "shell crack glow" }
        ),
        ["DEVOTED_SCULPTOR"] = new Config(
            exclude: new[] { "shadow", "hood feathers 1", "hood feathers 2", "hood feathers 3"} //이름이 feather이거나 wing이면 천천히 떨어지게 만들기
        ),
        ["ENTOMANCER"] = new Config(
            exclude: new[] { "shadow", "slam_blur" }
        ),
        ["FABRICATOR"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "handles", "handles 2", "dust_cloud_00", "dust_cloud_01", "dust_cloud_02" }
        ),
        ["FAT_GREMLIN"] = new Config(
            exclude: new[] { "shadow", "bag_back" }
        ),
        ["FLAIL_KNIGHT"] = new Config(
            exclude: new[] { "shadow", "circ_swishes", "circ_blurs_1", "circ_blurs_2", "circ_rotator", "ram_swish", "ram_slam", "breaker_swish" }
        ),
        ["FLYCONID"] = new Config(
            exclude: new[] { "shadow", "pffft" }
        ),
        ["FOGMOG"] = new Config(
            exclude: new[] { "shadow", "blink 1", "blink 1 copy", "blink 1 copy 2", "blink 1 copy 3", "cel spore A 1", "cel spore A 2", "cel spore A 3", "cel spore A 4", "cel spore A 5",
            "cel spore A 6", "cel spore B 1", "cel spore B 2", "cel spore B 3", "cel spore B 4", "cel spore B 5", "cel spore B 6", "cel spore C 1", "cel spore C 2", "cel spore C 3", 
            "cel spore C 4", "cel spore C 5", "cel spore C 6", "cel spore D 1", "cel spore D 2", "cel spore D 3", "cel spore D 4", "cel spore D 5", "cel spore D 6",
            "spore bits", "spore bits 2", "spore bits 3", "spore bits 2 copy", "spore cloud 1", "spore cloud 2", "spore cloud 3" }
        ),
        ["FOSSIL_STALKER"] = new Config(
            exclude: new[] { "shadow" }
        ),
        ["FROG_KNIGHT"] = new Config(
            exclude: new[] { "shadow", "closed_eye" }
        ),
        ["FUZZY_WURM_CRAWLER"] = new Config(
            exclude: new[] { "shadow", "vapor_ring", "acid wad", "acid shine", "mouth back", "teeth2" }
        ),
        ["GAS_BOMB"] = new Config(
            exclude: new[] { "shadow", "gas_bomb_blast", "zap1-1", "zap1-2", "zap1-3", "zap1-4", "zap1-5", "zap 2-1", "zap 2-2", "zap 2-3", "zap 2-4", "zap 2-5" }
        ),
        ["GLOBE_HEAD"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "burn smudge 1", "burn smudge 2", "burn smudge 3", "head zaps short", "head zaps 0", "head zaps branched", "lightning branch1", "lightning branch2", 
            "lightning branch3", "lightning branch4", "lightning main1", "lightning main2", "lightning main3", "lightning main4", "shine", "smoke", "smoke 2", "smoke 3", "smoke 4",
            "zap flash", "zap glow" },
            finishEffects: new Dictionary<string, Action<Node2D, Sprite2D>>
            {
                ["orb"] = (node, sprite) =>
                {
                    var scene = ResourceLoader.Load<PackedScene>("res://scenes/vfx/vfx_hyperbeam_impact.tscn");
                    if (scene == null) { GD.PrintErr("[Ragdoll] vfx_hyperbeam_impact.tscn not found"); return; }
                    try
                    {
                        var vfx = scene.Instantiate<Node2D>();
                        var root = node.GetTree().CurrentScene;
                        root.AddChild(vfx);
                        vfx.GlobalPosition = node.GlobalPosition;
                    }
                    catch (Exception e) { GD.PrintErr($"[Ragdoll] vfx error: {e.Message}"); }
                }
            }
        ),
        ["GREMLIN_MERC"] = new Config(
            exclude: new[] { "shadow", "attack_slash" }
        ),
        ["GUARDBOT"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "shiel grid", "shieldFX", "ditch line", "sield_shine" }
        ),
        ["HAUNTED_SHIP"] = new Config(
            exclude: new[] { "shadow", "attack_swish", "attack_triple_swish" }
        ),
        ["HUNTER_KILLER"] = new Config(
            exclude: new[] { "shadow", "tail_shading" }
        ),
        ["HAUNTED_SHIP"] = new Config(
            exclude: new[] { "shadow", "attack_swish", "attack_triple_swish" }
        ),
        ["INFESTED_PRISM"] = new Config(
            exclude: new[] { "shadow", "flash_out", "glow_body", "arm1_glow", "arm2_glow", "arm3_glow", "hit_swish", "shine1", "shine2", "shine3" }
        ),
        ["INKLET"] = new Config(
            exclude: new[] { "shadow", "swish1", "attack_triple_swish" }
        ),
        ["KIN_FOLLOWER"] = new Config(
            exclude: new[] { "shadow", "dirt1", "boomerang_1", "swing_slash_bigger"}
        ),
        ["KIN_PRIEST"] = new Config(
            exclude: new[] { "shadow", "lser_glow", "laser_glow_core" }
        ),
        ["KNOWLEDGE_DEMON"] = new Config(
            exclude: new[] { "shadow", "scripty_circle_1", "scripty_circle_2", "scripty_circle_shine", "OOO_sign", "whip" }
        ),
        ["LAGAVULIN_MATRIARCH"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "irises_open", "blur", "body_goo_1", "body_goo_2", "body_goo_3", "claw_hole", "claw_hole_2", "eyelid_closed", "eyes_open", "eyes_closed", "veins", "tentacle" }
        ),
        ["LEAF_SLIME_M"] = new Config(
            exclude: new[] { "shadow", "shine", "projectile" }
        ),
        ["LEAF_SLIME_S"] = new Config(
            exclude: new[] { "shadow", "shine", "projectile_small", "lips" }
        ),
        ["LIVING_SHIELD"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "zzzt1", "zzzt2", "explosion/explode0000", "explosion/explode0001", "explosion/explode0002", "explosion/explode0003", "explosion/explode0004", 
            "explosion/explode0005", "explosion/explode0006", "explosion/explode0007", "explosion/explode0008", "explosion/explode0009", "explosion/explode0010" ,
            "explosion/explode0011", "explosion/explode0012", "explosion/explode0013", "Layer 179"}
        ),
        ["LIVING_FOG"] = new Config(
            exclude: new[] { "shadow", "zap1-1", "zap1-2", "zap1-3", "zap1-4", "zap1-5", "zap 2-1", "zap 2-2", "zap 2-3", "zap 2-4", "zap 2-5" }
        ),
        ["LOUSE_PROGENITOR"] = new Config(
            exclude: new[] { "shadow", "web" }
        ),
        ["MAGI_KNIGHT"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "bomb", "bombspark", "fire shine", "head_fire_spark" }
        ),
        ["MAWLER"] = new Config(
            exclude: new[] { "shadow", "attack_slash" }
        ),
        ["MECHA_KNIGHT"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "slash", "zzzt1", "zzzt2", "orb_glow" },
            finishEffects: new Dictionary<string, Action<Node2D, Sprite2D>>
            {
                ["orb"] = (node, sprite) =>
                {
                    var scene = ResourceLoader.Load<PackedScene>("res://scenes/vfx/vfx_hyperbeam_impact.tscn"); 
                    if (scene == null) { GD.PrintErr("[Ragdoll] vfx_hyperbeam_impact.tscn not found"); return; }
                    try
                    {
                        var vfx = scene.Instantiate<Node2D>();
                        var root = node.GetTree().CurrentScene;
                        root.AddChild(vfx);
                        vfx.GlobalPosition = node.GlobalPosition;
                    }
                    catch (Exception e) { GD.PrintErr($"[Ragdoll] vfx error: {e.Message}"); }
                }
            }
        ),
        ["MYTE"] = new Config(
            exclude: new[] { "shadow", "projectile", "toxic_drop", "toxic_goo00", "toxic_goo01", "toxic_goo02", "toxic_goo03", "toxic_goo04", "toxic_goo05", "toxic_goo06", "toxic_goo07" }
        ),
        ["NIBBIT"] = new Config(
            exclude: new[] { "shadow", "tail shade patch" }
        ),
        ["NOISEBOT"] = new Config(
            exclude: new[] { "shadow", "wave", "ditch line" }
        ),
        ["OSTY"] = new Config(
            exclude: new[] { "shadow", "glow", "shockwave", "glob_ring", "glob_middle", "glob_index", "glob_palm", "glob_pinky", "glob_thumb" }
        ),
        ["OVICOPTER"] = new Config(
            exclude: new[] { "shadow", "wing_fl", "wing_fl_2", "wing_fr", "wing_fr_2" }
        ),
        ["OWL_MAGISTRATE"] = new Config(
            exclude: new[] { "shadow", "shockwave", "peck1", "peck2" }
        ),
        ["PARAFRIGHT"] = new Config(
            exclude: new[] { "shadow", "body_blurred", "wing_top_blurred", "wing_bottom_blurred", "head_blurred" }
        ),
        ["PHANTASMAL_GARDENER"] = new Config(
            exclude: new[] { "shadow", "hole_bottom", "hole_top", "striker thing" }
        ),
        ["PHROG_PARASITE"] = new Config(
            exclude: new[] { "shadow" }
        ),
        ["PUNCH_CONSTRUCT"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "die_swish", "punch_swish", "head_orb_glow", "orb_add", "ding1" },
            finishEffects: new Dictionary<string, Action<Node2D, Sprite2D>>
            {
                ["head_orb"] = (node, sprite) =>
                {
                    var scene = ResourceLoader.Load<PackedScene>("res://scenes/vfx/vfx_hyperbeam_impact.tscn");
                    if (scene == null) { GD.PrintErr("[Ragdoll] vfx_hyperbeam_impact.tscn not found"); return; }
                    try
                    {
                        var vfx = scene.Instantiate<Node2D>();
                        var root = node.GetTree().CurrentScene;
                        root.AddChild(vfx);
                        vfx.GlobalPosition = node.GlobalPosition;
                    }
                    catch (Exception e) { GD.PrintErr($"[Ragdoll] vfx error: {e.Message}"); }
                }
            }
        ),
        ["QUEEN"] = new Config(
            exclude: new[] { "shadow", "gem_shine", "staff_shadow" }
        ),
        ["SCROLL_OF_BITING"] = new Config(
            exclude: new[] { "shadow", "darkswirl" }
        ),
        ["SEAPUNK"] = new Config(
            exclude: new[] { "shadow", "huge_bubble", "medium_bubble" }
        ),
        ["SEWER_CLAM"] = new Config(
            exclude: new[] { "shadow", "open_mouth_back", "mouth back", "darkness", "blast wave" }
        ),
        ["SHRINKER_BEETLE"] = new Config(
            exclude: new[] { "shadow", "glowy bits", "glowy bits copy", "bod_death", "wings1_death", "head_death" }
        ),
        ["SKULKING_COLONY"] = new Config(
            exclude: new[] { "shadow", "back_slash" }
        ),
        ["SLIMED_BERSERKER"] = new Config(
            exclude: new[] { "shadow", "body bubbles", "arm veins R", "arm veins L", "bubbles 1", "bubbles 2", "bubbles 3", "bubbles 4", "bubbles 5", "bubbles 6" }
        ),
        ["SLITHERING_STRANGLER"] = new Config(
            exclude: new[] { "shadow", "headbutt_blur", "shine" }
        ),
        ["SLUDGE_SPINNER"] = new Config(
            exclude: new[] { "shadow", "skitter_blur" }
        ),
        ["SLUMBERING_BEETLE"] = new Config(
            exclude: new[] { "shadow", "curled/smear 2", "curled/smear", "curled/smear 3", "slumberng_dirt" }
        ),
        ["SNAPPING_JAXFRUIT"] = new Config(
            exclude: new[] { "shadow", "purple sac", "purple_sac_brightness", "add glows", "orb_shine_1", "orb_shine_2" },
            finishEffects: new Dictionary<string, Action<Node2D, Sprite2D>>
            {
                ["purple_sac"] = (node, sprite) =>
                {
                    var scene = ResourceLoader.Load<PackedScene>("res://scenes/vfx/vfx_hyperbeam_impact.tscn");
                    if (scene == null) { GD.PrintErr("[Ragdoll] vfx_hyperbeam_impact.tscn not found"); return; }
                    try
                    {
                        var vfx = scene.Instantiate<Node2D>();
                        var root = node.GetTree().CurrentScene;
                        root.AddChild(vfx);
                        vfx.GlobalPosition = node.GlobalPosition;
                    }
                    catch (Exception e) { GD.PrintErr($"[Ragdoll] vfx error: {e.Message}"); }
                }
            }
        ),
        ["SNEAKY_GREMLIN"] = new Config(
            exclude: new[] { "shadow" }
        ),
        ["SOUL_FYSH"] = new Config(
            exclude: new[] { "shadow", "soundwave/soundwave", "small_bubble" }
        ),
        ["SOUL_NEXUS"] = new Config(
            exclude: new[] { "shadow", "glowie", "straight soul", "light squig", "neutral_soul" }
        ),
        ["SPECTRAL_KNIGHT"] = new Config(
            exclude: new[] { "main shadow", "sword shadow", "debuff_whoosh", "sword swish" }
        ),
        ["SPINY_TOAD"] = new Config(
            exclude: new[] { "shadow", "puff_seq/spike_burst_anim00", "puff_seq/spike_burst_anim01", "puff_seq/spike_burst_anim02",
            "puff_seq/spike_burst_anim03", "puff_seq/spike_burst_anim04", "spit" }
        ),
        ["STABBOT"] = new Config(
            exclude: new[] { "ditch line" }
        ),
        ["TERROR_EEL"] = new Config(
            exclude: new[] { "shadow", "body_spots", "mouth back", "vulernable_line", "eye shine" }
        ),
        ["TEST_SUBJECT"] = new Config(
            exclude: new[] { "shadow", "death_glow", "atack_strobe", "slash_a1", "slash_a2", "slash_a3", "attack_zap_a1", 
            "attack_zap_a2", "attack_zap_a3", "attack_zap_a4", "claw_scratch", "cord_1", "cord_2" }
        ),
        ["THE_FORGOTTEN"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "mask_1_shadow", "glow wheel", "glow wheel thick", "cracks", "smoke1/smoke mesh" }
        ),
        ["THE_INSATIABLE"] = new Config(
            exclude: new[] { "suck streak" }
        ),
        ["THE_LOST"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "glow wheel", "smoke1/smoke mesh", "cracks_bottom", "mask_2_shadow", "mask_2" }
        ),
        ["THE_OBSCURA"] = new Config(
            exclude: new[] { "shadow", "attack_slash", "projector beam", "projector glare" }
        ),
        ["THIEVING_HOPPER"] = new Config(
            exclude: new[] { "shadow", "wing_blur_1", "wing_blur_2", "extend-o-tenna" }
        ),
        ["TOADPOLE"] = new Config(
            exclude: new[] { "shadow", "thron_shot", "twirl_swish", "smack_swish" }
        ),
        ["TORCH_HEAD_AMALGAM"] = new Config(
            exclude: new[] { "shadow", "glare", "laser_base", "zurp", "beam_base" }
        ),
        ["TOUGH_EGG"] = new Config(
            exclude: new[] { "shadow", "shadow b", "open egg", "spots", "shine", "spots b", "shine b" }
        ),
        ["TRACKER_RUBY_RAIDER"] = new Config(
            exclude: new[] { "tracker/tracker_shadow", "boar_back/boar_shadow" }
        ),
        ["TUNNELER"] = new Config(
            exclude: new[] { "shadow", "dirt back", "dirt top" }
        ),
        ["TURRET_OPERATOR"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "muzzle_flash_2", "muzzle_flash_1", "idle_orb", "smear 2", "smear 1", "smear 3" },
            finishEffects: new Dictionary<string, Action<Node2D, Sprite2D>>
            {
                ["orb"] = (node, sprite) =>
                {
                    var scene = ResourceLoader.Load<PackedScene>("res://scenes/vfx/vfx_hyperbeam_impact.tscn");
                    if (scene == null) { GD.PrintErr("[Ragdoll] vfx_hyperbeam_impact.tscn not found"); return; }
                    try
                    {
                        var vfx = scene.Instantiate<Node2D>();
                        var root = node.GetTree().CurrentScene;
                        root.AddChild(vfx);
                        vfx.GlobalPosition = node.GlobalPosition;
                    }
                    catch (Exception e) { GD.PrintErr($"[Ragdoll] vfx error: {e.Message}"); }
                }
            }
        ),
        ["TWIG_SLIME_M"] = new Config(
            exclude: new[] { "shadow", "layer 19", "eyes blink copy 2", "eyes blink copy 3", "eyes blink copy 4", "eyes blink copy 5", "eyes blink copy 6", "eyes blink copy 7",
             "eyes blink copy 8", "eyes blink copy 9", "eyes blink copy 10", "eyes blink copy 11", "projectile" }
        ),
        ["TWIG_SLIME_S"] = new Config(
            exclude: new[] { "shadow", "bod shine", "eyes blink 01", "eyes blink 02", "eyes blink 03", "eyes blink 04", "eyes blink 05", "eyes blink 06", "eyes blink 07", 
            "eyes blink 08", "eyes blink 09", "eyes blink 10", "eyes blink 11", "eyes blink 12", "eyes blink 13", "eyes blink 14", "eyes blink 15", "eyes blink 16",
            "eyes blink 17", "eyes blink 18", "eyes blink 19", "eyes blink 20", "eyes blink 21", "eyes blink 22" }
        ),
        ["TWO_TAILED_RAT"] = new Config(
            exclude: new[] { "shadow", "dead top", "dead bottom", "barnacles1/barnacles", "barnacles2/barnacles", "barnacles3/barnacles", "slash_1", "slash_2" }
        ),
        ["VANTOM"] = new Config(
            exclude: new[] { "shadow", "megashine" }
        ),
        ["VINE_SHAMBLER"] = new Config(
            exclude: new[] { "shadow", "straighe vine" }
        ),
        ["WATERFALL_GIANT"] = new Config(
            ragdollMode: RagdollMode.Explode,
            exclude: new[] { "shadow", "back_splash_00", "back_splash_01", "back_splash_02", "back_splash_03", "back_splash_04", "back_splash_05", "back_splash_06", 
            "back_splash_07", "front_splash_00", "front_splash_01", "front_splash_02", "front_splash_03", "front_splash_04", "front_splash_05", "front_splash_06", "front_splash_07",
            "front_splash_08", "legBSplash_1", "legBSplash_2", "legBSplash_3", "legBSplash_4", "legBSplash_5", "legBSplash_6", "legFSplash_1", "legFSplash_2", 
            "legFSplash_3", "legFSplash_4", "legFSplash_5", "legFSplash_6", "legFShadow_1", "legFShadow_2", "legFShadow_3", "legFShadow_4", "legFShadow_5", "legFShadow_6",
            "steam_explosion", "" }
        ),
        ["WRIGGLER"] = new Config(
            exclude: new[] { "shadow" }
        ),
        ["ZAPBOT"] = new Config(
            exclude: new[] { "shadow", "fly_lines_l", "fly_lines_r", "zap", "zap_off", "zap_glow_l", "zap_glow_r", "ditch line" }
        ),
    };

    public static Config? Get(string? monsterId)
    {
        if (monsterId == null) return null;
        _configs.TryGetValue(monsterId, out var config);
        return config;
    }
}

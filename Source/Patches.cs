
using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace ToggleableShields
{
	public class StaticShield : DefModExtension { }
	
	[StaticConstructorOnStartup]
	public static class Setup
	{
		static Setup()
		{
			new Harmony("Owlchemist.ToggleableShields").PatchAll();
		}
	}
	//Allows guns to work for disabled shields
	[HarmonyPatch(typeof(CompShield), nameof(CompShield.CompAllowVerbCast))]
	class Patch_CompShield_CompAllowVerbCast
	{
		static bool Postfix(bool __result, CompShield __instance)
		{
			return __result || __instance.energy < 0f;
		}
    }

	//Makes the shield not render while toggled off
	[HarmonyPatch(typeof(CompShield), nameof(CompShield.ShouldDisplay), MethodType.Getter)]
	class Patch_CompShield_ShouldDisplay
	{
		static bool Prefix(CompShield __instance, ref bool __result)
		{
			return __result = __instance.energy >= 0f;
		}
    }

	//Shields have ticks and each tick they try to recharge. This just sets to recharge to 0 if it's disabled (as defined by -1 energy)
	[HarmonyPatch(typeof(CompShield), nameof(CompShield.CompTick))]
	class Patch_CompShield_CompTick
	{
		static bool Prefix(CompShield __instance)
		{
			return __instance.energy >= 0f;
		}
    }

	[HarmonyPatch(typeof(Gizmo_EnergyShieldStatus), nameof(Gizmo_EnergyShieldStatus.GizmoOnGUI))]
	class Patch_Gizmo_EnergyShieldStatus_GizmoOnGUI
	{
		static string shieldPersonalTip = "ShieldPersonalTip".Translate();
		static string shieldInBuilt = "ShieldInbuilt".Translate().Resolve();
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            yield return new CodeInstruction(OpCodes.Ldarg_0);
			yield return new CodeInstruction(OpCodes.Ldarg_1);
			yield return new CodeInstruction(OpCodes.Ldarg_2);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_Gizmo_EnergyShieldStatus_GizmoOnGUI), nameof(GizmoOnGUI)));
            yield return new CodeInstruction(OpCodes.Ret);
        }
		public static GizmoResult GizmoOnGUI(Gizmo_EnergyShieldStatus instance, Vector2 topLeft, float maxWidth)
		{
			CompShield shield = instance.shield;
			var shieldEnergy = shield.Energy;
			var shieldEnergyMax = shield.parent.GetStatValue(StatDefOf.EnergyShieldEnergyMax, true, -1);
			bool shieldOn = shieldEnergy >= 0f;
			bool toggleable = !shield.parent.def.HasModExtension<StaticShield>();
			
			//Outer rect
			Rect rect = new Rect(topLeft.x, topLeft.y, instance.GetWidth(maxWidth), 75f);
			Widgets.DrawWindowBackground(rect);

			//Shield label
			Rect rectInner = rect.ContractedBy(6f);
			Widgets.Label(new Rect(rectInner.x, rectInner.y, rectInner.width, rect.height / 2f), shield.IsApparel ? shield.parent.LabelCap : shieldInBuilt);

			//Fill Bar
			Rect rectFillableBar;
			if (toggleable)
			{
				rectFillableBar = new Rect(rectInner.x, rectInner.y + rectInner.height / 2f, rectInner.width - 28f, rectInner.height / 2f);
				
				//Checkbox to right of fill bar
				if (Widgets.ButtonImage(new Rect(rectFillableBar.xMax + 4f, rectFillableBar.y + 4f, 24f, 24f), shieldOn ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex, true))
				{
					ToggleShields(shieldOn, shield); //Moved into its own method for easier MP support
				}
			}
			else
			{
				rectFillableBar = new Rect(rectInner.x, rectInner.y + rectInner.height / 2f, rectInner.width, rectInner.height / 2f);
			}
			Widgets.FillableBar(rectFillableBar, shieldEnergy / System.Math.Max(1f, shieldEnergyMax), Gizmo_EnergyShieldStatus.FullShieldBarTex, Gizmo_EnergyShieldStatus.EmptyShieldBarTex, false);

			//Text over fill bar
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rectFillableBar, (shieldEnergy * 100f).ToString("F0") + " / " + (shieldEnergyMax * 100f).ToString("F0"));
			Text.Anchor = TextAnchor.UpperLeft;

			//Tooltip
			TooltipHandler.TipRegion(rectInner, shieldPersonalTip);

			return new GizmoResult(GizmoState.Clear);
		}

		public static void ToggleShields(bool shieldOn, CompShield shield)
		{
			if (!shieldOn) 
			{
				shield.energy = 0;
				shield.ticksToReset = shield.Props.startingTicksToReset;
			}
			else
			{
				shield.Break();
				shield.energy = -0.0001f;
			}
		}
	}

	//Blank out this method as it is now obsolete
	[HarmonyPatch(typeof(WorkGiver_HunterHunt), nameof(WorkGiver_HunterHunt.HasShieldAndRangedWeapon))]
	class Patch_WorkGiver_HunterHunt_HasShieldAndRangedWeapon
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            yield return new CodeInstruction(OpCodes.Ldc_I4_0);
            yield return new CodeInstruction(OpCodes.Ret);
        }
	}
	//Blank out this method as it is now obsolete
	[HarmonyPatch(typeof(Alert_ShieldUserHasRangedWeapon), nameof(Alert_ShieldUserHasRangedWeapon.ShieldUsersWithRangedWeapon), MethodType.Getter)]
	class Patch_Alert_ShieldUserHasRangedWeapon_HasShieldAndRangedWeapon
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            yield return new CodeInstruction(OpCodes.Ldnull);
			yield return new CodeInstruction(OpCodes.Ret);
        }
	}

	//This patches how pawns would normally ignore shield belts if they're using a gun when they go and equip things from storage
	[HarmonyPatch(typeof(JobGiver_OptimizeApparel), nameof(JobGiver_OptimizeApparel.ApparelScoreGain))]
	class Patch_JobGiver_OptimizeApparel_ApparelScoreGain
	{
		static bool Prefix(Pawn pawn, Apparel ap, ref float __result, List<float> wornScoresCache)
		{
			if (ap.TryGetComp<CompShield>() != null && pawn.equipment.Primary != null && pawn.equipment.Primary.def.IsWeaponUsingProjectiles)
			{
				//This code is temporarily. It's just a copy-paste done fast to push as a hotfix for now. Replacing with a transpiler is probably the cleaner solution.
				float num = JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, ap);
				List<Apparel> wornApparel = pawn.apparel.WornApparel;
				bool flag = false;
				for (int i = wornApparel.Count; i-- > 0;)
				{
					var apparel = wornApparel[i];
					if (!ApparelUtility.CanWearTogether(apparel.def, ap.def, pawn.RaceProps.body))
					{
						if (!pawn.outfits.forcedHandler.AllowedToAutomaticallyDrop(apparel) || pawn.apparel.IsLocked(apparel))
						{
							__result = -1000f;
							return false;
						}
						num -= wornScoresCache[i];
						flag = true;
					}
				}
				if (!flag) num *= 10f;
				__result = num;
				return false;
			}
			return true;
		}
	}

	//If a gun user equips a shield belt while they're using a gun, the belt will default to off
	[HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear))]
	class Patch_Pawn_ApparelTracker_Wear
	{
		static void Prefix(Apparel newApparel, Pawn_ApparelTracker __instance)
		{
			if
			(
				newApparel.TryGetComp<CompShield>() is CompShield shieldBelt && //Is a shield belt?
				(__instance.pawn?.equipment?.Primary?.def.IsWeaponUsingProjectiles ?? false) && //Is using a gun?
				!shieldBelt.parent.def.HasModExtension<StaticShield>() && //Is not a static shield?
				__instance.pawn.Spawned //Is the pawn actually spawned?
			)
			{
				shieldBelt.energy = -0.0001f;
			}
		}
	}
}
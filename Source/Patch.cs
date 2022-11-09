
using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;

namespace ToggleableShields
{

	//Allows guns to work for disabled shields
	[HarmonyPatch(typeof(CompShield), nameof(CompShield.CompAllowVerbCast))]
	public class Patch_AllowVerbCast
	{
		public static void Postfix(CompShield __instance, ref bool __result)
		{
			__result = __result || __instance.energy < 0f;
		}
    }

	//Makes the shield not render while toggled off
	[HarmonyPatch(typeof(CompShield), nameof(CompShield.ShouldDisplay), MethodType.Getter)]
	public class Patch_ShouldDisplay
	{
		public static bool Prefix(CompShield __instance, ref bool __result)
		{
			return __result = __instance.energy >= 0f;
		}
    }

	//Shields have ticks and each tick they try to recharge. This just sets to recharge to 0 if it's disabled (as defined by -1 energy)
	[HarmonyPatch(typeof(CompShield), nameof(CompShield.CompTick))]
	public class Patch_EnergyGainPerTick
	{
		public static bool Prefix(CompShield __instance)
		{
			return __instance.energy >= 0f;
		}
    }

	//Adds our gizmo
	[HarmonyPatch(typeof(CompShield), nameof(CompShield.CompGetWornGizmosExtra))]
	public class Patch_GetWornGizmos
	{
		static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, CompShield __instance)
		{
			//Pass along all other gizmos
            foreach (var value in values) yield return value;

			//Add our gizmo
			if ((__instance.PawnOwner?.IsColonistPlayerControlled ?? false) && !__instance.parent.def.HasModExtension<StaticShield>())
			{
				yield return new Command_Toggle
					{
						defaultLabel = "ToggleableShields.Icon.Toggle".Translate(),
						defaultDesc = "ToggleableShields.Icon.Toggle.Desc".Translate(),
						icon = __instance.parent.def.uiIcon ?? TexCommand.ForbidOff,
						isActive = (() => __instance.energy >= 0f ),
						toggleAction = delegate()
						{
							if (__instance.energy < 0f) 
							{
								__instance.energy = 0;
								__instance.ticksToReset = __instance.Props.startingTicksToReset;
							}
							else
							{
								__instance.Break();
								__instance.energy = -0.0001f;
							}
						}
					};
			}
		}

		//Removes the shield warning as this mod largely makes it invalid
		[HarmonyPatch]
		class ResetCacheTriggers
		{
			static IEnumerable<System.Reflection.MethodBase> TargetMethods()
			{
				//Used by gun user + shield alert
				yield return AccessTools.Method(typeof(WorkGiver_HunterHunt), nameof(WorkGiver_HunterHunt.HasShieldAndRangedWeapon));
				//Used by the hunter + shield alert
				yield return AccessTools.PropertyGetter(typeof(Alert_ShieldUserHasRangedWeapon), nameof(Alert_ShieldUserHasRangedWeapon.ShieldUsersWithRangedWeapon));
			}

			static bool Prefix()
			{
				return false;
			}
		}

		//This patches how pawns would normally ignore shield belts if they're using a gun when they go and equip things from storage
		[HarmonyPatch(typeof(JobGiver_OptimizeApparel), nameof(JobGiver_OptimizeApparel.ApparelScoreGain))]
		public class Patch_ApparelScoreGain
		{
			public static bool Prefix(Pawn pawn, Apparel ap, ref float __result, List<float> wornScoresCache)
			{
				if (ap.TryGetComp<CompShield>() != null && pawn.equipment.Primary != null && pawn.equipment.Primary.def.IsWeaponUsingProjectiles)
				{
					//This code is temporarily. It's just a copy-paste done fast to push as a hotfix for now. Replacing with a transpiler is probably the cleaner solution.
					float num = JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, ap);
					List<Apparel> wornApparel = pawn.apparel.WornApparel;
					bool flag = false;
					for (int i = 0; i < wornApparel.Count; i++)
					{
						if (!ApparelUtility.CanWearTogether(wornApparel[i].def, ap.def, pawn.RaceProps.body))
						{
							if (!pawn.outfits.forcedHandler.AllowedToAutomaticallyDrop(wornApparel[i]) || pawn.apparel.IsLocked(wornApparel[i]))
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
		public class Patch_Wear
		{
			public static void Prefix(Apparel newApparel, Pawn_ApparelTracker __instance)
			{
				CompShield shieldBelt = newApparel.TryGetComp<CompShield>();
				if
				(
					shieldBelt != null && //Is a shield belt?
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
}
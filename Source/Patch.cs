
using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;

namespace ToggleableShields
{

	//Allows guns to work for disabled shields
	[HarmonyPatch(typeof(ShieldBelt), nameof(ShieldBelt.AllowVerbCast))]
	public class Patch_AllowVerbCast
	{
		public static void Postfix(ShieldBelt __instance, ref bool __result)
		{
			__result = __result || __instance.energy < 0f;
		}
    }

	//Makes the shield not render while toggled off
	[HarmonyPatch(typeof(ShieldBelt), nameof(ShieldBelt.ShouldDisplay), MethodType.Getter)]
	public class Patch_ShouldDisplay
	{
		public static bool Prefix(ShieldBelt __instance, ref bool __result)
		{
			if (__instance.energy >= 0f) return true;
			else
			{
				__result = false;
				return false;
			}
		}
    }

	//Shields have ticks and each tick they try to recharge. This just sets to recharge to 0 if it's disabled (as defined by -1 energy)
	[HarmonyPatch(typeof(ShieldBelt), nameof(ShieldBelt.EnergyGainPerTick), MethodType.Getter)]
	public class Patch_EnergyGainPerTick
	{
		public static bool Prefix(ShieldBelt __instance, float __result)
		{
			return __instance.energy >= 0f;
		}
    }

	//Adds our gizmo
	[HarmonyPatch(typeof(ShieldBelt), nameof(ShieldBelt.GetWornGizmos))]
	public class Patch_GetWornGizmos
	{
		static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, ShieldBelt __instance)
		{
			//Pass along all other gizmos
            foreach (var value in values) yield return value;

			//Add our gizmo
			if (!__instance.def.HasModExtension<StaticShield>())
			{
				yield return new Command_Toggle
					{
						defaultLabel = "ToggleableShields.Icon.Toggle".Translate(),
						defaultDesc = "ToggleableShields.Icon.Toggle.Desc".Translate(),
						icon = __instance.def.uiIcon ?? TexCommand.ForbidOff,
						isActive = (() => __instance.energy >= 0f ),
						toggleAction = delegate()
						{
							if (__instance.energy < 0f) 
							{
								__instance.energy = 0;
								__instance.ticksToReset = __instance.StartingTicksToReset;
							}
							else __instance.energy = -0.0001f;
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
    }
}
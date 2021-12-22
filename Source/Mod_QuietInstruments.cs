using Verse;
using HarmonyLib;
 
namespace ToggleableShields
{
    public class Mod_ShieldToggle : Mod
	{
		public Mod_ShieldToggle(ModContentPack content) : base(content)
		{
			new Harmony(this.Content.PackageIdPlayerFacing).PatchAll();
		}
	}
}
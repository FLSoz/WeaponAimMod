using HarmonyLib;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;


namespace WeaponAimMod
{
    public class AutoSetFusePatch
    {
        private static readonly FieldInfo m_FiringData = typeof(ModuleWeaponGun).GetField("m_FiringData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo m_LifeTime = typeof(Projectile).GetField("m_LifeTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo m_ExplodeAfterLifetime = typeof(Projectile).GetField("m_ExplodeAfterLifetime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // Set it to add/remove them after pool as appropriate
        [HarmonyPatch(typeof(ModuleWeapon))]
        [HarmonyPatch("UpdateAutoAimBehaviour")]
        public static class PatchDynamicFuseSet
        {
            private static readonly FieldInfo m_WeaponComponent = typeof(ModuleWeapon).GetField("m_WeaponComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            public static bool Prefix(ref ModuleWeapon __instance)
            {
                IModuleWeapon moduleWeapon = (IModuleWeapon) m_WeaponComponent.GetValue(__instance);
                if (moduleWeapon == null)
                {
                    Console.WriteLine($"NULL WeaponComponent for {__instance.block.name}");
                }
                return true;
            }
        }
    }
}

using HarmonyLib;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;


namespace WeaponAimMod.src
{
    public class AutoSetFusePatch
    {
        private static readonly FieldInfo m_FiringData = typeof(ModuleWeaponGun).GetField("m_FiringData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo m_LifeTime = typeof(Projectile).GetField("m_LifeTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo m_ExplodeAfterLifetime = typeof(Projectile).GetField("m_ExplodeAfterLifetime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);


        public static void AddAutoSetFuse(ModuleWeaponGun __instance)
        {
            TimedFuseData timedFuse = __instance.GetComponentInParent<TimedFuseData>();
            if (timedFuse == null)
            {
                FireData fireData = (FireData)m_FiringData.GetValue(__instance);
                if (fireData == null)
                {
                    fireData = __instance.GetComponent<FireData>();
                    m_FiringData.SetValue(__instance, fireData);
                }
                if (fireData != null)
                {
                    WeaponRound round = fireData.m_BulletPrefab;
                    if (round != null && round is Projectile projectile)
                    {
                        float lifetime = (float) m_LifeTime.GetValue(projectile);
                        bool explodeAfterLifetime = (bool)m_ExplodeAfterLifetime.GetValue(projectile);

                        // We check if projectile has a lifetime, and it explodes after it (no use setting fuse if fixed lifetime anyway)
                        if (explodeAfterLifetime && lifetime > 0f)
                        {
                            timedFuse = __instance.gameObject.AddComponent<TimedFuseData>();
                            timedFuse.always_present = false;
                        }
                    }
                }
            }
        }

        public static TimedFuseData RemoveAutoSetFuse(TimedFuseData fuse)
        {
            if (!fuse.always_present)
            {
                UnityEngine.Object.Destroy(fuse);
                return null;
            }
            return fuse;
        }

        // Set it to add on pool
        [HarmonyPatch(typeof(ModuleWeaponGun))]
        [HarmonyPatch("OnPool")]
        public static class AutoFusePatch
        {
            private static void Postfix(ref ModuleWeaponGun __instance)
            {
                // Do timed fuse setting
                if (WeaponAimSettings.AutoSetFuse)
                {
                    AddAutoSetFuse(__instance);
                }
            }
        }

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
                if (moduleWeapon != null && moduleWeapon is ModuleWeaponGun moduleWeaponGun)
                {
                    if (WeaponAimSettings.AutoSetFuse)
                    {
                        AddAutoSetFuse(moduleWeaponGun);
                    }
                    else
                    {
                        TimedFuseData timedFuse = __instance.GetComponentInParent<TimedFuseData>();
                        if (timedFuse && !timedFuse.always_present)
                        {
                            RemoveAutoSetFuse(timedFuse);
                        }
                    }
                }
                return true;
            }
        }
    }
}

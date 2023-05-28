using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace WeaponAimMod
{
    public class AccurateExplosionSpawnPatch
    {
        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("OnLifetimeEnd")]
        public static class PatchExplosionSpawn
        {
            private static readonly FieldInfo m_ExplodeAfterLifetime = typeof(Projectile).GetField("m_ExplodeAfterLifetime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo m_DestroyTimeout = typeof(Projectile).GetField("m_DestroyTimeout", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly MethodInfo SpawnExplosion = typeof(Projectile).GetMethod("SpawnExplosion", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            public static bool Prefix(Projectile __instance)
            {
                d.Assert(__instance.gameObject.activeInHierarchy);
                if ((bool)m_ExplodeAfterLifetime.GetValue(__instance))
                {
                    Vector3 adjPos = __instance.trans.position + (__instance.rbody.velocity * (float)m_DestroyTimeout.GetValue(__instance));
                    SpawnExplosion.Invoke(__instance, new object[] { adjPos, null });
                }
                __instance.Recycle(true);
                return false;
            }
        }
    }
}

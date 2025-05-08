using HarmonyLib;
using UnityEngine;
using System;
using System.Collections;
using System.Reflection;
using System.Linq;
using static CompoundExpression;

namespace WeaponAimMod
{
    public class AccurateExplosionSpawnPatch
    {
        [HarmonyPatch(typeof(ManCombat.Projectiles))]
        [HarmonyPatch("DeregisterPerishableProjectileAt")]
        public static class PatchExplosionSpawnPoint
        {
            private static readonly FieldInfo m_ExplodeAfterLifetime = typeof(Projectile).GetField("m_ExplodeAfterLifetime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo s_Perishables = typeof(ManCombat.Projectiles).GetField("s_Perishables", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            private static readonly Type ExpirationInfo = s_Perishables.FieldType.GetGenericArguments().Single();
            private static readonly FieldInfo Projectile = ExpirationInfo.GetField("Projectile", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo ExpirationTime = ExpirationInfo.GetField("ExpirationTime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            public static void Prefix(int index)
            {
                IList perishables = (IList) s_Perishables.GetValue(null);
                object info = perishables[index];
                Projectile proj = (Projectile) Projectile.GetValue(info);
                if ((bool)m_ExplodeAfterLifetime.GetValue(proj))
                {
                    float expirationTime = (float)ExpirationTime.GetValue(info);
                    proj.trans.position = proj.trans.position + (proj.rbody.velocity * (expirationTime - Time.time));
                }
                return;
            }
        }
    }
}

﻿using Harmony;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;


namespace WeaponAimMod.src
{
    public class ProjectilePatch
    {
        public static FieldInfo m_TargetPosition = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        public static FieldInfo Target = typeof(TargetAimer).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(field =>
                        field.CustomAttributes.Any(attr => attr.AttributeType == typeof(CompilerGeneratedAttribute)) &&
                        (field.DeclaringType == typeof(TargetAimer).GetProperty("Target").DeclaringType) &&
                        field.FieldType.IsAssignableFrom(typeof(TargetAimer).GetProperty("Target").PropertyType) &&
                        field.Name.StartsWith("<" + typeof(TargetAimer).GetProperty("Target").Name + ">")
                    );
        public static FieldInfo m_Block = typeof(TargetAimer).GetField("m_Block", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        public static FieldInfo m_ChangeTargetTimeout = typeof(TargetAimer).GetField("m_ChangeTargetTimeout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        public static FieldInfo m_ChangeTargetInteval = typeof(TargetAimer).GetField("m_ChangeTargetInteval", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);


        // Target leading for weapons
        [HarmonyPatch(typeof(TargetAimer))]
        [HarmonyPatch("UpdateTarget")]
        public static class TargetAimerPatch
        {
            public static void Postfix(ref TargetAimer __instance)
            {
                if (__instance.HasTarget && !Singleton.Manager<ManPauseGame>.inst.IsPaused)
                {
                    TankBlock block = (TankBlock) ProjectilePatch.m_Block.GetValue(__instance);
                    Tank tank = (bool)(UnityEngine.Object)block ? block.tank : (Tank)null;
                    FireData componentInParent1 = __instance.GetComponentInParent<FireData>();
                    Vector3 AimPointVector = __instance.Target.GetAimPoint(block.trans.position);

                    if (AimPointVector == null)
                    {
                        AimPointVector = __instance.Target.rbody.transform.position;
                    }

                    TimedFuseData timedFuse = __instance.GetComponentInParent<TimedFuseData>();
                    Vector3 relDist = AimPointVector - __instance.transform.position;

                    bool enemyWeapon = tank == null || !ManSpawn.IsPlayerTeam(tank.Team);

                    if (((UnityEngine.Object)componentInParent1 != (UnityEngine.Object)null) && ((enemyWeapon && WeaponAimSettings.EnemyLead) || (!enemyWeapon && WeaponAimSettings.PlayerLead)) && !(componentInParent1 is FireDataShotgun) && componentInParent1.m_MuzzleVelocity > 0.0f)
                    {
                        WeaponRound bulletPrefab = componentInParent1.m_BulletPrefab;

                        bool useGravity = false;
                        if (bulletPrefab != null)
                        {
                            useGravity = bulletPrefab.gameObject.GetComponent<Rigidbody>().useGravity;
                        }

                        Rigidbody rbodyTank = __instance.GetComponentInParent<Tank>().rbody;

                        Vector3 angularToggle = rbodyTank.angularVelocity;
                        Vector3 relativeVelocity = __instance.Target.rbody.velocity - (rbodyTank.velocity + angularToggle);

                        float time = relDist.magnitude / componentInParent1.m_MuzzleVelocity;
                        Visible target = __instance.Target;
                        Vector3 relativeAcceleration = target.type == ObjectTypes.Vehicle ? TargetManager.GetAcceleration(target.tank) : Vector3.zero;

                        if (useGravity)
                        {
                            relativeAcceleration -= Physics.gravity;
                        }

                        float exactTime = BallisticEquations.SolveBallisticArc(__instance.transform.position, componentInParent1.m_MuzzleVelocity, AimPointVector, relativeVelocity, relativeAcceleration);
                        Vector3 adjIntercept = AimPointVector + (relativeVelocity * time);
                        if (exactTime != Mathf.Infinity)
                        {
                            time = exactTime;
                            adjIntercept = AimPointVector + (relativeVelocity * time) + ((relativeAcceleration + (useGravity ? Physics.gravity : Vector3.zero)) / 2 * time * time);
                        }

                        if (timedFuse != null)
                        {
                            timedFuse.m_FuseTime = time;
                        }

                        ProjectilePatch.m_TargetPosition.SetValue(__instance, adjIntercept);
                    }
                    // Either disabled for enemy, or is a beam weapon
                    else
                    {
                        if (timedFuse != null)
                        {
                            timedFuse.m_FuseTime = relDist.magnitude / componentInParent1.m_MuzzleVelocity;
                        }
                    }
                }
            }
        }

        // Set fuse timer
        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("Fire")]
        public static class ProjectileFirePatch
        {
            private static bool Prefix(ref Projectile __instance, FireData fireData)
            {
                // Do timed fuse setting
                TimedFuseData timedFuse = fireData.GetComponentInParent<TimedFuseData>();
                if (timedFuse != null)
                {
                    // Console.WriteLine("fuse timer found: " + timedFuse.m_FuseTime.ToString());
                    FieldInfo m_LifeTime = typeof(Projectile).GetField("m_LifeTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    m_LifeTime.SetValue(__instance, timedFuse.m_FuseTime + timedFuse.offset);
                }
                return true;
            }
        }

        // Set muzzle velocity to 0 on beam weapons
        [HarmonyPatch(typeof(ModuleWeaponFlamethrower))]
        [HarmonyPatch("OnPool")]
        public static class WeaponFlamethrowerPatch
        {
            private static void Postfix(ref ModuleWeaponFlamethrower __instance)
            {
                FireData fireData = __instance.GetComponentInParent<FireData>();
                if (fireData != null)
                {
                    fireData.m_MuzzleVelocity = 0.0f;
                }
                return;
            }
        }

        [HarmonyPatch(typeof(ModuleWeaponGun))]
        [HarmonyPatch("OnPool")]
        public static class WeaponGunPatch
        {
            private static void Postfix(ref ModuleWeaponGun __instance)
            {
                FieldInfo m_CannonBarrels = typeof(ModuleWeaponGun).GetField("m_CannonBarrels", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                FieldInfo m_FiringData = typeof(ModuleWeaponGun).GetField("m_FiringData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                CannonBarrel[] cannonBarrels = (CannonBarrel[])m_CannonBarrels.GetValue(__instance);
                if (cannonBarrels != null)
                {
                    foreach (CannonBarrel cannonBarrel in cannonBarrels)
                    {
                        if (cannonBarrel.beamWeapon != null)
                        {
                            FireData fireData = __instance.GetComponent<FireData>();
                            if (fireData != null)
                            {
                                fireData.m_MuzzleVelocity = 0.0f;
                            }
                            return;
                        }
                    }
                }
                return;
            }
        }

        private const float tolerance = 15.0f;

        // Always fire when target is within tolerance of 15 degrees
        [HarmonyPatch(typeof(ModuleWeapon))]
        [HarmonyPatch("UpdateAutoAimBehaviour")]
        public static class PatchModuleWeapon
        {
            public static FieldInfo m_TargetPosition = typeof(ModuleWeapon).GetField("m_TargetPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static FieldInfo m_TargetPosition2 = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static FieldInfo m_WeaponComponent = typeof(ModuleWeapon).GetField("m_WeaponComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static MethodInfo FiringData = typeof(ModuleWeaponGun).GetMethod("get_FiringData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            public static FieldInfo m_TargetAimer = typeof(ModuleWeapon).GetField("m_TargetAimer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            // public static FieldInfo m_ChangeTargetInteval = typeof(ModuleWeapon).GetField("m_ChangeTargetInteval", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static FieldInfo m_SeekingProjectile = typeof(Projectile).GetField("m_SeekingProjectile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static FieldInfo m_VisionConeAngle = typeof(SeekingProjectile).GetField("m_VisionConeAngle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            public static void Postfix(ref ModuleWeapon __instance)
            {
                if (!__instance.FireControl && __instance.block.tank.IsAIControlled())
                {
                    TargetAimer targetAimer = (TargetAimer)m_TargetAimer.GetValue(__instance);
                    if (targetAimer.HasTarget)
                    {
                        Vector3 actualTargetPosition = (Vector3)m_TargetPosition2.GetValue(targetAimer);
                        m_TargetPosition.SetValue(__instance, actualTargetPosition);

                        IModuleWeapon weaponComponent = (IModuleWeapon)m_WeaponComponent.GetValue(__instance);
                        float range = weaponComponent.GetRange();

                        Transform fireTransform = weaponComponent.GetFireTransform();
                        Vector3 position = fireTransform.position;

                        if ((actualTargetPosition - position).sqrMagnitude < range * range)
                        {
                            if (weaponComponent is ModuleWeaponGun && ((ModuleWeaponGun) weaponComponent).m_SeekingRounds)
                            {
                                __instance.FireControl = true;
                            }
                            else if (Vector3.Angle(fireTransform.forward, actualTargetPosition - __instance.block.trans.position) < tolerance)
                            {
                                __instance.FireControl = true;
                            }
                        }
                    }
                }
            }
        }
    }
}

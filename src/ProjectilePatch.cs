using HarmonyLib;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text;

namespace WeaponAimMod.src
{
    public class ProjectilePatch
    {
        public static readonly FieldInfo m_TargetPosition = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        public static readonly FieldInfo Target = typeof(TargetAimer).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(field =>
                        field.CustomAttributes.Any(attr => attr.AttributeType == typeof(CompilerGeneratedAttribute)) &&
                        (field.DeclaringType == typeof(TargetAimer).GetProperty("Target").DeclaringType) &&
                        field.FieldType.IsAssignableFrom(typeof(TargetAimer).GetProperty("Target").PropertyType) &&
                        field.Name.StartsWith("<" + typeof(TargetAimer).GetProperty("Target").Name + ">")
                    );
        public static readonly FieldInfo m_Block = typeof(TargetAimer).GetField("m_Block", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        public static readonly FieldInfo m_ChangeTargetTimeout = typeof(TargetAimer).GetField("m_ChangeTargetTimeout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        public static readonly FieldInfo m_ChangeTargetInteval = typeof(TargetAimer).GetField("m_ChangeTargetInteval", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        public static string GetAllFootprints(Exception x)
        {
            var st = new StackTrace(x, true);
            var frames = st.GetFrames();
            var traceString = new StringBuilder();

            foreach (var frame in frames)
            {
                if (frame.GetFileLineNumber() < 1)
                    continue;

                traceString.Append("File: " + frame.GetFileName());
                traceString.Append(", Method:" + frame.GetMethod().Name);
                traceString.Append(", LineNumber: " + frame.GetFileLineNumber());
                traceString.Append("  -->  ");
            }

            return traceString.ToString();
        }

        // Target leading for weapons
        [HarmonyPatch(typeof(TargetAimer))]
        [HarmonyPatch("UpdateTarget")]
        public static class TargetAimerPatch
        {
            public static Visible GetManualTarget(TankBlock block)
            {
                Tank tank = block ? block.tank : null;
                TechWeapon techWeapon = tank ? tank.Weapons : null;
                if (!techWeapon)
                {
                    return null;
                }
                return techWeapon.GetManualTarget();
            }

            private static readonly FieldInfo m_TargetPosition = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            private static readonly FieldInfo m_Block = typeof(TargetAimer).GetField("m_Block", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            private static readonly FieldInfo m_ChangeTargetTimeout = typeof(TargetAimer).GetField("m_ChangeTargetTimeout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            private static readonly FieldInfo m_ChangeTargetInteval = typeof(TargetAimer).GetField("m_ChangeTargetInteval", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            [HarmonyPriority(999)]
            public static bool Prefix(TargetAimer __instance)
            {
                try
                {
                    TankBlock ___m_Block = (TankBlock)m_Block.GetValue(__instance);
                    float ___m_ChangeTargetTimeout = (float)m_ChangeTargetTimeout.GetValue(__instance);
                    float ___m_ChangeTargetInteval = (float)m_ChangeTargetInteval.GetValue(__instance);

                    if (!___m_Block || !___m_Block.tank)
                    {
                        __instance.Reset();
                    }
                    else
                    {
                        Visible manualTarget = GetManualTarget(___m_Block);
                        if (manualTarget)
                        {
                            PatchAiming.Target.SetValue(__instance, manualTarget);
                        }
                        else if (__instance.HasTarget && (!__instance.Target.isActive || Time.time > ___m_ChangeTargetTimeout))
                        {
                            __instance.Reset();
                        }
                        if (__instance.Target == null && ___m_Block.tank.control.targetType != ObjectTypes.Null && (!__instance.HasTarget || !___m_Block.tank.Vision.CanSee(__instance.Target)))
                        {
                            if (___m_Block.tank.control.targetType == ObjectTypes.Vehicle)
                            {
                                PatchAiming.Target.SetValue(__instance, ___m_Block.tank.Vision.GetFirstVisibleTechIsEnemy(___m_Block.tank.Team));
                            }
                            else
                            {
                                PatchAiming.Target.SetValue(__instance, ___m_Block.tank.Vision.GetFirstVisible());
                            }
                            m_ChangeTargetTimeout.SetValue(__instance, Time.time + ___m_ChangeTargetInteval);
                        }
                        if (__instance.HasTarget && __instance.Target.gameObject.IsNotNull())
                        {
                            try
                            {
                                m_TargetPosition.SetValue(__instance, __instance.Target.GetAimPoint(___m_Block.trans.position));
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                                Console.WriteLine(__instance.Target.gameObject == null);
                                Console.WriteLine($"Failed to get aim point for {__instance.Target.name}");

                                m_TargetPosition.SetValue(__instance, __instance.Target.transform.position);

                                throw new Exception("CAUGHT AN AIM POINT FAILURE");
                            }
                        }
                        else
                        {
                            __instance.Reset();
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("[WeaponAimMod] TargetAimer.UpdateTarget PATCH FAILED");
                    Console.WriteLine(e.ToString());
                }
                return false;
            }

            public static void Postfix(TargetAimer __instance)
            {
                try
                {
                    Visible target = __instance.Target;
                    FireData fireData = __instance.GetComponentInParent<FireData>();
                    if (fireData != null && __instance.HasTarget && !Singleton.Manager<ManPauseGame>.inst.IsPaused && ((target.type == ObjectTypes.Vehicle && target.tank.IsNotNull()) || (target.type == ObjectTypes.Block && target.block.IsNotNull())))
                    {
                        TankBlock block = (TankBlock)ProjectilePatch.m_Block.GetValue(__instance);
                        Tank tank = (bool)(UnityEngine.Object)block ? block.tank : (Tank)null;

                        string name = block ? block.name : "UNKNOWN";
                        TimedFuseData timedFuse = __instance.GetComponentInParent<TimedFuseData>();

                        bool enemyWeapon = tank == null || !ManSpawn.IsPlayerTeam(tank.Team);

                        if (((enemyWeapon && WeaponAimSettings.EnemyLead) || (!enemyWeapon && WeaponAimSettings.PlayerLead)) && !(fireData is FireDataShotgun) && fireData.m_MuzzleVelocity > 0.0f)
                        {
                            Vector3 AimPointVector = (Vector3)ProjectilePatch.m_TargetPosition.GetValue(__instance);
                            Vector3 relDist = AimPointVector - __instance.transform.position;
                            WeaponRound bulletPrefab = fireData.m_BulletPrefab;

                            bool useGravity = false;
                            if (bulletPrefab != null && bulletPrefab is Projectile projectile && projectile.rbody != null)
                            {
                                if (projectile is MissileProjectile missileProjectile)
                                {
                                    useGravity = missileProjectile.rbody.useGravity || WeaponAimSettings.BallisticMissile;
                                }
                                else
                                {
                                    useGravity = projectile.rbody.useGravity;
                                }
                            }

                            Rigidbody rbodyTank = __instance.GetComponentInParent<Tank>().rbody;

                            Vector3 angularToggle = rbodyTank.angularVelocity;
                            Vector3 relativeVelocity = (__instance.Target.rbody ? __instance.Target.rbody.velocity : Vector3.zero) - (rbodyTank.velocity + angularToggle);

                            float time = relDist.magnitude / fireData.m_MuzzleVelocity;
                            Vector3 relativeAcceleration = target.type == ObjectTypes.Vehicle ? TargetManager.GetAcceleration(target.tank) : Vector3.zero;

                            if (useGravity)
                            {
                                relativeAcceleration -= Physics.gravity;
                            }

                            float exactTime = BallisticEquations.SolveBallisticArc(__instance.transform.position, fireData.m_MuzzleVelocity, AimPointVector, relativeVelocity, relativeAcceleration);
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
                                timedFuse.m_FuseTime = 0.0f;
                            }
                        }
                    }
                }
                catch (NullReferenceException exception)
                {
                    Console.WriteLine("[WeaponAimMod] TargetAimer.UpdateTarget PATCH FAILED");
                    Console.WriteLine(exception);
                }
                catch (Exception exception)
                {
                    Console.WriteLine("[WeaponAimMod] TargetAimer.Updatetarget PATCH CRITICAL FAILURE");
                    Console.WriteLine(exception);
                }
            }

            public static Exception Finalizer(TargetAimer __instance, Exception __exception)
            {
                if (__exception != null)
                {
                    Console.WriteLine("ERROR FROM UPDATE TARGET:\n" + __exception.ToString());
                    Console.WriteLine($"Instance on object {__instance.name}");
                    TankBlock block = (TankBlock)m_Block.GetValue(__instance);
                    Console.WriteLine($"Instance on block {block.name}");
                    string tankName = block.tank ? block.tank.name : "<NULL TANK>";
                    Console.WriteLine($"Instance on tank {tankName}");
                }
                return null;
            }
        }

        // Set fuse timer
        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("Fire")]
        public static class ProjectileFirePatch
        {
            private static readonly FieldInfo m_MaxBoosterLifetime = typeof(MissileProjectile).GetField("m_MaxBoosterLifetime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo m_LifeTime = typeof(Projectile).GetField("m_LifeTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            private static bool Prefix(ref Projectile __instance, FireData fireData, out float __state)
            {
                __state = (float) m_LifeTime.GetValue(__instance);

                // Do timed fuse setting
                TimedFuseData timedFuse = fireData.GetComponentInParent<TimedFuseData>();
                if (timedFuse != null)
                {
                    m_LifeTime.SetValue(__instance, timedFuse.m_FuseTime + timedFuse.offset);
                }
                return true;
            }

            private static void Postfix(ref Projectile __instance, float __state)
            {
                m_LifeTime.SetValue(__instance, __state);
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
            private static readonly FieldInfo m_CannonBarrels = typeof(ModuleWeaponGun).GetField("m_CannonBarrels", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            private static readonly FieldInfo m_FiringData = typeof(ModuleWeaponGun).GetField("m_FiringData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            private static void Postfix(ref ModuleWeaponGun __instance)
            {
                CannonBarrel[] cannonBarrels = (CannonBarrel[])m_CannonBarrels.GetValue(__instance);
                if (cannonBarrels != null)
                {
                    foreach (CannonBarrel cannonBarrel in cannonBarrels)
                    {
                        if (cannonBarrel.beamWeapon)
                        {
                            FireData fireData = (FireData) m_FiringData.GetValue(__instance);
                            if (fireData != null)
                            {
                                fireData.m_MuzzleVelocity = 0f;
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
            public static readonly FieldInfo m_TargetPosition = typeof(ModuleWeapon).GetField("m_TargetPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo m_TargetPosition2 = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo m_WeaponComponent = typeof(ModuleWeapon).GetField("m_WeaponComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly MethodInfo FiringData = typeof(ModuleWeaponGun).GetMethod("get_FiringData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            public static readonly FieldInfo m_TargetAimer = typeof(ModuleWeapon).GetField("m_TargetAimer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            // public static readonly FieldInfo m_ChangeTargetInteval = typeof(ModuleWeapon).GetField("m_ChangeTargetInteval", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo m_SeekingProjectile = typeof(Projectile).GetField("m_SeekingProjectile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo m_VisionConeAngle = typeof(SeekingProjectile).GetField("m_VisionConeAngle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

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

            public static Exception Finalizer(Exception __exception)
            {
                if (__exception != null)
                {
                    Console.WriteLine("ERROR FROM UPDATEAUTOAIMBEHAVIOUR:\n" + __exception.ToString());
                }
                return null;
            }
        }
    }
}

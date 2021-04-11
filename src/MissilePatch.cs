using Harmony;
using UnityEngine;
using System.Reflection;
using System;

namespace WeaponAimMod.src
{
    public class MissilePatch
    {
        // Target leading for missiles
        [HarmonyPatch(typeof(SeekingProjectile))]
        [HarmonyPatch("FixedUpdate")]
        public static class PatchMissiles
        {
            private static FieldInfo m_MyProjectile = typeof(SeekingProjectile).GetField("m_MyProjectile", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static FieldInfo m_MyTransform = typeof(SeekingProjectile).GetField("m_MyTransform", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static FieldInfo m_TurnSpeed = typeof(SeekingProjectile).GetField("m_TurnSpeed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static FieldInfo m_ApplyRotationTowardsTarget = typeof(SeekingProjectile).GetField("m_ApplyRotationTowardsTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static MethodInfo GetCurrentTarget = typeof(SeekingProjectile).GetMethod("GetCurrentTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static MethodInfo GetTargetAimPosition = typeof(SeekingProjectile).GetMethod("GetTargetAimPosition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            // Actually apply rotation towards target
            private static void ApplyRotation(SeekingProjectile __instance, Projectile projectile, Transform me, Vector3 targetPosition)
            {
                bool applyRotation = (bool)m_ApplyRotationTowardsTarget.GetValue(__instance);

                Vector3 vector = targetPosition - me.position;
                Vector3 normalized = Vector3.Cross(projectile.rbody.velocity, vector).normalized;
                float b = Vector3.Angle(projectile.trans.forward, vector);

                Quaternion quaternion = Quaternion.AngleAxis(Mathf.Min((float)m_TurnSpeed.GetValue(__instance) * Time.deltaTime, b), normalized);
                projectile.rbody.velocity = quaternion * projectile.rbody.velocity;
                if (applyRotation)
                {
                    Quaternion rot = quaternion * projectile.rbody.rotation;
                    projectile.rbody.MoveRotation(rot);
                }
            }
            
            // Predict where enemy is going, then apply rotation towards that
            private static void CalculateAndApplyRotation(SeekingProjectile __instance, Projectile projectile, Visible target, Transform me, Vector3 targetPosition, Vector3 V, bool guess = false)
            {
                float speed = projectile.rbody.velocity.magnitude;
                float estTime = (targetPosition - me.position).magnitude / speed;
                
                float finalThreshold = 0.1f;
                

                if (estTime >= finalThreshold)
                {
                    Vector3 A = !guess && target.type == ObjectTypes.Vehicle && estTime >= 1.5f ? TargetManager.GetAcceleration(target.tank) : Vector3.zero;
                    float time = BallisticEquations.SolveBallisticArc(me.position, speed, targetPosition, V, A);

                    if (time != Mathf.Infinity)
                    {
                        targetPosition += (V * time) + (A * time * time / 2);
                    }

                    ApplyRotation(__instance, projectile, me, targetPosition);
                }
            }

            // Do missile targeting
            public static bool Prefix(ref SeekingProjectile __instance)
            {
                Projectile projectile = (Projectile)m_MyProjectile.GetValue(__instance);
                bool enemyMissile = projectile.Shooter == null || !!ManSpawn.IsPlayerTeam(projectile.Shooter.Team);

                // Only do leading if is a player, or enemy lead enabled
                if ((enemyMissile && WeaponAimSettings.EnemyMissileLead) || (!enemyMissile && WeaponAimSettings.PlayerMissileLead)) {
                    Visible target = (Visible)GetCurrentTarget.Invoke(__instance, null);
                    Transform me = (Transform)m_MyTransform.GetValue(__instance);

                    // If have a target, use it
                    if (target.IsNotNull())
                    {
                        Vector3 targetPosition = (Vector3)GetTargetAimPosition.Invoke(__instance, null);
                        if (target.rbody != null && target.rbody.velocity.magnitude > 1f)
                        {
                            CalculateAndApplyRotation(__instance, projectile, target, me, targetPosition, target.rbody.velocity);
                        }
                        else
                        {
                            ApplyRotation(__instance, projectile, me, targetPosition);
                        }
                    }

                    // If no target currently visible, steer towards last known position of any target
                    else if (WeaponAimSettings.SmartMissile)
                    {
                        SmartMissile smartMissile = __instance.GetComponent<SmartMissile>();
                        if (smartMissile != null && smartMissile.target != null)
                        {
                            // Console.WriteLine($"Missile use smart target {smartMissile.target.name} at extrapolated {smartMissile.position + (Time.time - smartMissile.time) * smartMissile.velocity}, actual {smartMissile.target.centrePosition}");
                            Vector3 targetPosition = smartMissile.position + (Time.time - smartMissile.time) * smartMissile.velocity;
                            if (smartMissile.velocity.magnitude > 1f)
                            {
                                CalculateAndApplyRotation(__instance, projectile, smartMissile.target, me, targetPosition, smartMissile.velocity, true);
                            }
                            else
                            {
                                ApplyRotation(__instance, projectile, me, targetPosition);
                            }
                        }
                    }
                    return false;
                }
                // If no lead, but smart missiles selected - rotate towards that
                else if (WeaponAimSettings.SmartMissile)
                {
                    SmartMissile smartMissile = __instance.GetComponent<SmartMissile>();
                    if (smartMissile != null && smartMissile.target != null)
                    {
                        // Console.WriteLine($"Missile use smart target {smartMissile.target.name} at extrapolated {smartMissile.position + (Time.time - smartMissile.time) * smartMissile.velocity}, actual {smartMissile.target.centrePosition}");
                        Transform me = (Transform)m_MyTransform.GetValue(__instance);
                        Vector3 targetPosition = smartMissile.position + (Time.time - smartMissile.time) * smartMissile.velocity;
                        ApplyRotation(__instance, projectile, me, targetPosition);
                    }
                    return false;
                }
                return true;
            }

            // Update smart missile
            public static void Postfix(ref SeekingProjectile __instance)
            {
                Visible target = (Visible)GetCurrentTarget.Invoke(__instance, null);
                SmartMissile smartMissile = __instance.GetComponent<SmartMissile>();
                if (smartMissile != null && target != null)
                {
                    Vector3 targetPosition = (Vector3)GetTargetAimPosition.Invoke(__instance, null);
                    smartMissile.target = target;
                    smartMissile.time = Time.time;
                    smartMissile.position = targetPosition;
                    smartMissile.velocity = target.rbody.velocity;
                }
            }
        }

        // Add SmartMissile, tell it to go towards tank's current target
        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("Fire")]
        public static class MissileFirePatch
        {
            public static FieldInfo m_SeekingProjectile = typeof(Projectile).GetField("m_SeekingProjectile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            private static void Postfix(ref Projectile __instance, bool seekingRounds)
            {
                // If there's a SeekingProjectile, set SmartMissile
                SeekingProjectile missile = (SeekingProjectile)m_SeekingProjectile.GetValue(__instance);
                if (missile != null && seekingRounds)
                {
                    SmartMissile smartMissile = __instance.GetComponent<SmartMissile>();
                    if (smartMissile == null)
                    {
                        smartMissile = __instance.gameObject.AddComponent<SmartMissile>();
                    }

                    Tank shooter = __instance.Shooter;
                    Visible target = null;
                    if (shooter.control.targetType == ObjectTypes.Vehicle)
                    {
                        target = shooter.Vision.GetFirstVisibleTechIsEnemy(shooter.Team);
                    }
                    else
                    {
                        target = shooter.Vision.GetFirstVisible();
                    }

                    if (target != null)
                    {
                        // Console.WriteLine($"Missile smart target {target.name} at {target.centrePosition} with velocity {target.rbody.velocity}");
                        smartMissile.target = target;
                        smartMissile.time = Time.time;
                        smartMissile.position = target.centrePosition;
                        smartMissile.velocity = target.rbody.velocity;
                    }
                    else
                    {
                        smartMissile.target = null;
                    }
                }
                return;
            }
        }
    }
}

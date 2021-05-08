using Harmony;
using UnityEngine;
using System.Reflection;
using System;

namespace WeaponAimMod.src
{
    public class MissilePatch
    {
        private static readonly FieldInfo m_MaxBoosterLifetime = typeof(MissileProjectile).GetField("m_MaxBoosterLifetime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        // Target leading for missiles
        [HarmonyPatch(typeof(SeekingProjectile))]
        [HarmonyPatch("FixedUpdate")]
        public static class PatchMissiles
        {
            private static readonly FieldInfo m_MyProjectile = typeof(SeekingProjectile).GetField("m_MyProjectile", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo m_MyTransform = typeof(SeekingProjectile).GetField("m_MyTransform", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo m_TurnSpeed = typeof(SeekingProjectile).GetField("m_TurnSpeed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo m_ApplyRotationTowardsTarget = typeof(SeekingProjectile).GetField("m_ApplyRotationTowardsTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly MethodInfo GetCurrentTarget = typeof(SeekingProjectile).GetMethod("GetCurrentTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly MethodInfo GetTargetAimPosition = typeof(SeekingProjectile).GetMethod("GetTargetAimPosition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            // Actually apply rotation towards target
            private static void ApplyRotation(SeekingProjectile __instance, Projectile projectile, Transform me, Vector3 targetPosition, bool reduced)
            {
                bool applyRotation = (bool)m_ApplyRotationTowardsTarget.GetValue(__instance);

                Vector3 vector = targetPosition - me.position;
                Vector3 normalized = Vector3.Cross(projectile.rbody.velocity, vector).normalized;
                float b = Vector3.Angle(projectile.trans.forward, vector);

                float turnSpeed = (float)m_TurnSpeed.GetValue(__instance);
                if (reduced)
                {
                    turnSpeed = Mathf.Min(turnSpeed * 0.1f, 5f);
                }

                Quaternion quaternion = Quaternion.AngleAxis(Mathf.Min(turnSpeed * Time.deltaTime, b), normalized);
                projectile.rbody.velocity = quaternion * projectile.rbody.velocity;
                if (applyRotation)
                {
                    Quaternion rot = quaternion * projectile.rbody.rotation;
                    projectile.rbody.MoveRotation(rot);
                }
            }
            
            // Predict where enemy is going, then apply rotation towards that
            private static void CalculateAndApplyRotation(SeekingProjectile __instance, Projectile projectile, Visible target, Transform me, Vector3 targetPosition, Vector3 V, bool reduced, bool guess = false)
            {
                float speed = projectile.rbody.velocity.magnitude;
                float estTime = (targetPosition - me.position).magnitude / speed;
                float remainingTime = 0f;
                float totalTime = 0f;

                if (WeaponAimSettings.BallisticMissile)
                {
                    SmartMissile smartMissile = __instance.GetComponent<SmartMissile>();
                    if (smartMissile)
                    {
                        remainingTime = smartMissile.expireTime - Time.time;
                        totalTime = smartMissile.totalTime;

                        float estActualTime = BallisticEquations.SolveBallisticArc(me.position, speed, targetPosition, V, Vector3.zero);
                        if (estActualTime != Mathf.Infinity)
                        {
                            estTime = estActualTime;
                        }
                    }
                }

                if (estTime <= remainingTime)
                {
                    targetPosition += (V * estTime);
                }
                else
                {
                    Vector3 A = !guess && target.type == ObjectTypes.Vehicle ? TargetManager.GetAcceleration(target.tank) : Vector3.zero;
                    if (projectile.rbody.useGravity || WeaponAimSettings.BallisticMissile)
                    {
                        A -= Physics.gravity * projectile.GetGravityScale();
                    }
                    float time = BallisticEquations.SolveBallisticArc(me.position, speed, targetPosition, V, A);

                    if (time != Mathf.Infinity)
                    {
                        // Vector3 originalPosition = targetPosition;
                        targetPosition += (V * time) + (A * time * time / 2);
                    }
                    else
                    {
                        // Only possible if using gravity
                        targetPosition += (V * estTime);
                        Vector3 vector = targetPosition - me.position;
                        if (vector != Vector3.up && vector != Vector3.down)
                        {
                            float angle = Mathf.Atan(vector.y / Mathf.Sqrt(vector.x * vector.x + vector.z * vector.z));
                            Vector3 adjustedVector = new Vector3(vector.x, 0f, vector.z).normalized;

                            float targetAngle;
                            if (angle <= 0f)
                            {
                                targetAngle = (Mathf.PI / 4) - (Mathf.Abs(angle) / 2);
                            }
                            else
                            {
                                targetAngle = (Mathf.PI / 4) + (angle / 2);
                            }

                            float adjustedAngle = (targetAngle + angle) / 2;

                            if (remainingTime > 0f)
                            {
                                float constant = totalTime > 0f ? totalTime : 2.0f;
                                float percentage = Mathf.Clamp(remainingTime, 0, constant) / constant;
                                adjustedAngle = (angle * percentage) + (targetAngle * (1 - percentage));
                            }

                            // Point halfway between angle for maximum range, and angle to target
                            adjustedVector.y = Mathf.Tan(adjustedAngle);
                            targetPosition = adjustedVector + me.position;
                        }
                    }
                }
                ApplyRotation(__instance, projectile, me, targetPosition, reduced);
            }

            // Do missile targeting
            public static bool Prefix(ref SeekingProjectile __instance)
            {
                Projectile projectile = (Projectile)m_MyProjectile.GetValue(__instance);
                bool enemyMissile = projectile.Shooter == null || !!ManSpawn.IsPlayerTeam(projectile.Shooter.Team);
                bool reduced = false;

                if (projectile is MissileProjectile missile && WeaponAimSettings.BallisticMissile)
                {
                    float lifetime = (float)m_MaxBoosterLifetime.GetValue(missile);
                    reduced = lifetime < 0f;
                }

                // Only do leading if is a player, or enemy lead enabled
                if ((enemyMissile && WeaponAimSettings.EnemyMissileLead) || (!enemyMissile && WeaponAimSettings.PlayerMissileLead)) {
                    Visible target = (Visible)GetCurrentTarget.Invoke(__instance, null);
                    Transform me = (Transform)m_MyTransform.GetValue(__instance);

                    // If have a target, use it
                    if (target.IsNotNull())
                    {
                        Vector3 targetPosition = (Vector3)GetTargetAimPosition.Invoke(__instance, null);
                        if (target.rbody != null && (target.rbody.velocity.magnitude > 1f || WeaponAimSettings.BallisticMissile))
                        {
                            CalculateAndApplyRotation(__instance, projectile, target, me, targetPosition, target.rbody.velocity, reduced);
                        }
                        else
                        {
                            ApplyRotation(__instance, projectile, me, targetPosition, reduced);
                        }
                    }

                    // If no target currently visible, steer towards last known position of any target
                    else if (WeaponAimSettings.SmartMissile)
                    {
                        SmartMissile smartMissile = __instance.GetComponent<SmartMissile>();
                        if (smartMissile != null && smartMissile.target != null)
                        {
                            Vector3 targetPosition = smartMissile.position + (Time.time - smartMissile.time) * smartMissile.velocity;
                            if (smartMissile.velocity.magnitude > 1f || WeaponAimSettings.BallisticMissile)
                            {
                                CalculateAndApplyRotation(__instance, projectile, smartMissile.target, me, targetPosition, smartMissile.velocity, reduced, true);
                            }
                            else
                            {
                                ApplyRotation(__instance, projectile, me, targetPosition, reduced);
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
                        Transform me = (Transform)m_MyTransform.GetValue(__instance);
                        Vector3 targetPosition = smartMissile.position + (Time.time - smartMissile.time) * smartMissile.velocity;
                        ApplyRotation(__instance, projectile, me, targetPosition, reduced);
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
            private static void Postfix(ref Projectile __instance, bool seekingRounds)
            {
                // If there's a SeekingProjectile, set SmartMissile
                SeekingProjectile missile = __instance.SeekingProjectile;
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

        [HarmonyPatch(typeof(MissileProjectile))]
        [HarmonyPatch("DeactivateBoosters")]
        public static class BallisticMissilePatch2
        {
            public static void Postfix(ref MissileProjectile __instance)
            {
                // If projectile is not magic bs, enable ballistics
                // bool originalGravity = __instance.CanApplyGravity();
                if (WeaponAimSettings.BallisticMissile)
                {
                    float current = (float) m_MaxBoosterLifetime.GetValue(__instance);
                    if (current > 0f)
                    {
                        m_MaxBoosterLifetime.SetValue(__instance, -current);
                    }
                    else
                    {
                        m_MaxBoosterLifetime.SetValue(__instance, Mathf.NegativeInfinity);
                    }
                }
            }
        }
        [HarmonyPatch(typeof(MissileProjectile))]
        [HarmonyPatch("OnRecycle")]
        public static class ClearBallisticToggle
        {
            public static void Postfix(ref MissileProjectile __instance)
            {
                float current = (float)m_MaxBoosterLifetime.GetValue(__instance);
                if (current != Mathf.NegativeInfinity)
                {
                    m_MaxBoosterLifetime.SetValue(__instance, Mathf.Abs(current));
                }
                else
                {
                    m_MaxBoosterLifetime.SetValue(__instance, Mathf.Abs(0f));
                }
            }
        }
        [HarmonyPatch(typeof(MissileProjectile))]
        [HarmonyPatch("Fire")]
        public static class BallisticMissileFirePatch
        {
            private static readonly FieldInfo m_MaxBoosterLifetime = typeof(MissileProjectile).GetField("m_MaxBoosterLifetime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo m_LifeTime = typeof(Projectile).GetField("m_LifeTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            public struct State
            {
                public float projectileLifetime;
                public float boosterLifetime;
                public float adjustedBoosterLifetime;
            }

            public static bool Prefix(ref MissileProjectile __instance, FireData fireData, out State __state)
            {
                float projectileLifetime = (float)m_LifeTime.GetValue((Projectile) __instance);
                float boosterLifetime = (float)m_MaxBoosterLifetime.GetValue(__instance);
                float adjustedBoosterLifetime = boosterLifetime;

                // Do timed fuse setting
                if (WeaponAimSettings.BallisticMissile)
                {
                    m_LifeTime.SetValue((Projectile) __instance, 0f);
                    if (adjustedBoosterLifetime == 0f)
                    {
                        if (projectileLifetime > 0f)
                        {
                            adjustedBoosterLifetime = projectileLifetime;
                        }
                    }
                    else if (projectileLifetime > 0f)
                    {
                        adjustedBoosterLifetime = Mathf.Min(adjustedBoosterLifetime, projectileLifetime);
                    }

                    m_MaxBoosterLifetime.SetValue(__instance, adjustedBoosterLifetime);
                }

                __state = new State
                {
                    projectileLifetime = projectileLifetime,
                    boosterLifetime = boosterLifetime,
                    adjustedBoosterLifetime = adjustedBoosterLifetime
                };
                return true;
            }

            public static void Postfix(ref MissileProjectile __instance, State __state)
            {
                m_LifeTime.SetValue((Projectile) __instance, __state.projectileLifetime);
                m_MaxBoosterLifetime.SetValue(__instance, __state.boosterLifetime);

                SmartMissile smartMissile = __instance.GetComponent<SmartMissile>();
                if (smartMissile)
                {
                    smartMissile.expireTime = Time.time + __state.adjustedBoosterLifetime;
                    smartMissile.totalTime = __state.adjustedBoosterLifetime;
                    // smartMissile.finalCorrection = false;
                }
            }
        }
    }
}

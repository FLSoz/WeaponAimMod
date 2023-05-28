using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System;
using static CompoundExpression;

namespace WeaponAimMod
{
    public class MissilePatch
    {
        private static readonly FieldInfo m_MaxBoosterLifetime = typeof(MissileProjectile).GetField("m_MaxBoosterLifetime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        // Target leading for missiles
        [HarmonyPatch(typeof(SeekingProjectile), "FixedUpdate")]
        public static class PatchMissiles
        {
            private static readonly FieldInfo m_MyProjectile = typeof(SeekingProjectile).GetField("m_MyProjectile", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo m_TurnSpeed = typeof(SeekingProjectile).GetField("m_TurnSpeed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo m_ApplyRotationTowardsTarget = typeof(SeekingProjectile).GetField("m_ApplyRotationTowardsTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly MethodInfo GetCurrentTarget = typeof(SeekingProjectile).GetMethod("GetCurrentTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly MethodInfo GetTargetAimPosition = typeof(SeekingProjectile).GetMethod("GetTargetAimPosition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            private static void ApplyRotationToRbody(Rigidbody rbody, Quaternion change, bool rotateTransform = true)
            {
                Vector3 newVelocity = change * rbody.velocity;
                rbody.velocity = newVelocity;
                if (rotateTransform)
                {
                    Quaternion originalRot = rbody.rotation;
                    Quaternion newRot = change * originalRot;
                    Quaternion alignedRot = Quaternion.RotateTowards(newRot, Quaternion.LookRotation(newVelocity), 45 * Time.fixedDeltaTime);
                    rbody.MoveRotation(alignedRot);
                    WeaponAimMod.logger.Trace($"Rotating missile. Original: {originalRot.eulerAngles}, New: {newRot.eulerAngles}, Velocity Aligned:{Quaternion.LookRotation(newVelocity).eulerAngles}, Adjusted Target: {alignedRot.eulerAngles}, Updated {rbody.rotation.eulerAngles}");
                }
            }

            // Actually apply rotation towards target
            private static void ApplyRotation(SeekingProjectile __instance, Projectile projectile, Vector3 targetDirection, bool reduced)
            {
                Vector3 normalized = Vector3.Cross(projectile.rbody.velocity, targetDirection).normalized;
                float b = Vector3.Angle(projectile.trans.forward, targetDirection);

                float turnSpeed = (float)m_TurnSpeed.GetValue(__instance);
                if (reduced)
                {
                    // cripple turning speed at end of lifetime
                    turnSpeed = Mathf.Max(turnSpeed * 0.1f, 5f);
                }

                bool applyRotation = (bool)m_ApplyRotationTowardsTarget.GetValue(__instance);
                Quaternion changeRot = Quaternion.AngleAxis(Mathf.Min(turnSpeed * Time.fixedDeltaTime, b), normalized);
                ApplyRotationToRbody(projectile.rbody, changeRot, applyRotation);
            }
            
            // Predict where enemy is going, then apply rotation towards that
            private static void CalculateAndApplyRotation(
                SeekingProjectile __instance, Projectile projectile, Visible target,
                Vector3 targetPosition, Vector3 targetVelocity, Vector3 targetAcceleration, bool reduced, bool guess = false)
            {
                Vector3 myPosition = __instance.transform.position;
                SmartMissile smartMissile = __instance.GetComponent<SmartMissile>();
                float remainingTime = smartMissile.expireTime - Time.time;
                float speed = smartMissile.GetMaxVelocity(remainingTime);
                bool disableBallisticOverride = smartMissile.disableBallistic;
                if (smartMissile.velocityOverride > 0.0f)
                {
                    speed = smartMissile.velocityOverride;
                }

                Vector3 A = !guess && target.type == ObjectTypes.Vehicle ? TargetManager.GetAcceleration(target.tank) : targetAcceleration;
                Vector3 relDist = targetPosition - myPosition;
                float estTime = relDist.magnitude / speed;

                bool accelerationGravityAdjusted = false;
                if (projectile.rbody.useGravity)
                {
                    A -= Physics.gravity * projectile.GetGravityScale();
                    accelerationGravityAdjusted = true;
                }
                bool failedBallistic = false;
                bool ballisticMissileActive = !disableBallisticOverride && WeaponAimSettings.BallisticMissile;

                float time = BallisticEquations.SolveBallisticArc(myPosition, speed, targetPosition, targetVelocity, A, out Vector3 targetDirection);
                if (time != Mathf.Infinity)
                {
                    estTime = time;
                }
                else
                {
                    failedBallistic = true;
                    targetDirection = relDist;
                }

                if (estTime <= remainingTime || remainingTime <= 0.0f)
                {
                    goto DoRotateNow;
                }
                else
                {
                    // we have failed timeout check. Potentially, no solution was found
                    if (!accelerationGravityAdjusted && ballisticMissileActive)
                    {
                        A -= Physics.gravity * projectile.GetGravityScale();
                        failedBallistic = false;
                    }

                    // if no solution was found, then we shouldn't bother looking for a new ballistic arc
                    // if we only failed due to timeout, then we can go looking for a new ballistic arc with gravity
                    float remainingDistance = remainingTime * speed;
                    if (!failedBallistic && remainingDistance < relDist.magnitude)
                    {
                        // we try and search for a path using a barrel length of the maximal engagement envelope
                        time = BallisticEquations.SolveBallisticArc(myPosition, speed, targetPosition, targetVelocity, A, out targetDirection, remainingDistance);
                    }
                    else
                    {
                        // no solution was possible
                        goto DoRotateNow;
                    }

                    if (time != Mathf.Infinity)
                    {
                        // we succeeded with gravity assisted ballistic arc - go use that
                        goto DoRotateNow;
                    }
                    else
                    {
                        // No solution is possible
                        Vector3 relVec = relDist.normalized;
                        if (ballisticMissileActive && relVec != Vector3.up && relVec != Vector3.down)
                        {
                            // if projectile uses gravity
                            float angle = Mathf.Atan(relVec.y / Mathf.Sqrt(relVec.x * relVec.x + relVec.z * relVec.z));
                            Vector3 adjustedVector = new Vector3(relVec.x, 0f, relVec.z).normalized;

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
                                float totalTime = smartMissile.totalTime;
                                float constant = totalTime > 0f ? totalTime : 2.0f;
                                float percentage = Mathf.Clamp(remainingTime, 0, constant) / constant;
                                adjustedAngle = (angle * percentage) + (targetAngle * (1 - percentage));
                            }

                            // Point halfway between angle for maximum range, and angle to target
                            adjustedVector.y = Mathf.Tan(adjustedAngle);
                            targetDirection = adjustedVector;
                        }
                        else
                        {
                            // ballistic, but somehow directly above/below, in which case we want to just go
                            // OR - no valid path. Just make best effort then
                            targetDirection = relDist;
                        }
                    }
                }
                DoRotateNow:
                ApplyRotation(__instance, projectile, targetDirection, reduced);
            }

            // Do missile targeting
            [HarmonyPrefix]
            public static bool Prefix(SeekingProjectile __instance)
            {
                Projectile projectile = (Projectile)m_MyProjectile.GetValue(__instance);
                bool enemyMissile = projectile.Shooter.IsNull() || !ManSpawn.IsPlayerTeam(projectile.Shooter.Team);
                bool reduced = false;

                // missile always have a SmartMissile on there, even if smart missiles is disabled
                SmartMissile smartMissile = __instance.GetComponent<SmartMissile>();
                bool ballisticMissileActive = !smartMissile.disableBallistic && WeaponAimSettings.BallisticMissile;
                if (projectile is MissileProjectile missile && ballisticMissileActive)
                {
                    reduced = !smartMissile.boostersFiring;
                }

                Vector3 myPosition = __instance.transform.position;
                // Only do leading if is a player, or enemy lead enabled, and missile hasn't specified we should disable target leading
                if (!smartMissile.disableLead && ((enemyMissile && WeaponAimSettings.EnemyMissileLead) || (!enemyMissile && WeaponAimSettings.PlayerMissileLead))) {
                    Visible target = (Visible)GetCurrentTarget.Invoke(__instance, null);

                    // If have a target, use it
                    if (target.IsNotNull())
                    {
                        Vector3 targetPosition = (Vector3)GetTargetAimPosition.Invoke(__instance, null);
                        if (target.rbody.IsNotNull() && (target.rbody.velocity.magnitude > 1f || ballisticMissileActive))
                        {
                            CalculateAndApplyRotation(__instance, projectile, target, targetPosition, target.rbody.velocity, Vector3.zero, reduced);
                        }
                        else
                        {
                            ApplyRotation(__instance, projectile, targetPosition - myPosition, reduced);
                        }
                    }
                    // If no target currently visible, steer towards last known position of any target
                    else if (WeaponAimSettings.SmartMissile)
                    {
                        if (smartMissile.target.IsNotNull())
                        {
                            float elapsedTime = Time.time - smartMissile.shotTime;
                            Vector3 targetPosition = smartMissile.targetPosition + (elapsedTime * smartMissile.targetVelocity) + (elapsedTime * elapsedTime * smartMissile.targetAcceleration / 2);
                            Vector3 targetVelocity = smartMissile.targetVelocity + (elapsedTime * smartMissile.targetAcceleration);
                            if (smartMissile.targetVelocity.magnitude > 1f || ballisticMissileActive)
                            {
                                CalculateAndApplyRotation(__instance, projectile, smartMissile.target,
                                    targetPosition, targetVelocity, smartMissile.targetAcceleration, reduced, true);
                            }
                            else
                            {
                                ApplyRotation(__instance, projectile, targetPosition - myPosition, reduced);
                            }
                        }
                        else
                        {
                            ApplyRotationToRbody(projectile.rbody, Quaternion.identity);
                        }
                    }
                    else
                    {
                        ApplyRotationToRbody(projectile.rbody, Quaternion.identity);
                    }
                    return false;
                }
                // If no lead, but smart missiles selected - rotate towards that
                else if (WeaponAimSettings.SmartMissile)
                {
                    if (smartMissile.target.IsNotNull())
                    {
                        float elapsedTime = Time.time - smartMissile.shotTime;
                        Vector3 targetPosition = smartMissile.targetPosition + (elapsedTime * smartMissile.targetVelocity) + (elapsedTime * elapsedTime * smartMissile.targetAcceleration / 2);
                        ApplyRotation(__instance, projectile, targetPosition - myPosition, reduced);
                    }
                    else
                    {
                        ApplyRotationToRbody(projectile.rbody, Quaternion.identity);
                    }
                    return false;
                }
                return true;
            }

            // Update smart missile
            [HarmonyPostfix]
            public static void Postfix(SeekingProjectile __instance)
            {
                Visible target = (Visible)GetCurrentTarget.Invoke(__instance, null);
                SmartMissile smartMissile = __instance.GetComponent<SmartMissile>();
                if (smartMissile.IsNotNull() && target.IsNotNull())
                {
                    Vector3 targetPosition = (Vector3)GetTargetAimPosition.Invoke(__instance, null);
                    smartMissile.target = target;
                    smartMissile.shotTime = Time.time;
                    smartMissile.targetPosition = targetPosition;
                    smartMissile.targetVelocity = target.rbody ? target.rbody.velocity : Vector3.zero;

                    TargetManager manager = target.GetComponent<TargetManager>();
                    if (manager.IsNotNull())
                    {
                        smartMissile.targetAcceleration = manager.Acceleration;
                    }
                }
            }
        }

        // Add SmartMissile, tell it to go towards tank's current target
        [HarmonyPatch(typeof(Projectile), "Fire")]
        public static class MissileFirePatch
        {
            [HarmonyPostfix]
            public static void Postfix(Projectile __instance, bool seekingRounds)
            {
                // If there's a SeekingProjectile, set SmartMissile
                SeekingProjectile missile = __instance.SeekingProjectile;
                if (missile.IsNotNull() && seekingRounds)
                {
                    SmartMissile smartMissile = __instance.GetComponent<SmartMissile>();
                    if (smartMissile.IsNull())
                    {
                        smartMissile = __instance.gameObject.AddComponent<SmartMissile>();
                        smartMissile.Init();
                    }
                    Tank shooter = __instance.Shooter;
                    Visible target;
                    if (shooter.control.targetType == ObjectTypes.Vehicle)
                    {
                        target = shooter.Vision.GetFirstVisibleTechIsEnemy(shooter.Team);
                    }
                    else
                    {
                        target = shooter.Vision.GetFirstVisible();
                    }

                    if (target.IsNotNull())
                    {
                        smartMissile.target = target;
                        smartMissile.shotTime = Time.time;
                        smartMissile.targetPosition = target.centrePosition;
                        if (smartMissile.velocityOverride > 0.0f)
                        {
                            smartMissile.targetVelocity = target.rbody.velocity.normalized * smartMissile.velocityOverride;
                        }
                        else
                        {
                            smartMissile.targetVelocity = target.rbody.velocity;
                        }
                    }
                    else
                    {
                        smartMissile.target = null;
                    }
                }
                return;
            }
        }

        [HarmonyPatch(typeof(MissileProjectile), "DeactivateBoosters")]
        public static class PatchBoosterDeactivation
        {
            [HarmonyPostfix]
            public static void Postfix(MissileProjectile __instance)
            {
                SmartMissile smartMissile = __instance.GetComponent<SmartMissile>();
                smartMissile.boostersFiring = false;
            }
        }

        [HarmonyPatch(typeof(MissileProjectile), "ActivateBoosters")]
        public static class PatchBoosterActivation
        {
            [HarmonyPostfix]
            public static void Postfix(MissileProjectile __instance)
            {
                SmartMissile smartMissile = __instance.GetComponent<SmartMissile>();
                smartMissile.boostersFiring = true;
            }
        }

        // Note: not all missiles have SmartMissile
        // SmartMissile is tied to Seeking rounds, not missile rounds
        [HarmonyPatch(typeof(MissileProjectile), "Fire")]
        public static class BallisticMissileFirePatch
        {
            private static readonly FieldInfo m_MaxBoosterLifetime = typeof(MissileProjectile).GetField("m_MaxBoosterLifetime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo m_LifeTime = typeof(Projectile).GetField("m_LifeTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            public struct State
            {
                public float projectileLifetime;
                public float boosterLifetime;
                public float adjustedBoosterLifetime;
                public SmartMissile smartMissile;
            }

            [HarmonyPrefix]
            public static bool Prefix(MissileProjectile __instance, FireData fireData, out State __state)
            {
                float projectileLifetime = (float)m_LifeTime.GetValue((Projectile) __instance);
                float boosterLifetime = (float)m_MaxBoosterLifetime.GetValue(__instance);
                float adjustedBoosterLifetime = boosterLifetime;

                SmartMissile smartMissile = __instance.GetComponent<SmartMissile>();
                bool disableBallisticOverride = false;
                if (smartMissile.IsNotNull())
                {
                    disableBallisticOverride = smartMissile.disableBallistic;
                }

                // Do timed fuse setting
                if (!disableBallisticOverride && WeaponAimSettings.BallisticMissile)
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
                    adjustedBoosterLifetime = adjustedBoosterLifetime,
                    smartMissile = smartMissile
                };
                return true;
            }

            [HarmonyPostfix]
            public static void Postfix(MissileProjectile __instance, Vector3 fireDirection, State __state)
            {
                m_LifeTime.SetValue((Projectile) __instance, __state.projectileLifetime);
                m_MaxBoosterLifetime.SetValue(__instance, __state.boosterLifetime);

                SmartMissile smartMissile = __state.smartMissile;
                if (smartMissile)
                {
                    smartMissile.expireTime = Time.time + __state.adjustedBoosterLifetime;
                    smartMissile.totalTime = __state.adjustedBoosterLifetime;
                    // smartMissile.finalCorrection = false;
                }

                // forcibly change rotation so acceleration doesn't do wacky-looking things
                // that are technically correct, but look weird
                // __instance.trans.rotation = Quaternion.LookRotation(__instance.rbody.velocity);

                // force spawn projectiles farther from barrel to allow for that rotation without colliding with stuff
                __instance.trans.position += fireDirection.normalized * 2.0f;
            }
        }
    }
}

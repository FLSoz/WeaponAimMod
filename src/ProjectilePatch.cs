using HarmonyLib;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text;
using RuntimeDebugDraw;
using System.Collections.Generic;
using System.Reflection.Emit;
using static CompoundExpression;

namespace WeaponAimMod
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
            private static readonly FieldInfo m_CannonBarrels = AccessTools.Field(typeof(ModuleWeaponGun), "m_CannonBarrels");

            [HarmonyPriority(Priority.Low)]
            public static bool Prefix(TargetAimer __instance)
            {
                try
                {
                    TankBlock ___m_Block = (TankBlock)m_Block.GetValue(__instance);
                    float ___m_ChangeTargetTimeout = (float)m_ChangeTargetTimeout.GetValue(__instance);
                    float ___m_ChangeTargetInteval = (float)m_ChangeTargetInteval.GetValue(__instance);
                    Tank tank = ___m_Block.tank;

                    if (___m_Block.IsNull() || tank.IsNull())
                    {
                        return false;
                    }
                    else
                    {
                        Visible manualTarget = GetManualTarget(___m_Block);
                        if (manualTarget)
                        {
                            PatchAiming.Target.SetValue(__instance, manualTarget);
                        }
                        else if (__instance.Target.IsNotNull() && (!__instance.Target.isActive || Time.time > ___m_ChangeTargetTimeout || !tank.Vision.CanSee(__instance.Target)))
                        {
                            __instance.Reset();
                            m_ChangeTargetTimeout.SetValue(__instance, ___m_ChangeTargetTimeout);
                        }
                        if (!__instance.HasTarget && tank.control.targetType != ObjectTypes.Null)
                        {
                            if (tank.control.targetType == ObjectTypes.Vehicle)
                            {
                                WeaponAimMod.logger.Trace($"Selecting new target. Current block details: {___m_Block}, tank {tank}, tank team: {tank.Team}");
                                PatchAiming.Target.SetValue(__instance, tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team));
                            }
                            else
                            {
                                PatchAiming.Target.SetValue(__instance, tank.Vision.GetFirstVisible());
                            }
                            m_ChangeTargetTimeout.SetValue(__instance, Time.time + ___m_ChangeTargetInteval);
                        }
                        if (__instance.HasTarget)
                        {
                            try
                            {
                                m_TargetPosition.SetValue(__instance, __instance.Target.GetAimPoint(___m_Block.trans.position));
                            }
                            catch (Exception e)
                            {
                                WeaponAimMod.logger.Error(e);
                                WeaponAimMod.logger.Error($"Failed to get aim point for {__instance.Target.name}");
                                WeaponAimMod.logger.Error($"{__instance.Target.gameObject.IsNull()}");

                                m_TargetPosition.SetValue(__instance, __instance.Target.transform.position);

                                throw new Exception("CAUGHT AN AIM POINT FAILURE", e);
                            }
                        }
                        /* else
                        {
                            __instance.Reset();
                        } */
                    }
                }
                catch (Exception e)
                {
                    WeaponAimMod.logger.Error("[WeaponAimMod] TargetAimer.UpdateTarget PATCH FAILED");
                    WeaponAimMod.logger.Error(e.ToString());
                }
                return false;
            }

            public static void Postfix(TargetAimer __instance)
            {
                try
                {
                    Visible target = __instance.Target;
                    FireData fireData = __instance.GetComponentInParent<FireData>();
                    if (fireData.IsNotNull() && __instance.HasTarget && !Singleton.Manager<ManPauseGame>.inst.IsPaused && ((target.type == ObjectTypes.Vehicle && target.tank.IsNotNull()) || (target.type == ObjectTypes.Block && target.block.IsNotNull())))
                    {
                        TankBlock block = (TankBlock)ProjectilePatch.m_Block.GetValue(__instance);
                        Tank tank = (bool)(UnityEngine.Object)block ? block.tank : (Tank)null;

                        string name = block ? block.name : "UNKNOWN";
                        ModuleWeaponAimHelper helper = block.GetComponent<ModuleWeaponAimHelper>();

                        bool enemyWeapon = tank.IsNull() || !ManSpawn.IsPlayerTeam(tank.Team);
                        if (helper.IsNotNull())
                        {
                            TimedFuseData timedFuse = helper.timedFuseData;
                            float muzzleVelocity = fireData.m_MuzzleVelocity;
                            if (helper.muzzleVelocityOverride > 0.0f)
                            {
                                muzzleVelocity = helper.muzzleVelocityOverride;
                            }
                            if (((enemyWeapon && WeaponAimSettings.EnemyLead) || (!enemyWeapon && WeaponAimSettings.PlayerLead)) && muzzleVelocity > 0.0f)
                            {
                                Vector3 AimPointVector = (Vector3)ProjectilePatch.m_TargetPosition.GetValue(__instance);
                                Vector3 turretCenter = __instance.transform.position + helper.gimbalOffset;
                                if (!helper.ignoreOffset)
                                {
                                }

                                Vector3 relDist = AimPointVector - turretCenter;
                                WeaponRound bulletPrefab = fireData.m_BulletPrefab;

                                bool useGravity = helper.useGravity;
                                if (!useGravity && WeaponAimSettings.BallisticMissile)
                                {
                                    if (bulletPrefab.IsNotNull() && bulletPrefab is MissileProjectile)
                                    {
                                        useGravity = true;
                                    }  
                                }

                                Rigidbody rbodyTank = __instance.GetComponentInParent<Tank>().rbody;

                                Vector3 angularToggle = rbodyTank.angularVelocity;
                                Vector3 relativeVelocity = (__instance.Target.rbody ? __instance.Target.rbody.velocity : Vector3.zero) - (rbodyTank.velocity + angularToggle);

                                float time = relDist.magnitude / muzzleVelocity;

                                // relative acceleratino will accelerate it up
                                Vector3 relativeBaseAcceleration = target.type == ObjectTypes.Vehicle ? TargetManager.GetAcceleration(target.tank) : Vector3.zero;
                                Vector3 relativeFullAcceleration = relativeBaseAcceleration - (useGravity ? Physics.gravity : Vector3.zero);

                                float offset = helper.ignoreOffset ? 0.0f : helper.barrelLength;
                                Vector3 gimbalHeightOffset = Vector3.zero;
                                Quaternion extraRotation = Quaternion.identity;

                                // Gimbal height offset is intermediate gimbal height
                                // We ignore horizontal offset, and we also rotate frame of reference so if base gimbal is not Y, we force it to become Y
                                if (helper.firstAxis == GimbalAimer.AxisConstraint.X)
                                {
                                    extraRotation = Quaternion.Euler(0, 0, 90.0f);
                                    /* if (!helper.ignoreOffset)
                                    {
                                        gimbalHeightOffset = helper.intermediateGimbalOffset.x * Vector3.up;
                                    } */
                                }
                                else if (helper.firstAxis == GimbalAimer.AxisConstraint.Y)
                                {
                                    /* if (!helper.ignoreOffset)
                                    {
                                        gimbalHeightOffset = helper.intermediateGimbalOffset.y * Vector3.up;
                                    } */
                                }

                                float exactTime = BallisticEquations.SolveBallisticArc(
                                    Vector3.zero, muzzleVelocity, extraRotation * __instance.transform.InverseTransformDirection(AimPointVector - turretCenter),
                                    extraRotation * __instance.transform.InverseTransformDirection(relativeVelocity), extraRotation * __instance.transform.InverseTransformDirection(relativeFullAcceleration),
                                    out Vector3 direction, barrelLength: offset, useHighArc: helper.useHighArc
                                );

                                Vector3 adjIntercept = AimPointVector + (relativeVelocity * time);
                                if (exactTime != Mathf.Infinity)
                                {
                                    time = exactTime;
                                    adjIntercept = AimPointVector + (relativeVelocity * time) + ((relativeBaseAcceleration / 2) * time * time);
#if DEBUG
                                    if (WeaponAimMod.DEBUG)
                                    {
                                        Draw.DrawText(adjIntercept, $"time: {exactTime}, acc: {relativeBaseAcceleration}, vel: {relativeVelocity}, gravity: {useGravity}, highArc: {helper.useHighArc}", Color.green);
                                    }
#endif
                                    if (!helper.ignoreOffset)
                                    {
                                        Vector3 actualDirection =  __instance.transform.TransformDirection(Quaternion.Inverse(extraRotation) * direction);
                                        /*
                                        Vector3 relTarget = adjIntercept - turretCenter;
                                        if (useGravity)
                                        {
                                            Vector3 groundLine = new Vector3(relTarget.x, 0.0f, relTarget.z);
                                            float groundDist = groundLine.magnitude;
                                            float dirGround = (groundDist * offset) / (offset + (muzzleVelocity * exactTime));
                                            actualDirection = groundLine.normalized * dirGround;
                                            actualDirection.y = Mathf.Sqrt((offset * offset) - (dirGround * dirGround));
#if DEBUG
                                            if (WeaponAimMod.DEBUG)
                                            {
                                                Draw.DrawText(turretCenter + (Vector3.up * 2), $"dir: {actualDirection}, rel: {relTarget}, offset: {offset}\nvelocity: {muzzleVelocity}, dist: {groundDist}, dirGround: {dirGround}\nme: {turretCenter}, target: {AimPointVector}", Color.green);
                                            }
#endif
                                        }
                                        else
                                        {
                                            actualDirection = relTarget;
#if DEBUG
                                            if (WeaponAimMod.DEBUG)
                                            {
                                                Draw.DrawText(turretCenter + (Vector3.up * 2), $"dir: {actualDirection}, rel: {relTarget}, offset: {offset}\nvelocity: {muzzleVelocity}\nme: {turretCenter}, target: {AimPointVector}", Color.green);
                                            }
#endif
                                        }
                                        */

                                        // we know direction barrel must be facing
                                        Vector3 actualOffset = actualDirection.normalized * offset;
                                        adjIntercept -= actualOffset;
                                        // adjIntercept -= helper.intermediateGimbalOffset;
#if DEBUG
                                        if (WeaponAimMod.DEBUG)
                                        {
                                            Vector3 curr = turretCenter;
                                            Vector3 next = turretCenter;
                                            Vector3 velocity = (actualDirection.normalized * muzzleVelocity) + rbodyTank.velocity + angularToggle;
                                            /*
                                            for (float incrTime = 0.1f; incrTime < exactTime; incrTime += 0.1f)
                                            {
                                                next = turretCenter + (velocity * incrTime) + (useGravity ? (Physics.gravity * incrTime * incrTime / 2) : Vector3.zero);
                                                Draw.DrawLine(curr, next, Color.green);
                                                curr = next;
                                            } */
                                            ModuleWeaponGun gun = __instance.GetComponent<ModuleWeaponGun>();
                                            CannonBarrel[] barrels = (CannonBarrel[])m_CannonBarrels.GetValue(gun);
                                            foreach (CannonBarrel barrel in barrels)
                                            {
                                                Vector3 basePosition = barrel.projectileSpawnPoint.position;
                                                curr = basePosition;
                                                next = basePosition;
                                                for (float incrTime = 0.1f; incrTime < exactTime; incrTime += 0.1f)
                                                {
                                                    next = basePosition + (velocity * incrTime) + (useGravity ? (Physics.gravity * incrTime * incrTime / 2) : Vector3.zero);
                                                    Draw.DrawLine(curr, next, Color.blue);
                                                    curr = next;
                                                }
                                            }

                                            Vector3 actualIntercept = turretCenter + (velocity * exactTime) + (useGravity ? (Physics.gravity * exactTime * exactTime / 2) : Vector3.zero);

                                            Draw.DrawLine(turretCenter, adjIntercept, Color.green);
                                            Draw.DrawLine(turretCenter, actualIntercept, Color.cyan);
                                            Draw.DrawLine(turretCenter, turretCenter + direction.normalized * 5.0f, Color.yellow);
                                            Draw.DrawLine(turretCenter, turretCenter + actualOffset, Color.red);
                                            TargetManager.DrawBox(turretCenter + actualDirection.normalized * offset, new Bounds(turretCenter + actualDirection.normalized * offset, Vector3.one * 0.5f), Color.red);
                                            TargetManager.DrawBox(actualIntercept, new Bounds(actualIntercept, Vector3.one * 0.5f), Color.red);
                                            TargetManager.DrawBox(actualIntercept - actualOffset, new Bounds(actualIntercept - actualOffset, Vector3.one * 0.5f), Color.yellow);
                                        }
#endif
                                    }
                                }

                                if (timedFuse.IsNotNull())
                                {
                                    timedFuse.m_FuseTime = time;
                                }

                                ProjectilePatch.m_TargetPosition.SetValue(__instance, adjIntercept);
                            }
                            // Either disabled for enemy, or is a beam weapon
                            else
                            {
                                if (timedFuse.IsNotNull())
                                {
                                    timedFuse.m_FuseTime = 0.0f;
                                }
                            }
                        }
                    }
                }
                catch (NullReferenceException exception)
                {
                    WeaponAimMod.logger.Warn("[WeaponAimMod] TargetAimer.UpdateTarget PATCH FAILED");
                    WeaponAimMod.logger.Warn(exception.ToString());
                }
                catch (Exception exception)
                {
                    WeaponAimMod.logger.Error("[WeaponAimMod] TargetAimer.Updatetarget PATCH CRITICAL FAILURE");
                    WeaponAimMod.logger.Error(exception);
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

            private static bool Prefix(Projectile __instance, FireData fireData, out float __state)
            {
                __state = (float) m_LifeTime.GetValue(__instance);

                // Do timed fuse setting
                TimedFuseData timedFuse = fireData.GetComponentInParent<TimedFuseData>();
                if (timedFuse.IsNotNull() && timedFuse.m_FuseTime > 0.0f)
                {
                    // LK Pike is somehow abusing this to get infinite range
                    float fuseTime = timedFuse.m_FuseTime + timedFuse.offset;
                    WeaponAimMod.logger.Trace($"Setting fuse on {__instance.name} to ${fuseTime} seconds");
                    m_LifeTime.SetValue(__instance, fuseTime);
                }
                return true;
            }

            private static void Postfix(Projectile __instance, float __state)
            {
                m_LifeTime.SetValue(__instance, __state);
            }
        }

        private const float tolerance = 5.0f;
        [HarmonyPatch(typeof(ModuleWeapon), "ControlInputTargeted")]
        public static class PatchAIFireControl
        {
            public static Vector3 GetTargetPosition(ModuleWeapon __instance, Vector3 providedTarget)
            {
                TargetAimer aimer = (TargetAimer)PatchAutoAim.m_TargetAimer.GetValue(__instance);
                if (aimer.IsNotNull() && aimer.HasTarget) {
                    return (Vector3)PatchAutoAim.m_TargetPosition2.GetValue(aimer);
                }
                return providedTarget;
            }

            public static bool IsMissile(ModuleWeapon __instance)
            {
                IModuleWeapon weaponComponent = (IModuleWeapon)PatchAutoAim.m_WeaponComponent.GetValue(__instance);
                return weaponComponent is ModuleWeaponGun && ((ModuleWeaponGun)weaponComponent).m_SeekingRounds;
            }

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                List<CodeInstruction> instrList = instructions.ToList();
                instrList.RemoveRange(2, 2);
                instrList.Insert(2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatchAIFireControl), nameof(GetTargetPosition))));

                int conditionalBeginIndex = 0;
                Label myLabel = generator.DefineLabel();
                for(int i = 0; i < instrList.Count; i++)
                {
                    CodeInstruction instruction = instrList[i];
                    // right before we begin conditional. Here is where we insert our jump
                    if (instruction.opcode == OpCodes.Stloc_2)
                    {
                        conditionalBeginIndex = i + 1;
                    }

                    if (instruction.opcode == OpCodes.Ble_Un_S)
                    {
                        // Add my label
                        instrList[i+1].labels.Add(myLabel);
                        break;
                    }
                }

                // insert jump to conditional block
                // here is where we check for missiles, and always fire in that case
                instrList.InsertRange(conditionalBeginIndex, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatchAIFireControl), nameof(IsMissile))),
                    new CodeInstruction(OpCodes.Brtrue_S, myLabel)
                });
                return instrList;
            }
        }

        // Use TargetAimer's target position instead of calculating it again
        [HarmonyPatch(typeof(ModuleWeapon), "UpdateAutoAimBehaviour")]
        public static class PatchAutoAim
        {
            public static readonly FieldInfo m_TargetPosition = typeof(ModuleWeapon).GetField("m_TargetPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo m_TargetPosition2 = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo m_WeaponComponent = typeof(ModuleWeapon).GetField("m_WeaponComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly MethodInfo FiringData = typeof(ModuleWeaponGun).GetMethod("get_FiringData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            public static readonly FieldInfo m_TargetAimer = typeof(ModuleWeapon).GetField("m_TargetAimer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            // public static readonly FieldInfo m_ChangeTargetInteval = typeof(ModuleWeapon).GetField("m_ChangeTargetInteval", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo m_SeekingProjectile = typeof(Projectile).GetField("m_SeekingProjectile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo m_VisionConeAngle = typeof(SeekingProjectile).GetField("m_VisionConeAngle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            public static Vector3 GetTargetPosition(TargetAimer targetAimer)
            {
                return (Vector3)m_TargetPosition2.GetValue(targetAimer);
            }
            // Use already calculated target position instead of getting aim point again
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> instrList = instructions.ToList();
                int removePositionStartIndex = 0;
                for(int i = 0; i < instrList.Count; i++)
                {
                    CodeInstruction instruction = instrList[i];
                    if (instruction.opcode == OpCodes.Callvirt && (MethodInfo) instruction.operand == AccessTools.Method(typeof(Visible), "GetAimPoint"))
                    {
                        Console.WriteLine("Removing GetAimPoint");
                        removePositionStartIndex = i - 2;
                        break;
                    }
                }
                // get rid of unnecessary variable
                instrList.RemoveRange(removePositionStartIndex, 3);
                instrList.Insert(removePositionStartIndex, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatchAutoAim), nameof(GetTargetPosition))));
                return instrList;
            }
            // Always fire if we're missiles
            /*
            public static void Postfix(ModuleWeapon __instance)
            {
                if (!__instance.FireControl && __instance.m_AutoFire && __instance.block.tank.IsAIControlled())
                {
                    TargetAimer targetAimer = (TargetAimer)m_TargetAimer.GetValue(__instance);
                    if (targetAimer.HasTarget)
                    {
                        IModuleWeapon weaponComponent = (IModuleWeapon)m_WeaponComponent.GetValue(__instance);
                        if (weaponComponent is ModuleWeaponGun && ((ModuleWeaponGun)weaponComponent).m_SeekingRounds)
                        {
                            __instance.FireControl = true;
                        }
                    }
                }
            }
            */
            public static Exception Finalizer(Exception __exception)
            {
                if (__exception != null)
                {
                    WeaponAimMod.logger.Error($"Game FAILED at UpdateAutoAimBehaviour:\n{__exception}");
                }
                return null;
            }
        }

        [HarmonyPatch(typeof(Visible), "GetAimPoint")]
        public static class PatchAimPoint
        {
            public static bool Prefix(Visible __instance, Vector3 origin, ref Vector3 __result)
            {
                if (__instance.type == ObjectTypes.Vehicle)
                {
                    if (__instance.tank.IsNotNull())
                    {
                        __result = __instance.tank.control.GetWeaponTargetLocation(origin);
                    }
                    else if (__instance.IsNull() || __instance.gameObject.IsNull())
                    {
                        // null GameObject? - visible has been deleted, tell it to aim at aimer origin
                        __result = origin;
                    }
                    else
                    {
                        // Visible has null tank??????
                        __result = __instance.transform.position;
                    }
                }
                else
                {
                    __result = __instance.centrePosition;
                }
                return false;
            }
        }
    }
}

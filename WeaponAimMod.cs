﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TechComponentInjector;
using System.Reflection;
using HarmonyLib;
using BlockChangePatcher;
using UnityEngine;

namespace WeaponAimMod
{
    public class WeaponAimMod : ModBase
    {
        internal const string HarmonyID = "flsoz.ttmm.weaponaim.mod";
        internal static readonly Harmony harmony = new Harmony(HarmonyID);
        private static bool Inited = false;

        internal static bool DEBUG = true;

        internal static Logger logger;
        internal static void ConfigureLogger()
        {
            logger = new Logger("WeaponAimMod");
            logger.Info("Logger is setup");
        }

        public static int InitOrder = 999;

        public static Type[] LoadBefore()
        {
            return new Type[] { typeof(TechComponentInjector.TechComponentInjector), typeof(BlockChangePatcherMod) };
        }

        internal static Change change;

        private class WeaponConditional : CustomConditional
        {
            public override bool Validate(BlockMetadata blockData)
            {
                Transform target = blockData.blockPrefab;
                // no leading on flamethrowers
                ModuleWeaponFlamethrower flamethrower = target.GetComponent<ModuleWeaponFlamethrower>();
                if (flamethrower != null)
                {
                    if (blockData.VanillaID > 0)
                    {
                        logger.Debug($"BLOCK {blockData.VanillaID}) IS FLAMETHROWER");
                    }
                    else
                    {
                        logger.Debug($"BLOCK {blockData.BlockID} ({blockData.SessionID}) IS FLAMETHROWER");
                    }
                    return false;
                }
                if (blockData.VanillaID > 0)
                {
                    logger.Debug($"TESTING BLOCK {blockData.VanillaID})");
                }
                else
                {
                    logger.Debug($"TESTING BLOCK {blockData.BlockID} ({blockData.SessionID})");
                }

                FireDataShotgun shotgun = target.GetComponent<FireDataShotgun>();
                if (shotgun != null)
                {
                    if (blockData.VanillaID > 0)
                    {
                        logger.Debug($"BLOCK {blockData.VanillaID}) IS SHOTGUN");
                    }
                    else
                    {
                        logger.Debug($"BLOCK {blockData.BlockID} ({blockData.SessionID}) IS SHOTGUN");
                    }
                    return false;
                }

                ModuleWeaponGun moduleWeaponGun = target.GetComponent<ModuleWeaponGun>();
                if (moduleWeaponGun != null)
                {
                    logger.Debug($"ModuleWeaponGun found");
                    FireData fireData = target.GetComponent<FireData>();
                    // no patching shotguns
                    if (fireData != null)
                    {
                        logger.Debug($"FireData present");
                        // no leading for beam weapons
                        CannonBarrel[] cannonBarrels = target.GetComponentsInChildren<CannonBarrel>(true);
                        if (cannonBarrels != null)
                        {
                            foreach (CannonBarrel cannonBarrel in cannonBarrels)
                            {
                                if (cannonBarrel.beamWeapon != null)
                                {
                                    if (blockData.VanillaID > 0)
                                    {
                                        logger.Warn($"BLOCK {blockData.VanillaID}) IS BEAM WEAPON");
                                    }
                                    else
                                    {
                                        logger.Warn($"BLOCK {blockData.BlockID} ({blockData.SessionID}) IS BEAM WEAPON");
                                    }
                                    return false;
                                }
                            }
                        }

                        ModuleWeaponAimHelper helper = target.GetComponent<ModuleWeaponAimHelper>();
                        if (fireData.m_MuzzleVelocity > 0.0f || (helper != null && helper.muzzleVelocityOverride > 0.0f))
                        {
                            logger.Debug($"Has muzzle velocity");
                            if (blockData.VanillaID > 0)
                            {
                                logger.Info($"BLOCK {blockData.VanillaID}) SUCCESS");
                            }
                            else
                            {
                                logger.Info($"BLOCK {blockData.BlockID} ({blockData.SessionID}) SUCCESS");
                            }
                            return true;
                        }
                    }
                }
                logger.Debug($"BLOCK FAILED");
                return false;
            }
        }

        private void PatchWeapon(BlockMetadata blockData)
        {
            Transform editablePrefab = blockData.blockPrefab;
            ModuleWeaponAimHelper helper = editablePrefab.GetComponent<ModuleWeaponAimHelper>();
            TimedFuseData fuseData = editablePrefab.GetComponent<TimedFuseData>();

            logger.Debug($"Start patch");
            bool startedWithFuse = fuseData != null;
            if (!startedWithFuse)
            {
                fuseData = editablePrefab.gameObject.AddComponent<TimedFuseData>();
            }
            fuseData.always_present = startedWithFuse;
            logger.Debug($"fuse data set");
            if (helper == null)
            {
                helper = editablePrefab.gameObject.AddComponent<ModuleWeaponAimHelper>();
            }
            helper.timedFuseData = fuseData;
            logger.Debug($"Helper setup");

            if (!helper.ignoreOffset)
            {
                logger.Debug($"doing offset");
                CannonBarrel[] cannonBarrels = editablePrefab.GetComponentsInChildren<CannonBarrel>(true);
                Vector3 averageOffset = Vector3.zero;
                foreach (CannonBarrel barrel in cannonBarrels)
                {
                    logger.Debug($"processing barrel");
                    Transform projectileSpawn = barrel.projectileSpawnPoint;
                    int index = 0;
                    Transform curr = projectileSpawn;
                    GimbalAimer lastAimer = null;
                    // limit search depth to 10
                    while (curr != editablePrefab && index < 10)
                    {
                        lastAimer = curr.GetComponent<GimbalAimer>();
                        if (lastAimer != null)
                        {
                            break;
                        }
                        curr = projectileSpawn.parent;
                        index++;
                    }

                    Vector3 localPosition;
                    if (lastAimer != null)
                    {
                        localPosition = lastAimer.transform.InverseTransformPoint(projectileSpawn.position);
                    }
                    else
                    {
                        localPosition = editablePrefab.InverseTransformPoint(projectileSpawn.position);
                    }
                    averageOffset += localPosition;
                }
                averageOffset /= cannonBarrels.Length;

                helper.barrelLength = averageOffset.z;
                // disable barrel length
                helper.barrelLength = 0.0f;

                if (helper.gimbalOffset == Vector3.zero)
                {
                    List<GimbalAimer> aimers = new List<GimbalAimer>();
                    Queue<Transform> frontier = new Queue<Transform>();
                    foreach (Transform child in editablePrefab)
                    {
                        frontier.Enqueue(child);
                    }
                    while (frontier.Count > 0)
                    {
                        Transform curr = frontier.Dequeue();
                        GimbalAimer aimer = curr.GetComponent<GimbalAimer>();
                        if (aimer != null)
                        {
                            aimers.Add(aimer);
                        }
                        else
                        {
                            foreach (Transform child in curr)
                            {
                                frontier.Enqueue(child);
                            }
                        }
                    }

                    GimbalAimer.AxisConstraint firstAxis = GimbalAimer.AxisConstraint.Free;
                    if (aimers.Count > 0)
                    {
                        firstAxis = aimers[0].rotationAxis;
                    }
                    bool matchingAxis = true;
                    foreach (GimbalAimer aimer in aimers)
                    {
                        helper.gimbalOffset += editablePrefab.InverseTransformPoint(aimer.transform.position);
                        if (firstAxis != aimer.rotationAxis)
                        {
                            matchingAxis = false;
                        }
                    }
                    if (aimers.Count > 0)
                    {
                        helper.gimbalOffset /= aimers.Count;

                        if (matchingAxis)
                        {
                            helper.firstAxis = firstAxis;

                            List<GimbalAimer> secondAimers = new List<GimbalAimer>();
                            GimbalAimer.AxisConstraint secondAxis = GimbalAimer.AxisConstraint.Free;
                            foreach (GimbalAimer aimer in aimers)
                            {
                                frontier.Enqueue(aimer.transform);
                            }
                            while (frontier.Count > 0)
                            {
                                Transform curr = frontier.Dequeue();
                                GimbalAimer aimer = curr.GetComponent<GimbalAimer>();
                                if (aimer != null && aimer.rotationAxis != firstAxis)
                                {
                                    secondAimers.Add(aimer);
                                }
                                else
                                {
                                    foreach (Transform child in curr)
                                    {
                                        frontier.Enqueue(child);
                                    }
                                }
                            }
                            if (secondAimers.Count > 0)
                            {
                                secondAxis = secondAimers[0].rotationAxis;
                            }
                            foreach (GimbalAimer aimer in secondAimers)
                            {
                                helper.intermediateGimbalOffset += (editablePrefab.InverseTransformPoint(aimer.transform.position) - helper.gimbalOffset);
                                if (secondAxis != aimer.rotationAxis)
                                {
                                    matchingAxis = false;
                                }
                            }
                            if (secondAimers.Count > 0)
                            {
                                helper.intermediateGimbalOffset /= secondAimers.Count;
                            }

                            if (matchingAxis)
                            {
                                // we have successfully gotten both gimbal axes
                                helper.secondAxis = secondAxis;
                            }
                        }
                    }
                }
            }

            FireData fireData = editablePrefab.gameObject.GetComponent<FireData>();
            if (fireData.m_BulletPrefab != null && fireData.m_BulletPrefab is Projectile projectile)
            {
                Rigidbody rbody = projectile.GetComponent<Rigidbody>();
                if (rbody != null) {
                    helper.useGravity = rbody.useGravity;
                }

                SeekingProjectile seekingProjectile = projectile.GetComponent<SeekingProjectile>();
                if (seekingProjectile != null)
                {
                    SmartMissile smartMissile = projectile.GetComponent<SmartMissile>();
                    if (smartMissile == null)
                    {
                        smartMissile = projectile.gameObject.AddComponent<SmartMissile>();
                        smartMissile.disableLead = false;
                        smartMissile.velocityOverride = 0.0f;
                        smartMissile.disableBallistic = false;
                    }
                }
            }
        }

        public override void EarlyInit()
        {
            if (!Inited)
            {
                ConfigureLogger();
                IngressPoint.SetupConfig();
                IngressPoint.SetupUI();
                Inited = true;
                change = new Change
                {
                    id = "Weapon Aim Mod",
                    targetType = ChangeTargetType.TRANSFORM,
                    condition = new WeaponConditional(),
                    patcher = new Action<BlockMetadata>(PatchWeapon)
                };
            }
        }

        public override bool HasEarlyInit()
        {
            return true;
        }

        public override void DeInit()
        {
            TechComponentInjector.TechComponentInjector.RemoveTechComponentToInject(typeof(TargetManager));
            TechComponentInjector.TechComponentInjector.RemoveTechComponentToInject(typeof(OctantVision));
            harmony.UnpatchAll(HarmonyID);
        }

        public override void Init()
        {
            TechComponentInjector.TechComponentInjector.AddTechComponentToInject(typeof(TargetManager));
            TechComponentInjector.TechComponentInjector.AddTechComponentToInject(typeof(OctantVision));
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            BlockChangePatcherMod.RegisterChange(change);
        }
    }
}

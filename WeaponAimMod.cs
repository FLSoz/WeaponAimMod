using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TechComponentInjector;
using System.Reflection;
using HarmonyLib;
using BlockChangePatcher;
using UnityEngine;
using System.Runtime.Remoting.Messaging;
using static ManMap;

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

        internal static Change helperChange;
        internal static Change missileChange;

        private class WeaponConditional : CustomConditional
        {
            public override bool Validate(BlockMetadata blockData)
            {
                Transform target = blockData.blockPrefab;
                // no leading on flamethrowers
                ModuleWeaponFlamethrower flamethrower = target.GetComponent<ModuleWeaponFlamethrower>();
                if (flamethrower.IsNotNull())
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
                if (shotgun.IsNotNull())
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
                if (moduleWeaponGun.IsNotNull())
                {
                    logger.Debug($"ModuleWeaponGun found");
                    FireData fireData = target.GetComponent<FireData>();
                    // no patching shotguns
                    if (fireData.IsNotNull())
                    {
                        logger.Debug($"FireData present");
                        // no leading for beam weapons
                        CannonBarrel[] cannonBarrels = target.GetComponentsInChildren<CannonBarrel>(true);
                        if (cannonBarrels != null)
                        {
                            foreach (CannonBarrel cannonBarrel in cannonBarrels)
                            {
                                if (cannonBarrel.beamWeapon.IsNotNull())
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
                        if (fireData.m_MuzzleVelocity > 0.0f || (helper.IsNotNull() && helper.muzzleVelocityOverride > 0.0f))
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

        private class MissileConditional : CustomConditional
        {
            public override bool Validate(BlockMetadata blockData)
            {
                Transform editablePrefab = blockData.blockPrefab;
                ModuleWeaponGun gun = editablePrefab.GetComponent<ModuleWeaponGun>();
                if (gun.IsNotNull())
                {
                    return gun.m_SeekingRounds;
                }
                return false;
            }
        }

        private void AddHelper(BlockMetadata blockData)
        {
            Transform editablePrefab = blockData.blockPrefab;
            ModuleWeaponAimHelper helper = editablePrefab.GetComponent<ModuleWeaponAimHelper>();
            TimedFuseData fuseData = editablePrefab.GetComponent<TimedFuseData>();

            logger.Debug($"Start patch");
            /* bool startedWithFuse = fuseData.IsNotNull();
            if (!startedWithFuse)
            {
                fuseData = editablePrefab.gameObject.AddComponent<TimedFuseData>();
            }
            fuseData.always_present = startedWithFuse;
            logger.Debug($"fuse data set - started with fuse? {startedWithFuse}"); */
            if (helper.IsNull())
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
                        if (lastAimer.IsNotNull())
                        {
                            break;
                        }
                        curr = projectileSpawn.parent;
                        index++;
                    }

                    Vector3 localPosition;
                    if (lastAimer.IsNotNull())
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
                        if (aimer.IsNotNull())
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
                                if (aimer.IsNotNull() && aimer.rotationAxis != firstAxis)
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
        }

        private static Transform GetProjectilePrefab(BlockMetadata block)
        {
            return block.blockPrefab.GetComponent<FireData>().m_BulletPrefab.transform;
        }
        private static void PatchProjectile(BlockMetadata block, Transform projectilePrefab)
        {
            if (projectilePrefab != null)
            {
                Projectile projectile = projectilePrefab.GetComponent<Projectile>();

                SeekingProjectile seekingProjectile = projectile.GetComponent<SeekingProjectile>();
                if (seekingProjectile.IsNotNull())
                {
                    SmartMissile smartMissile = projectile.GetComponent<SmartMissile>();
                    if (smartMissile.IsNull())
                    {
                        smartMissile = projectile.gameObject.AddComponent<SmartMissile>();
                        smartMissile.Init();
                    }
                }
            }
        }
        private static void ReplaceProjectilePrefab(BlockMetadata block, Transform editableAncillaryPrefab)
        {
            FireData fireData = block.blockPrefab.GetComponent<FireData>();
            fireData.m_BulletPrefab = editableAncillaryPrefab.GetComponent<WeaponRound>();
            if (fireData.m_BulletPrefab == null)
            {
                logger.Fatal($"NULL BULLET PREFAB");
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
                helperChange = new Change
                {
                    id = "Weapon Aim Mod Helper",
                    targetType = ChangeTargetType.TRANSFORM,
                    condition = new WeaponConditional(),
                    patcher = new Action<BlockMetadata>(AddHelper)
                };
                missileChange = new Change {
                    id = "WAM Missile Patch",
                    targetType = ChangeTargetType.TRANSFORM,
                    targetsAncillaryPrefabs = true,
                    condition = new MissileConditional(),
                    ancillaryChanges = new List<AncillaryChange>{ new AncillaryChange {
                        id = "SmartMissile",
                        AncillaryPatcher = new Action<BlockMetadata, Transform>(PatchProjectile),
                        GetAncillaryPrefab = new Func<BlockMetadata, Transform>(GetProjectilePrefab),
                        UpdateAncillaryPrefab = new Action<BlockMetadata, Transform>(ReplaceProjectilePrefab)
                    } }
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
            BlockChangePatcherMod.RegisterChange(helperChange);
            BlockChangePatcherMod.RegisterChange(missileChange);
        }
    }
}

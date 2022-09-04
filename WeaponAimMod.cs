using System;
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

        internal static bool DEBUG = false;

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

        private static readonly FieldInfo m_CannonBarrels = AccessTools.Field(typeof(ModuleWeaponGun), "m_CannonBarrels");
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
                        CannonBarrel[] cannonBarrels = (CannonBarrel[])m_CannonBarrels.GetValue(moduleWeaponGun);
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

                helper.barrelOffset = averageOffset;
            }

            FireData fireData = editablePrefab.gameObject.GetComponent<FireData>();
            if (fireData.m_BulletPrefab != null && fireData.m_BulletPrefab is Projectile projectile)
            {
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

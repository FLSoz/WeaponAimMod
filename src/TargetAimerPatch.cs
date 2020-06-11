using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Harmony;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace WeaponAimMod.src
{
    public static class WrappedDataHolder
    {
        // public static Vector3 unitGravity;
        // public static float gravityMagnitude;
        public static bool cheatDisabled = false;

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

        [HarmonyPatch(typeof(TargetAimer))]
        [HarmonyPatch("UpdateTarget")]
        public static class TargetAimerPatch
        {
            public static bool Prefix(ref TargetAimer __instance)
            {
                // Console.WriteLine("/n FUNCTION BEGINNING");
                TankBlock block = (TankBlock)WrappedDataHolder.m_Block.GetValue(__instance);

                // Console.WriteLine("got block");

                Tank tank = (bool)(UnityEngine.Object)block ? block.tank : (Tank)null;

                // Console.WriteLine("got tank");

                TechWeapon techWeapon = (bool)(UnityEngine.Object)tank ? tank.Weapons : (TechWeapon)null;

                // Console.WriteLine("Prefix Function End");

                Visible manualTarget = !(bool)(UnityEngine.Object)techWeapon ? (Visible)null : techWeapon.GetManualTarget();

                // Console.WriteLine("Ooga");

                if (manualTarget != null)
                {
                    // Console.WriteLine("Ooga 1");
                    WrappedDataHolder.Target.SetValue(__instance, manualTarget);
                    // Console.WriteLine("Booga 1");
                }
                else if (__instance.HasTarget && (!__instance.Target.isActive || (double)Time.time > (double)(float)WrappedDataHolder.m_ChangeTargetTimeout.GetValue(__instance)))
                {
                    // Console.WriteLine("Ooga 2");
                    WrappedDataHolder.Target.SetValue(__instance, (Visible)null);
                    // Console.WriteLine("Booga 2");
                }
                if ((UnityEngine.Object)__instance.Target == (UnityEngine.Object)null && block.tank.control.targetType != ObjectTypes.Null && (!__instance.HasTarget || !block.tank.Vision.CanSee(__instance.Target)))
                {
                    // Console.WriteLine("OogaBooga 3");
                    Visible replacement = block.tank.control.targetType != ObjectTypes.Vehicle ? block.tank.Vision.GetFirstVisible() : block.tank.Vision.GetFirstVisibleTechIsEnemy(block.tank.Team);
                    // Console.WriteLine("BoogaBooga 3");
                    /* if (replacement == null)
                    {
                        // Console.WriteLine("WOLOLOLO");
                    } */
                    WrappedDataHolder.Target.SetValue(__instance, replacement);
                    // Console.WriteLine("OogaOoga 3");
                    WrappedDataHolder.m_ChangeTargetTimeout.SetValue(__instance, Time.time + (float)m_ChangeTargetInteval.GetValue(__instance));
                    // Console.WriteLine("BoogaOoga 3");
                }
                if (!__instance.HasTarget)
                {
                    // Console.WriteLine("NO TARGET");
                    return false;
                }

                // Console.WriteLine("begin alignment");

                FireData componentInParent1 = __instance.GetComponentInParent<FireData>();

                // Console.WriteLine("get FireData");

                Vector3 AimPointVector = __instance.Target.GetAimPoint(block.trans.position);

                // Console.WriteLine("get AimPointVector");

                if (AimPointVector == null)
                {
                    // Console.WriteLine("Need adjustment");
                    AimPointVector = __instance.Target.rbody.transform.position;
                }

                // Console.WriteLine("AimPointVector Adjusted");

                if (((UnityEngine.Object)componentInParent1 != (UnityEngine.Object)null) && (WrappedDataHolder.cheatDisabled || tank.ControllableByAnyPlayer) && !(componentInParent1 is FireDataShotgun) && componentInParent1.m_MuzzleVelocity > 0.0f)
                // if (((UnityEngine.Object)componentInParent1 != (UnityEngine.Object)null) && !(componentInParent1 is FireDataShotgun) && componentInParent1.m_MuzzleVelocity > 0.0f)
                {
                    // Console.WriteLine("woho");
                    // Tank componentInParent2 = __instance.GetComponentInParent<Tank>();

                    // Console.WriteLine("woh Tank");

                    WeaponRound bulletPrefab = componentInParent1.m_BulletPrefab;

                    // Console.WriteLine("try prefab");

                    bool useGravity = false;
                    if (bulletPrefab != null)
                    {
                        // Console.WriteLine("bullet prefab null???");
                        useGravity = bulletPrefab.gameObject.GetComponent<Rigidbody>().useGravity;
                    }

                    // Console.WriteLine("Tentative get end");
                    // Vector3 dist = __instance.Target.transform.position - componentInParent2.transform.position;
                    Vector3 dist = AimPointVector - __instance.transform.position;
                    Rigidbody rbodyTank = __instance.GetComponentInParent<Tank>().rbody;
                    // Vector3 relativeVelocity = __instance.Target.rbody.velocity - (rbodyTank.velocity + __instance.transform.TransformPoint(Vector3.Cross(rbodyTank.angularVelocity, __instance.transform.localPosition)));
                    Vector3 angularToggle = Vector3.zero;
                    if (componentInParent1.m_MuzzleVelocity != 0.0f)
                    {
                        angularToggle = rbodyTank.angularVelocity;
                    }
                    Vector3 relativeVelocity = __instance.Target.rbody.velocity - (rbodyTank.velocity + angularToggle);

                    TimedFuseData timedFuse = __instance.GetComponentInParent<TimedFuseData>();

                    if (useGravity)
                    {
                        // Console.WriteLine("enter Gravity");
                        float height = dist.y;
                        float land = Mathf.Sqrt((dist.x * dist.x) + (dist.z * dist.z));

                        float sqrVel = componentInParent1.m_MuzzleVelocity * componentInParent1.m_MuzzleVelocity;
                        float time = 0.0f;

                        if (sqrVel >= 30.0f * (height + dist.magnitude))
                        {
                            // radians
                            float theta = Mathf.Atan( (sqrVel - Mathf.Sqrt((sqrVel * sqrVel) - ((900.0f * land * land) + (60.0f * height * sqrVel)))) / (30.0f * land));
                            time = land / (Mathf.Cos(theta) * componentInParent1.m_MuzzleVelocity);
                        }

                        if (timedFuse != null)
                        {

                            timedFuse.m_FuseTime = time;
                        }

                        WrappedDataHolder.m_TargetPosition.SetValue(__instance, (relativeVelocity * time) + AimPointVector);
                    }
                    else
                    {
                        float time = dist.magnitude / componentInParent1.m_MuzzleVelocity;

                        if (timedFuse != null)
                        {
                            // Console.WriteLine("fuse timer set to " + time.ToString());
                            timedFuse.m_FuseTime = time;
                        }

                        WrappedDataHolder.m_TargetPosition.SetValue(__instance, (time * relativeVelocity) + AimPointVector);
                    }

                }
                else
                {
                    // Console.WriteLine("NULL FIREDATA ?!?!?!?");
                    WrappedDataHolder.m_TargetPosition.SetValue(__instance, AimPointVector);
                }

                return false;
            }
        }
    }

    [HarmonyPatch(typeof(Projectile))]
    [HarmonyPatch("Fire")]
    public static class ProjectileFirePatch
    {
        private static void Postfix(ref Projectile __instance, FireData fireData)
        {
            TimedFuseData timedFuse = fireData.GetComponentInParent<TimedFuseData>();
            if (timedFuse != null)
            {
                // Console.WriteLine("fuse timer found: " + timedFuse.m_FuseTime.ToString());
                FieldInfo m_LifeTime = typeof(Projectile).GetField("m_LifeTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                m_LifeTime.SetValue(__instance, timedFuse.m_FuseTime + timedFuse.offset);
            }
            return;
        }
    }

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
            CannonBarrel[] cannonBarrels = (CannonBarrel[]) m_CannonBarrels.GetValue(__instance);
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
}

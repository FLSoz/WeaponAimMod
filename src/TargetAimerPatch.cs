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

        // SolveQuadric, SolveCubic, and SolveQuartic were ported from C as written for Graphics Gems I
        
        // Original Author: Jochen Schwarze (schwarze@isa.de)
        // https://github.com/erich666/GraphicsGems/blob/240a34f2ad3fa577ef57be74920db6c4b00605e4/gems/Roots3And4.c
        
        // C# Port written by: Forrest Smith (forrestthewoods@gmail.com)
        // https://github.com/forrestthewoods/lib_fts/blob/master/projects/unity/ballistic_trajectory/Assets/Scripts/fts_ballistic_trajectory.cs

        // Utility function used by SolveQuadratic, SolveCubic, and SolveQuartic
        private static bool IsZero(double d)
        {
            const double eps = 1e-9;
            return d > -eps && d < eps;
        }

        private static double GetCubicRoot(double value)
        {
            if (value > 0.0)
            {
                return System.Math.Pow(value, 1.0 / 3.0);
            }
            else if (value < 0)
            {
                return -System.Math.Pow(-value, 1.0 / 3.0);
            }
            else
            {
                return 0.0;
            }
        }

        // Solve quadratic equation: c0*x^2 + c1*x + c2. 
        // Returns number of solutions.
        public static int SolveQuadric(double c0, double c1, double c2, out double s0, out double s1)
        {
            s0 = double.NaN;
            s1 = double.NaN;

            double p, q, D;

            /* normal form: x^2 + px + q = 0 */
            p = c1 / (2 * c0);
            q = c2 / c0;

            D = p * p - q;

            if (IsZero(D))
            {
                s0 = -p;
                return 1;
            }
            else if (D < 0)
            {
                return 0;
            }
            else /* if (D > 0) */
            {
                double sqrt_D = System.Math.Sqrt(D);

                s0 = sqrt_D - p;
                s1 = -sqrt_D - p;
                return 2;
            }
        }

        // Solve cubic equation: c0*x^3 + c1*x^2 + c2*x + c3. 
        // Returns number of solutions.
        public static int SolveCubic(double c0, double c1, double c2, double c3, out double s0, out double s1, out double s2)
        {
            s0 = double.NaN;
            s1 = double.NaN;
            s2 = double.NaN;

            int num;
            double sub;
            double A, B, C;
            double sq_A, p, q;
            double cb_p, D;

            /* normal form: x^3 + Ax^2 + Bx + C = 0 */
            A = c1 / c0;
            B = c2 / c0;
            C = c3 / c0;

            /*  substitute x = y - A/3 to eliminate quadric term:  x^3 +px + q = 0 */
            sq_A = A * A;
            p = 1.0 / 3 * (-1.0 / 3 * sq_A + B);
            q = 1.0 / 2 * (2.0 / 27 * A * sq_A - 1.0 / 3 * A * B + C);

            /* use Cardano's formula */
            cb_p = p * p * p;
            D = q * q + cb_p;

            if (IsZero(D))
            {
                if (IsZero(q)) /* one triple solution */
                {
                    s0 = 0;
                    num = 1;
                }
                else /* one single and one double solution */
                {
                    double u = GetCubicRoot(-q);
                    s0 = 2 * u;
                    s1 = -u;
                    num = 2;
                }
            }
            else if (D < 0) /* Casus irreducibilis: three real solutions */
            {
                double phi = 1.0 / 3 * System.Math.Acos(-q / System.Math.Sqrt(-cb_p));
                double t = 2 * System.Math.Sqrt(-p);

                s0 = t * System.Math.Cos(phi);
                s1 = -t * System.Math.Cos(phi + System.Math.PI / 3);
                s2 = -t * System.Math.Cos(phi - System.Math.PI / 3);
                num = 3;
            }
            else /* one real solution */
            {
                double sqrt_D = System.Math.Sqrt(D);
                double u = GetCubicRoot(sqrt_D - q);
                double v = -GetCubicRoot(sqrt_D + q);

                s0 = u + v;
                num = 1;
            }

            /* resubstitute */
            sub = 1.0 / 3 * A;

            if (num > 0) s0 -= sub;
            if (num > 1) s1 -= sub;
            if (num > 2) s2 -= sub;

            return num;
        }

        // Solve quartic function: c0*x^4 + c1*x^3 + c2*x^2 + c3*x + c4. 
        // Returns number of solutions.
        public static int SolveQuartic(double c0, double c1, double c2, double c3, double c4, out double s0, out double s1, out double s2, out double s3)
        {
            s0 = double.NaN;
            s1 = double.NaN;
            s2 = double.NaN;
            s3 = double.NaN;

            double[] coeffs = new double[4];
            double z, u, v, sub;
            double A, B, C, D;
            double sq_A, p, q, r;
            int num;

            /* normal form: x^4 + Ax^3 + Bx^2 + Cx + D = 0 */
            A = c1 / c0;
            B = c2 / c0;
            C = c3 / c0;
            D = c4 / c0;

            /*  substitute x = y - A/4 to eliminate cubic term: x^4 + px^2 + qx + r = 0 */
            sq_A = A * A;
            p = -3.0 / 8 * sq_A + B;
            q = 1.0 / 8 * sq_A * A - 1.0 / 2 * A * B + C;
            r = -3.0 / 256 * sq_A * sq_A + 1.0 / 16 * sq_A * B - 1.0 / 4 * A * C + D;

            if (IsZero(r))
            {
                /* no absolute term: y(y^3 + py + q) = 0 */

                coeffs[3] = q;
                coeffs[2] = p;
                coeffs[1] = 0;
                coeffs[0] = 1;

                num = SolveCubic(coeffs[0], coeffs[1], coeffs[2], coeffs[3], out s0, out s1, out s2);
            }
            else
            {
                /* solve the resolvent cubic ... */
                coeffs[3] = 1.0 / 2 * r * p - 1.0 / 8 * q * q;
                coeffs[2] = -r;
                coeffs[1] = -1.0 / 2 * p;
                coeffs[0] = 1;

                SolveCubic(coeffs[0], coeffs[1], coeffs[2], coeffs[3], out s0, out s1, out s2);

                /* ... and take the one real solution ... */
                z = s0;

                /* ... to build two quadric equations */
                u = z * z - r;
                v = 2 * z - p;

                if (IsZero(u))
                    u = 0;
                else if (u > 0)
                    u = System.Math.Sqrt(u);
                else
                    return 0;

                if (IsZero(v))
                    v = 0;
                else if (v > 0)
                    v = System.Math.Sqrt(v);
                else
                    return 0;

                coeffs[2] = z - u;
                coeffs[1] = q < 0 ? -v : v;
                coeffs[0] = 1;

                num = SolveQuadric(coeffs[0], coeffs[1], coeffs[2], out s0, out s1);

                coeffs[2] = z + u;
                coeffs[1] = q < 0 ? v : -v;
                coeffs[0] = 1;

                if (num == 0) num += SolveQuadric(coeffs[0], coeffs[1], coeffs[2], out s0, out s1);
                else if (num == 1) num += SolveQuadric(coeffs[0], coeffs[1], coeffs[2], out s1, out s2);
                else if (num == 2) num += SolveQuadric(coeffs[0], coeffs[1], coeffs[2], out s2, out s3);
            }

            /* resubstitute */
            sub = 1.0 / 4 * A;

            if (num > 0) s0 -= sub;
            if (num > 1) s1 -= sub;
            if (num > 2) s2 -= sub;
            if (num > 3) s3 -= sub;

            return num;
        }

        // Given input parameters, solve the ballistic equation needed to get to the target
        public static float SolveBallisticArc(Vector3 proj_pos, float S, Vector3 target_pos, Vector3 V, Vector3 A)
        {
            // Initialize output parameters
            float bestTime = Mathf.Infinity;
            Vector3 D = target_pos - proj_pos;

            // If we have acceleration, use quartic
            if (A.sqrMagnitude > 1)
            {
                // Derivation 

                //  Variable declaration:
                //      D: target position (relative. Assume projectile source is stationary at origin to simplify calcs)
                //      S: Projectile Speed
                //      t: time till intercept
                //      V: target velocity
                //      A: target acceleration
                //      u: Unit vector of the projectile velocity that has a valid intercept

                //  For intercept, the following must be true:
                //  D + Vt + At^2/2 = uSt

                //  Note, however, that the projectile, which is travelling in a straight line (no gravity. We say is stationary, gravity is part of relative target acceleration),
                //  that ||D + Vt + At^2/2|| = St
                //  We take the property that square of magnitude == dot product with itself, and commutative/distributive dot product properties to get:
                //  S^2t^2 = (D ○ D) + 2(D ○ V)t + (D ○ A)t^2 + (A ○ V)t^3 + (A ○ A)t^4/4 + (V ○ V)t^2
                //  ((A ○ A) / 4) t^4 + (A ○ V) t^3 + ((V ○ V) + (D ○ A) - S^2) t^2 + 2(D ○ V)t + (D ○ D) = 0

                // Solve quartic
                double[] times = new double[4];
                int numTimes = SolveQuartic(A.sqrMagnitude / 4, A.Dot(V), (V.sqrMagnitude + D.Dot(A) - (S * S)), 2 * D.Dot(V), D.sqrMagnitude, out times[0], out times[1], out times[2], out times[3]);

                // Sort so faster collision is found first
                System.Array.Sort(times);

                // Plug quartic solutions into base equations
                // There should never be more than 2 positive, real roots.
                for (int i = 0; i < times.Length; ++i)
                {
                    float t = (float)times[i];
                    if (t < 0 || float.IsNaN(t) || float.IsInfinity(t) || float.IsNegativeInfinity(t))
                        continue;

                    if (t < bestTime)
                    {
                        bestTime = t;
                    }
                }
            }
            // Else, use simplified
            else
            {
                // Derivation 

                //  Variable declaration:
                //      D: target position (relative. Assume projectile source is stationary at origin to simplify calcs)
                //      S: Projectile Speed
                //      t: time till intercept
                //      V: target velocity
                //      u: Unit vector of the projectile velocity that has a valid intercept

                //  For intercept, the following must be true:
                //  D + Vt = uSt

                //  Note, however, that the projectile, which is travelling in a straight line (no gravity. We say is stationary, gravity is part of relative target acceleration),
                //  that ||D + Vt|| = St
                //  We take the property that square of magnitude == dot product with itself, and commutative/distributive dot product properties to get:
                //  S^2t^2 = (D ○ D) + 2(D ○ V)t + (V ○ V)t^2
                //  ((V ○ V) - S^2) t^2 + 2(D ○ V)t + (D ○ D) = 0

                float a = V.sqrMagnitude - (S * S);
                float b = 2 * D.Dot(V);
                float c = D.sqrMagnitude;

                float determinant = (b * b) - (4 * a * c);
                if (determinant > 0)
                {
                    float sqrt = Mathf.Sqrt(determinant);
                    float temp1 = (-b - sqrt) / (2 * a);
                    float temp2 = (sqrt - b) / (2 * a);
                    if (temp1 < 0f)
                    {
                        return temp2;
                    }
                    else if (temp2 < 0f)
                    {
                        return temp1;
                    }
                    else
                    {
                        return Mathf.Min(temp1, temp2);
                    }
                }
            }

            // Write out solutions
            return bestTime;
        }

        public static Vector3 GetAcceleration(Tank tank)
        {
            if (tank != null) {
                TargetManager targetManager = tank.GetComponentInParent<TargetManager>();
                if (targetManager != null)
                {
                    return targetManager.Acceleration;
                }
            }
            return Vector3.zero;
        }

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

            public static bool Prefix(ref SeekingProjectile __instance)
            {
                Projectile projectile = (Projectile)m_MyProjectile.GetValue(__instance);

                // Only do seeking if is a player, or enemy lead enabled
                if (WrappedDataHolder.cheatDisabled || projectile.Shooter == null || projectile.Shooter.ControllableByAnyPlayer)
                {
                    Visible target = (Visible) GetCurrentTarget.Invoke(__instance, null);
                    Transform me = (Transform) m_MyTransform.GetValue(__instance);

                    if (target.IsNotNull()) {
                        Vector3 targetPosition = (Vector3)GetTargetAimPosition.Invoke(__instance, null);
                        float speed = projectile.rbody.velocity.magnitude;

                        float estTime = (targetPosition - me.position).magnitude / speed;
                        float finalThreshold = 0.1f;

                        bool applyRotation = (bool)m_ApplyRotationTowardsTarget.GetValue(__instance);

                        if (estTime >= finalThreshold)
                        {
                            Vector3 V = target.rbody.velocity;
                            Vector3 A = estTime >= 1.5f ? GetAcceleration(target.tank) : Vector3.zero;
                            float time = SolveBallisticArc(me.position, speed, targetPosition, V, A);

                            if (time != Mathf.Infinity)
                            {
                                targetPosition += (V * time) + (A * time * time / 2);
                            }
                        }

                        Vector3 vector = targetPosition - me.position;
                        Vector3 normalized = Vector3.Cross(projectile.rbody.velocity, vector).normalized;
                        float b = Vector3.Angle(projectile.trans.forward, vector);

                        Quaternion quaternion = Quaternion.AngleAxis(Mathf.Min((float) m_TurnSpeed.GetValue(__instance) * Time.deltaTime, b), normalized);
                        projectile.rbody.velocity = quaternion * projectile.rbody.velocity;
                        if (applyRotation)
                        {
                            Quaternion rot = quaternion * projectile.rbody.rotation;
                            projectile.rbody.MoveRotation(rot);
                        }
                    }
                    return false;
                }
                return true;
            }
        }

        // Target leading for weapons
        [HarmonyPatch(typeof(TargetAimer))]
        [HarmonyPatch("UpdateTarget")]
        public static class TargetAimerPatch
        {
            public static void Postfix(ref TargetAimer __instance)
            {
                if (__instance.HasTarget && !Singleton.Manager<ManPauseGame>.inst.IsPaused)
                {
                    TankBlock block = (TankBlock)WrappedDataHolder.m_Block.GetValue(__instance);
                    Tank tank = (bool)(UnityEngine.Object)block ? block.tank : (Tank)null;
                    FireData componentInParent1 = __instance.GetComponentInParent<FireData>();
                    Vector3 AimPointVector = __instance.Target.GetAimPoint(block.trans.position);

                    if (AimPointVector == null)
                    {
                        AimPointVector = __instance.Target.rbody.transform.position;
                    }

                    TimedFuseData timedFuse = __instance.GetComponentInParent<TimedFuseData>();
                    Vector3 relDist = AimPointVector - __instance.transform.position;

                    if (((UnityEngine.Object)componentInParent1 != (UnityEngine.Object)null) && (WrappedDataHolder.cheatDisabled || tank.ControllableByAnyPlayer) && !(componentInParent1 is FireDataShotgun) && componentInParent1.m_MuzzleVelocity > 0.0f)
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

                        Vector3 targetAcceleration = GetAcceleration(__instance.Target.tank);
                        Vector3 relativeAcceleration = targetAcceleration;

                        if (useGravity)
                        {
                            relativeAcceleration -= Physics.gravity;
                        }

                        float exactTime = SolveBallisticArc(__instance.transform.position, componentInParent1.m_MuzzleVelocity, AimPointVector, relativeVelocity, relativeAcceleration);
                        Vector3 adjIntercept = AimPointVector + (relativeVelocity * time);
                        if (exactTime != Mathf.Infinity)
                        {
                            time = exactTime;
                            adjIntercept = AimPointVector + (relativeVelocity * time) + ((relativeAcceleration + (useGravity ? Physics.gravity : Vector3.zero)) / 2 * time * time);

                            /* Vector3 tarIntercept = AimPointVector + (__instance.Target.rbody.velocity * time) + ((relativeAcceleration + (useGravity ? Physics.gravity : Vector3.zero)) / 2 * (time * time));
                            Vector3 intercept = __instance.transform.position + ((rbodyTank.velocity + angularToggle + (componentInParent1.m_MuzzleVelocity * (adjIntercept - __instance.transform.position).normalized)) * time) + (useGravity ? Physics.gravity / 2 * (time * time) : Vector3.zero);

                            long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                            long futureTime = currentTime + (long) Mathf.Floor(time * 1000);

                            if (block.name == "_C_BLOCK:10021")
                            {
                                Console.WriteLine($"[{currentTime}] ==({time})==> [{futureTime}] {block.name} My pos: {__instance.transform.position}, Target pos: {AimPointVector}, " +
                                    $"Target Intercept: {tarIntercept}, Adj Intercept: {adjIntercept}, My Vel:{rbodyTank.velocity + angularToggle}, " +
                                    $"TargetVel {__instance.Target.rbody.velocity}, Muzzle Velocity: {componentInParent1.m_MuzzleVelocity}, Target Acceleration: {targetAcceleration}, RelativeAcceleration: {relativeAcceleration + (useGravity ? Physics.gravity : Vector3.zero)}");
                            } */
                        }

                        if (timedFuse != null)
                        {
                            // Console.WriteLine($"Set {block.name} fuse to {timedFuse.m_FuseTime} with offset of {timedFuse.offset}");
                            timedFuse.m_FuseTime = time;
                            // timedFuse.expectedIntercept = AimPointVector +(__instance.Target.rbody.velocity * time) + ((relativeAcceleration + (useGravity ? Physics.gravity : Vector3.zero)) / 2 * (time * time));
                        }

                        WrappedDataHolder.m_TargetPosition.SetValue(__instance, adjIntercept);
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
    }

    [HarmonyPatch(typeof(Projectile))]
    [HarmonyPatch("OnLifetimeEnd")]
    public static class PatchExplosionSpawn
    {
        private static FieldInfo m_ExplodeAfterLifetime = typeof(Projectile).GetField("m_ExplodeAfterLifetime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo m_DestroyTimeout = typeof(Projectile).GetField("m_DestroyTimeout", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static MethodInfo SpawnExplosion = typeof(Projectile).GetMethod("SpawnExplosion", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool Prefix(ref Projectile __instance)
        {
            d.Assert(__instance.gameObject.activeInHierarchy);
            if ((bool) m_ExplodeAfterLifetime.GetValue(__instance))
            {
                Vector3 adjPos = __instance.trans.position + (__instance.rbody.velocity * (float)m_DestroyTimeout.GetValue(__instance));
                SpawnExplosion.Invoke(__instance, new object[] { adjPos, null });
            }
            __instance.Recycle(true);
            return false;
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

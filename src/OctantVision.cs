using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using HarmonyLib;


namespace WeaponAimMod.src
{
    public class OctantVision : TechComponent
    {
        private List<Visible.ConeFiltered>[] m_Visibles = new List<Visible.ConeFiltered>[8];
        private Visible.WeakReference[] m_ClosestOctantEnemy = new Visible.WeakReference[8];
        private Visible.WeakReference m_ClosestEnemy = new Visible.WeakReference();
        public float m_RefreshInterval = 0.5f;
        public float DbgRONumVisible;
        private List<ModuleVision> m_VisionModules = new List<ModuleVision>(10);
        private Vector3 m_SearchEpicentre;
        private float m_SearchRadius;
        private bool m_SearchSphereNeedsRecalc;
        private float m_UpdateClosestEnemyTimeout;
        private const bool debug = false;

        private static readonly FieldInfo restingOrientation = typeof(GimbalAimer).GetField("restingOrientationLocal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        public struct Limits
        {
            public Vector3 localToBlock;
            public Vector3 localToGimbal;
            public Vector3 localToBarrel;

            public float[] XConstraints;
            public float[] YConstraints;
        };

        public static void DebugPrint(TankBlock block, string text)
        {
            if (debug && block.name.Contains("_C_BLOCK"))
            {
                Console.WriteLine(text);
            }
        }

        public static int[] GetOctants(Limits limits, GimbalAimer parentGimbal, TankBlock block)
        {
            Vector3 up = block.transform.up;
            Vector3 forward = block.transform.forward;

            if (parentGimbal)
            {
                Quaternion localRotation = (Quaternion)restingOrientation.GetValue(parentGimbal);
                Quaternion currentRotation = parentGimbal.transform.localRotation;
                Quaternion reverseCurrent = Quaternion.Inverse(currentRotation);
                up = (reverseCurrent * parentGimbal.transform.up);
                forward = (reverseCurrent * parentGimbal.transform.forward);
            }

            Transform tankTransform = block.tank.transform;

            float rightTest = up.Dot(tankTransform.right);
            float frontTest = up.Dot(tankTransform.forward);
            float topTest = up.Dot(tankTransform.up);

            float xTest, yTest;

            int[] leftPair;
            int[] rightPair;

            int[] topQuad;
            int[] bottomQuad;

            if (rightTest > 0.5) // On the right
            {
                xTest = forward.Dot(tankTransform.forward);
                yTest = forward.Dot(tankTransform.up);

                topQuad = new int[4] { 4, 5, 6, 7 };
                bottomQuad = new int[4] { 0, 1, 2, 3 };

                if (xTest > 0.5) // right
                {
                    DebugPrint(block, $"{block.name} ON RIGHT FACING RIGHT");
                    leftPair = new int[2] { 2, 6 };
                    rightPair = new int[2] { 0, 4 };
                }
                else if (yTest > 0.5) // up
                {
                    DebugPrint(block, $"{block.name} ON RIGHT FACING FRONT");
                    leftPair = new int[2] { 0, 4 };
                    rightPair = new int[2] { 1, 5 };
                }
                else if (xTest < -0.5) // left
                {
                    DebugPrint(block, $"{block.name} ON RIGHT FACING LEFT");
                    leftPair = new int[2] { 1, 5 };
                    rightPair = new int[2] { 3, 7 };
                }
                else // down
                {
                    DebugPrint(block, $"{block.name} ON RIGHT FACING BACK");
                    leftPair = new int[2] { 3, 7 };
                    rightPair = new int[2] { 2, 6 };
                }
            }
            else if (rightTest < -0.5) // On the left
            {
                xTest = -forward.Dot(tankTransform.forward);
                yTest = forward.Dot(tankTransform.up);

                bottomQuad = new int[4] { 4, 5, 6, 7 };
                topQuad = new int[4] { 0, 1, 2, 3 };

                if (xTest > 0.5) // right
                {
                    DebugPrint(block, $"{block.name} ON LEFT FACING RIGHT");
                    rightPair = new int[2] { 2, 6 };
                    leftPair = new int[2] { 0, 4 };
                }
                else if (yTest > 0.5) // up
                {
                    DebugPrint(block, $"{block.name} ON LEFT FACING FRONT");
                    rightPair = new int[2] { 0, 4 };
                    leftPair = new int[2] { 1, 5 };
                }
                else if (xTest < -0.5) // left
                {
                    DebugPrint(block, $"{block.name} ON LEFT FACING LEFT");
                    rightPair = new int[2] { 1, 5 };
                    leftPair = new int[2] { 3, 7 };
                }
                else // down
                {
                    DebugPrint(block, $"{block.name} ON LEFT FACING BACK");
                    rightPair = new int[2] { 3, 7 };
                    leftPair = new int[2] { 2, 6 };
                }
            }
            else if (frontTest > 0.5) // On the front
            {
                xTest = forward.Dot(tankTransform.right);
                yTest = forward.Dot(tankTransform.up);

                topQuad = new int[4] { 1, 3, 5, 7 };
                bottomQuad = new int[4] { 0, 2, 4, 6 };

                if (xTest > 0.5) // right
                {
                    DebugPrint(block, $"{block.name} ON FRONT FACING RIGHT");
                    leftPair = new int[2] { 6, 7 };
                    rightPair = new int[2] { 4, 5 };
                }
                else if (yTest > 0.5) // up
                {
                    DebugPrint(block, $"{block.name} ON FRONT FACING FRONT");
                    leftPair = new int[2] { 4, 5 };
                    rightPair = new int[2] { 0, 1 };
                }
                else if (xTest < -0.5) // left
                {
                    DebugPrint(block, $"{block.name} ON FRONT FACING LEFT");
                    leftPair = new int[2] { 0, 1 };
                    rightPair = new int[2] { 2, 3 };
                }
                else // down
                {
                    DebugPrint(block, $"{block.name} ON FRONT FACING BACK");
                    leftPair = new int[2] { 2, 3 };
                    rightPair = new int[2] { 6, 7 };
                }
            }
            else if (frontTest < -0.5) // On the back
            {
                xTest = -forward.Dot(tankTransform.right);
                yTest = forward.Dot(tankTransform.up);

                bottomQuad = new int[4] { 1, 3, 5, 7 };
                topQuad = new int[4] { 0, 2, 4, 6 };

                if (xTest > 0.5) // right
                {
                    DebugPrint(block, $"{block.name} ON BACK FACING RIGHT");
                    rightPair = new int[2] { 6, 7 };
                    leftPair = new int[2] { 4, 5 };
                }
                else if (yTest > 0.5) // up
                {
                    DebugPrint(block, $"{block.name} ON BACK FACING FRONT");
                    rightPair = new int[2] { 4, 5 };
                    leftPair = new int[2] { 0, 1 };
                }
                else if (xTest < -0.5) // left
                {
                    DebugPrint(block, $"{block.name} ON BACK FACING LEFT");
                    rightPair = new int[2] { 0, 1 };
                    leftPair = new int[2] { 2, 3 };
                }
                else // down
                {
                    DebugPrint(block, $"{block.name} ON BACK FACING BACK");
                    rightPair = new int[2] { 2, 3 };
                    leftPair = new int[2] { 6, 7 };
                }
            }
            else if (topTest > 0.5) // On the top
            {
                xTest = forward.Dot(tankTransform.right);
                yTest = forward.Dot(tankTransform.forward);
                bottomQuad = new int[4] { 0, 1, 4, 5 };
                topQuad = new int[4] { 2, 3, 6, 7 };

                if (xTest > 0.5) // right
                {
                    DebugPrint(block, $"{block.name} ON TOP FACING RIGHT");
                    leftPair = new int[2] { 3, 1 };
                    rightPair = new int[2] { 2, 0 };
                }
                else if (yTest > 0.5) // up
                {
                    DebugPrint(block, $"{block.name} ON TOP FACING FRONT");
                    leftPair = new int[2] { 2, 0 };
                    rightPair = new int[2] { 6, 4 };
                }
                else if (xTest < -0.5) // left
                {
                    DebugPrint(block, $"{block.name} ON TOP FACING LEFT");
                    leftPair = new int[2] { 6, 4 };
                    rightPair = new int[2] { 7, 5 };
                }
                else // down
                {
                    DebugPrint(block, $"{block.name} ON TOP FACING BACK");
                    leftPair = new int[2] { 7, 5 };
                    rightPair = new int[2] { 3, 1 };
                }
            }
            else // On the bottom
            {
                xTest = -forward.Dot(tankTransform.right);
                yTest = forward.Dot(tankTransform.forward);
                topQuad = new int[4] { 0, 1, 4, 5 };
                bottomQuad = new int[4] { 2, 3, 6, 7 };

                if (xTest > 0.5) // right
                {
                    DebugPrint(block, $"{block.name} ON BOTTOM FACING RIGHT");
                    rightPair = new int[2] { 3, 1 };
                    leftPair = new int[2] { 2, 0 };
                }
                else if (yTest > 0.5) // up
                {
                    DebugPrint(block, $"{block.name} ON BOTTOM FACING FRONT");
                    rightPair = new int[2] { 2, 0 };
                    leftPair = new int[2] { 6, 4 };
                }
                else if (xTest < -0.5) // left
                {
                    DebugPrint(block, $"{block.name} ON BOTTOM FACING LEFT");
                    rightPair = new int[2] { 6, 4 };
                    leftPair = new int[2] { 7, 5 };
                }
                else // down
                {
                    DebugPrint(block, $"{block.name} ON BOTTOM FACING BACK");
                    rightPair = new int[2] { 7, 5 };
                    leftPair = new int[2] { 3, 1 };
                }
            }

            List<int> octants = new List<int>() { 0, 1, 2, 3, 4, 5, 6, 7 };
            if (limits.YConstraints[0] != limits.YConstraints[1])
            {
                if (limits.YConstraints[0] >= -90)
                {
                    foreach (int octant in leftPair)
                    {
                        octants.Remove(octant);
                    }
                }
                if (limits.YConstraints[1] <= 90)
                {
                    foreach (int octant in rightPair)
                    {
                        octants.Remove(octant);
                    }
                }
            }
            if (limits.XConstraints[0] != limits.XConstraints[1])
            {
                if (limits.XConstraints[0] >= 0)
                {
                    foreach (int octant in bottomQuad)
                    {
                        octants.Remove(octant);
                    }
                }
                if (limits.XConstraints[1] <= 0)
                {
                    foreach (int octant in topQuad)
                    {
                        octants.Remove(octant);
                    }
                }
            }

            return octants.ToArray();
        }

        private bool AnyModuleCanSee(Visible item, out float distSq)
        {
            if (item.tank != null && item.tank.netTech != null && item.tank.netTech.InitialSpawnShieldID != 0U)
            {
                distSq = 0f;
                return false;
            }
            for (int i = 0; i < this.m_VisionModules.Count; i++)
            {
                if (this.m_VisionModules[i].CanSee(item, out distSq))
                {
                    return true;
                }
            }
            distSq = 0f;
            return false;
        }

        public bool CanSee(Visible item)
        {
            float num;
            return this.AnyModuleCanSee(item, out num);
        }

        public Visible GetFirstVisibleTechIsEnemy(int team)
        {
            bool flag = true;
            if (Singleton.Manager<ManNetwork>.inst.IsMultiplayer())
            {
                if (base.Tech.netTech == null)
                {
                    flag = false;
                }
                else if (base.Tech.netTech.NetPlayer == null)
                {
                    flag = ManNetwork.IsHost;
                }
                else
                {
                    flag = (base.Tech.netTech.NetPlayer == Singleton.Manager<ManNetwork>.inst.MyPlayer);
                }
            }
            if (flag && Time.time > this.m_UpdateClosestEnemyTimeout)
            {
                this.m_UpdateClosestEnemyTimeout = Time.time + this.m_RefreshInterval;
                if (this.m_SearchSphereNeedsRecalc)
                {
                    this.m_SearchSphereNeedsRecalc = false;
                    this.RecalculateSearchSphere();
                }
                Vector3 searchPosition = base.Tech.trans.TransformPoint(this.m_SearchEpicentre);
                Visible visible = null;
                float bestDist = 0f;
                this.ClearVisibles();
                foreach (Visible visible2 in Singleton.Manager<ManWorld>.inst.TileManager.IterateVisibles(ObjectTypes.Vehicle, searchPosition, this.m_SearchRadius))
                {
                    Tank tank = visible2.tank;
                    if (Singleton.Manager<ManNetwork>.inst.IsMultiplayer() && Singleton.Manager<ManNetwork>.inst.NetController != null)
                    {
                        NetTech notableTech = Singleton.Manager<ManNetwork>.inst.NetController.GetNotableTech();
                        if (notableTech != null && notableTech.tech == tank && tank.IsEnemy(team) && notableTech.InitialSpawnShieldID == 0U)
                        {
                            visible = notableTech.tech.visible;
                            break;
                        }
                    }
                    if (tank != null && tank.IsEnemy(team) && tank.ShouldShowOverlay)
                    {
                        float sqrMagnitude = (searchPosition - tank.trans.position).sqrMagnitude;
                        float num2;
                        if (sqrMagnitude < this.m_SearchRadius * this.m_SearchRadius && this.AnyModuleCanSee(tank.visible, out num2))
                        {
                            Vector3 localPosition = base.Tech.trans.InverseTransformPoint(tank.trans.position);
                            int octant = localPosition.x > 0 ? 4 : 0 + localPosition.y > 0 ? 2 : 0 + localPosition.z > 0 ? 1 : 0;

                            this.m_Visibles[octant].Add(new Visible.ConeFiltered
                            {
                                visible = tank.visible,
                                distSq = sqrMagnitude
                            });
                            if (visible == null || sqrMagnitude < bestDist)
                            {
                                visible = tank.visible;
                                bestDist = sqrMagnitude;
                            }
                        }
                    }
                }
                this.m_ClosestEnemy.Set(visible);
                for (int i = 0; i < 8; i++)
                {
                    this.m_Visibles[i].Sort((Visible.ConeFiltered a, Visible.ConeFiltered b) => (int)(a.distSq * 1000f - b.distSq * 1000f));
                    Visible octantBest = null;
                    if (this.m_Visibles[i].Count > 0)
                    {
                        octantBest = this.m_Visibles[i][0].visible;
                    }
                    this.m_ClosestOctantEnemy[i].Set(octantBest);
                }
            }
            return this.m_ClosestEnemy.Get();
        }

        public Visible GetFirstVisibleTechIsEnemy(int[] octants, Limits limits, TankBlock block, GimbalAimer XGimbal, GimbalAimer YGimbal, Func<Vector3, Vector3> aimDelegate)
        {
            Visible overallBest = null;

            bool hasX = limits.XConstraints[0] != limits.XConstraints[1];
            bool hasY = limits.YConstraints[0] != limits.YConstraints[1];

            List<int> sortedOctantId = new List<int>();
            int[] indices = new int[octants.Length];
            for (int i = 0; i < octants.Length; i++)
            {
                indices[i] = 0;
                if (this.m_Visibles[octants[i]].Count > 0)
                {
                    sortedOctantId.Add(i);
                }
            }
            sortedOctantId.Sort((int a, int b) => (int) (this.m_Visibles[octants[a]][indices[a]].distSq * 1000f - this.m_Visibles[octants[b]][indices[b]].distSq * 1000f));

            // Continuously keep a sorted list of visibles to go through
            while (sortedOctantId.Count > 0)
            {
                int currentOctantId = sortedOctantId[0];
                sortedOctantId.RemoveAt(0);

                int octant = octants[currentOctantId];
                int index = indices[currentOctantId];

                // Check if targetable. If it is, then return it
                Visible.ConeFiltered filteredVisible = this.m_Visibles[octant][index];
                if (filteredVisible.visible != null)
                {
                    Visible visible = filteredVisible.visible;
                    Tank tank = filteredVisible.visible.tank;

                    bool canAim = true;
                    Vector3 targetWorld = tank.transform.position;
                    if (aimDelegate != null)
                    {
                        targetWorld = aimDelegate(targetWorld);
                    }

                    float leeway = Mathf.Max(5.0f, (float) Math.Atan2(tank.blockBounds.size.magnitude / 2, (targetWorld - block.transform.position).magnitude) * Mathf.Rad2Deg);
                    float YAngle = 0f, XAngle = 0f;
                    if (hasY && YGimbal)
                    {
                        Vector3 targetRelative = YGimbal.transform.parent.InverseTransformDirection(targetWorld - YGimbal.transform.position);
                        YAngle = Mathf.Atan2(targetRelative.x, targetRelative.z) * Mathf.Rad2Deg;
                        canAim = YAngle >= limits.YConstraints[0] - leeway && YAngle <= limits.YConstraints[1] + leeway;
                    }
                    if (canAim && hasX && XGimbal)
                    {
                        Vector3 targetRelative = XGimbal.transform.parent.InverseTransformDirection(targetWorld - XGimbal.transform.position);
                        XAngle = Mathf.Atan2(-targetRelative.y, targetRelative.z) * Mathf.Rad2Deg;
                        canAim = XAngle >= limits.XConstraints[0] - leeway && XAngle <= limits.XConstraints[1] + leeway;
                    }

                    if (canAim)
                    {
                        RaycastHit raycastHit;
                        float length = Mathf.Max(block.tank.blockBounds.size.magnitude, 1f);
                        Vector3 barrelRelative = limits.localToBarrel + limits.localToGimbal + limits.localToBlock;

                        Vector3 ray = (targetWorld - block.transform.position).normalized;

                        if (!Physics.Raycast(block.transform.position + (ray * barrelRelative.magnitude), ray, out raycastHit, length, Globals.inst.layerTank.mask, QueryTriggerInteraction.Ignore) || raycastHit.rigidbody != block.tank.rbody)
                        {
                            float sqrMagnitude = (base.Tech.trans.position - tank.trans.position).sqrMagnitude;
                            return visible;
                        }
                    }
                }
                
                indices[currentOctantId]++;
                // If stuff left in visibles, add to sorted list
                if (this.m_Visibles[octant].Count > index + 1)
                {
                    int sortIndex = sortedOctantId.BinarySearch(currentOctantId, (int a, int b) => (int)(this.m_Visibles[octants[a]][indices[a]].distSq * 1000f - this.m_Visibles[octants[b]][indices[b]].distSq * 1000f));
                    if (sortIndex < 0)
                    {
                        sortIndex = ~sortIndex;
                    }
                    sortedOctantId.Insert(sortIndex, currentOctantId);
                }
            }
            return overallBest;
        }

        private void ClearVisibles()
        {
            for (int i = 0; i < 8; i++)
            {
                this.m_Visibles[i].Clear();
            }
        }


        private static readonly FieldInfo TechVision_SearchEpicentre = typeof(TechVision).GetField("m_SearchEpicentre", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo TechVision_SearchRadius = typeof(TechVision).GetField("m_SearchRadius", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo TechVision_RecalculateSearchSphere = typeof(TechVision).GetMethod("RecalculateSearchSphere", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private void RecalculateSearchSphere()
        {
            Tank baseTech = base.Tech;
            if (baseTech)
            {
                TechVision vision = baseTech.Vision;
                if (vision)
                {
                    TechVision_RecalculateSearchSphere.Invoke(vision, null);
                    this.m_SearchEpicentre = (Vector3)TechVision_SearchEpicentre.GetValue(vision);
                    this.m_SearchRadius = (float) TechVision_SearchRadius.GetValue(vision);
                }
                else
                {
                    this.m_SearchEpicentre = Vector3.zero;
                    foreach (ModuleVision moduleVision in this.m_VisionModules)
                    {
                        this.m_SearchEpicentre += moduleVision.block.centreOfMassWorld;
                    }
                    if (this.m_VisionModules.Count > 1)
                    {
                        this.m_SearchEpicentre /= (float)this.m_VisionModules.Count;
                    }
                    this.m_SearchRadius = 0f;
                    foreach (ModuleVision moduleVision2 in this.m_VisionModules)
                    {
                        float b = (moduleVision2.block.centreOfMassWorld - this.m_SearchEpicentre).magnitude + moduleVision2.Range;
                        this.m_SearchRadius = Mathf.Max(this.m_SearchRadius, b);
                    }
                    this.m_SearchEpicentre = base.Tech.trans.InverseTransformPoint(this.m_SearchEpicentre);
                }
            }
        }

        public void AddVision(ModuleVision vision)
        {
            this.m_VisionModules.Add(vision);
            this.m_SearchSphereNeedsRecalc = true;
        }
        public void RemoveVision(ModuleVision vision)
        {
            this.m_VisionModules.Remove(vision);
            this.m_SearchSphereNeedsRecalc = true;
        }

        public void OnPool()
        {
            WeaponAimMod.logger.Debug("OctantVision OnPool");
            for (int i = 0; i < 8; i++)
            {
                if (this.m_ClosestOctantEnemy[i] == null)
                {
                    this.m_ClosestOctantEnemy[i] = new Visible.WeakReference();
                }
                if (this.m_Visibles[i] == null)
                {
                    this.m_Visibles[i] = new List<Visible.ConeFiltered>();
                }
                else
                {
                    this.m_Visibles[i].Clear();
                }
            }
        }

        private void OnSpawn()
        {
            WeaponAimMod.logger.Debug("OctantVision OnSpawn");
            this.ClearVisibles();
            this.m_VisionModules.Clear();
            this.m_SearchRadius = 0f;
            this.m_SearchSphereNeedsRecalc = false;
            this.m_UpdateClosestEnemyTimeout = Time.time + UnityEngine.Random.value * this.m_RefreshInterval;
        }

        private void OnRecycle()
        {
            WeaponAimMod.logger.Debug("OctantVision OnRecycle");
            this.ClearVisibles();
        }
    }

    public static class PatchLogSearchSphere
    {
        public static void Postfix(ref TechVision __instance)
        {
            Console.WriteLine($"RECALCULATE SEARCH SPHERE {__instance.Tech.name}");
        }
    }

    [HarmonyPatch(typeof(TechVision))]
    [HarmonyPatch("RefreshState")]
    public static class PatchLogRefreshState
    {
        public static void Postfix(ref TechVision __instance)
        {
            Console.WriteLine($"REFRESH STATE {__instance.Tech.name}");
        }
    }

    [HarmonyPatch(typeof(TechVision))]
    [HarmonyPatch("GetFirstVisibleTechIsEnemy")]
    public static class PatchEnemySearch
    {
        public static bool Prefix(ref TechVision __instance, ref int team, ref Visible __result)
        {
            OctantVision octantVision = __instance.GetComponent<OctantVision>();
            if (octantVision)
            {
                __result = octantVision.GetFirstVisibleTechIsEnemy(team);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(TechVision))]
    [HarmonyPatch("AddVision")]
    public static class PatchAddVision
    {
        public static void Postfix(ref TechVision __instance, ref ModuleVision vision)
        {
            OctantVision octantVision = __instance.GetComponent<OctantVision>();
            if (octantVision)
            {
                octantVision.AddVision(vision);
            }
            return;
        }
    }

    [HarmonyPatch(typeof(TechVision))]
    [HarmonyPatch("RemoveVision")]
    public static class PatchRemoveVision
    {
        public static void Postfix(ref TechVision __instance, ref ModuleVision vision)
        {
            OctantVision octantVision = __instance.GetComponent<OctantVision>();
            if (octantVision)
            {
                octantVision.RemoveVision(vision);
            }
            return;
        }
    }

    // We overwrite the aim method for octant targeting, because it has a call to return to normal within the function that checks if it's aimable
    [HarmonyPatch(typeof(TargetAimer))]
    [HarmonyPatch("AimAtTarget")]
    public static class PatchAiming
    {
        private static readonly FieldInfo m_TargetPosition = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo AimDelegate = typeof(TargetAimer).GetField("AimDelegate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo m_GimbalAimers = typeof(TargetAimer).GetField("m_GimbalAimers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo m_Block = typeof(TargetAimer).GetField("m_Block", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo UpdateTarget = typeof(TargetAimer).GetMethod("UpdateTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Target = typeof(TargetAimer).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(field =>
                        field.CustomAttributes.Any(attr => attr.AttributeType == typeof(CompilerGeneratedAttribute)) &&
                        (field.DeclaringType == typeof(TargetAimer).GetProperty("Target").DeclaringType) &&
                        field.FieldType.IsAssignableFrom(typeof(TargetAimer).GetProperty("Target").PropertyType) &&
                        field.Name.StartsWith("<" + typeof(TargetAimer).GetProperty("Target").Name + ">")
                    );
        private static readonly FieldInfo m_CannonBarrels = typeof(ModuleWeaponGun).GetField("m_CannonBarrels", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo m_ChangeTargetTimeout = AccessTools.Field(typeof(TargetAimer), "m_ChangeTargetTimeout");

        private static void DrawTarget(TargetAimer __instance, TankBlock block)
        {
            if (!Singleton.Manager<ManPauseGame>.inst.IsPaused)
            {
                if (block && block.tank && block.tank.IsPlayer)
                {
                    Visible currentTarget = __instance.Target;
                    if (currentTarget != null)
                    {
                        WeaponAimMod.logger.Debug($"TO DRAW TARGET BOX AROUND: {currentTarget.name}");
                        TargetManager.DrawTarget(currentTarget.tank);
                    }
                }
                else
                {
                    Visible currentTarget = __instance.Target;
                    if (currentTarget != null)
                    {
                        // WeaponAimMod.logger.Trace("NOT A PLAYER: IGNORING");
                    }
                }
            }
        }

        public static bool Prefix(TargetAimer __instance, ref float rotateSpeed)
        {
            TankBlock block = (TankBlock)m_Block.GetValue(__instance);

            // Has a target
            if (WeaponAimSettings.OctantAim && block && block.tank && __instance.HasTarget)
            {
                Tank tank = block.tank;
                bool enemyWeapon = !ManSpawn.IsPlayerTeam(tank.Team);

                if (enemyWeapon && WeaponAimSettings.EnemyLead || !enemyWeapon && WeaponAimSettings.PlayerLead)
                {
                    Vector3 targetWorld = (Vector3)m_TargetPosition.GetValue(__instance);
                    Func<Vector3, Vector3> aimDelegate = (Func<Vector3, Vector3>)AimDelegate.GetValue(__instance);
                    if (__instance.HasTarget && aimDelegate != null)
                    {
                        targetWorld = aimDelegate(targetWorld);
                    }

                    // Check if we can aim
                    List<GimbalAimer> gimbalAimers = (List<GimbalAimer>)m_GimbalAimers.GetValue(__instance);
                    bool canAim = true;
                    foreach (GimbalAimer aimer in gimbalAimers)
                    {
                        if (__instance.Target && canAim)
                        {
                            canAim = aimer.CanAim(targetWorld);
                            if (!canAim)
                            {
                                break;
                            }
                        }
                    }

                    // If we can aim, is all good - continue as normal (will do aim calculation for gimbals twice)
                    if (canAim)
                    {
                        DrawTarget(__instance, block);
                        return true;
                    }

                    IModuleWeapon moduleWeapon = block.GetComponent<IModuleWeapon>();

                    OctantVision octantVision = tank.GetComponent<OctantVision>();
                    if (octantVision && gimbalAimers.Count > 0)
                    {
                        float[] XConstraints = new float[2];
                        float[] YConstraints = new float[2];

                        GimbalAimer XGimbal = null;
                        GimbalAimer YGimbal = null;
                        GimbalAimer FreeGimbal = null;

                        bool free = false;
                        foreach (GimbalAimer aimer in gimbalAimers)
                        {
                            if (aimer.rotationAxis == GimbalAimer.AxisConstraint.X)
                            {
                                XConstraints = aimer.rotationLimits;
                                XGimbal = aimer;
                            }
                            else if (aimer.rotationAxis == GimbalAimer.AxisConstraint.Y)
                            {
                                YConstraints = aimer.rotationLimits;
                                YGimbal = aimer;
                            }
                            else
                            {
                                FreeGimbal = aimer;
                                free = true;
                                break;
                            }
                        }
                        int[] octants;
                        if (free)
                        {
                            octants = new int[8] { 0, 1, 2, 3, 4, 5, 6, 7 };
                            OctantVision.Limits limits = new OctantVision.Limits
                            {
                                localToBlock = block.trans.InverseTransformPoint(FreeGimbal.transform.position),
                                localToGimbal = Vector3.zero,
                                localToBarrel = Vector3.zero,

                                XConstraints = new float[2],
                                YConstraints = new float[2]
                            };
                            Visible betterTarget = octantVision.GetFirstVisibleTechIsEnemy(octants, limits, block, null, null, aimDelegate);
                            if (betterTarget)
                            {
                                Target.SetValue(__instance, betterTarget);
                                m_ChangeTargetTimeout.SetValue(__instance, Time.time + 0.5f); // Set target for at least next half second
                                UpdateTarget.Invoke(__instance, null);
                            }
                        }
                        else if (XGimbal || YGimbal)
                        {
                            if (moduleWeapon != null)
                            {
                                GimbalAimer parentGimbal = null;
                                GimbalAimer childGimbal = null;
                                if (XGimbal)
                                {
                                    if (YGimbal)
                                    {
                                        if (YGimbal.transform.IsChildOf(XGimbal.transform))
                                        {
                                            parentGimbal = XGimbal;
                                            childGimbal = YGimbal;
                                        }
                                        else
                                        {
                                            parentGimbal = YGimbal;
                                            childGimbal = XGimbal;
                                        }
                                    }
                                    else
                                    {
                                        parentGimbal = XGimbal;
                                    }
                                }
                                else
                                {
                                    parentGimbal = YGimbal;
                                }

                                Vector3 gimbalBlockLocalPosition = block.trans.InverseTransformPoint(parentGimbal.transform.position);
                                Vector3 gimbalLocalPosition = Vector3.zero;
                                if (parentGimbal && childGimbal)
                                {
                                    gimbalLocalPosition = parentGimbal.transform.InverseTransformPoint(childGimbal.transform.position);
                                }

                                Vector3 fireLocalPosition = Vector3.zero;
                                GimbalAimer localChildGimbal = childGimbal ? childGimbal : parentGimbal;
                                if (moduleWeapon is ModuleWeaponGun moduleWeaponGun)
                                {
                                    CannonBarrel[] cannonBarrels = (CannonBarrel[])m_CannonBarrels.GetValue(moduleWeaponGun);
                                    foreach (CannonBarrel cannonBarrel in cannonBarrels)
                                    {
                                        fireLocalPosition += localChildGimbal.transform.InverseTransformPoint(cannonBarrel.projectileSpawnPoint.position);
                                    }
                                    fireLocalPosition /= cannonBarrels.Length;
                                }
                                else if (moduleWeapon is ModuleWeaponFlamethrower moduleWeaponFlamethrower)
                                {
                                    fireLocalPosition = localChildGimbal.transform.InverseTransformPoint(moduleWeaponFlamethrower.GetFireTransform().position);
                                }

                                OctantVision.Limits limits = new OctantVision.Limits
                                {
                                    localToBlock = gimbalBlockLocalPosition,
                                    localToGimbal = gimbalLocalPosition,
                                    localToBarrel = fireLocalPosition,

                                    XConstraints = XConstraints,
                                    YConstraints = YConstraints
                                };
                                octants = OctantVision.GetOctants(limits, parentGimbal, block);

                                Visible betterTarget = octantVision.GetFirstVisibleTechIsEnemy(octants, limits, block, XGimbal, YGimbal, aimDelegate);
                                if (betterTarget != null)
                                {
                                    Target.SetValue(__instance, betterTarget);
                                    m_ChangeTargetTimeout.SetValue(__instance, Time.time + 0.5f); // Set target for at least next half second
                                    UpdateTarget.Invoke(__instance, null);
                                }
                            }
                        }
                    }
                }
            }
            DrawTarget(__instance, block);
            return true;
        }
    }

    // Mandate a maximum aim clamp of 10% when using octant vision
    [HarmonyPatch(typeof(GimbalAimer))]
    [HarmonyPatch("CalcAim")]
    public static class PatchAimClamp
    {
        public static bool Prefix(ref GimbalAimer __instance, out float __state)
        {
            __state = __instance.aimClampMaxPercent;
            if (WeaponAimSettings.OctantAim)
            {
                __instance.aimClampMaxPercent = Mathf.Min(0.10f, __instance.aimClampMaxPercent);
            }
            return true;
        }

        public static void Postfix(ref GimbalAimer __instance, float __state)
        {
            __instance.aimClampMaxPercent = __state;
        }
    }
}

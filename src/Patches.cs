using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;


namespace WeaponAimMod.src
{
    public class Patches
    {
        public static class PatchTankStart
        {
            private static readonly FieldInfo m_TankRuntimePrefabs = typeof(ManSpawn).GetField("m_TankRuntimePrefabs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            public static void ReplaceTank(Transform instance)
            {
                // This successfully replaces the Tank with the ExpandedTank
                // However, due to component pooling, there is a component pool for Tank, and a component pool for ExpandedTank
                // So just doing OnPool/OnSpawn, etc. would lead to double call to OnSpawn, which is bad
                // So we use some reflection to inject our additions to the beginning of Tank's calls for that stuff

                // Tank tank = instance.GetComponent<Tank>();
                // tank.enabled = false;
                // ExpandedTank newTank = instance.gameObject.AddComponent<ExpandedTank>();

                instance.gameObject.AddComponent<TargetManager>();
                OctantVision octantVision = instance.gameObject.AddComponent<OctantVision>();
                // octantVision.OnPool();

                // newTank.dragSphere = tank.dragSphere;

                // Take every single TechComponent, replace Tank with ExpandedTank

                // Tank poolPrefab = Singleton.Manager<ComponentPool>.inst.GetOriginalPrefab<Tank>(tank);
                // UnityEngine.Object.Destroy(tank);
                return;
            }

            public static void Postfix(ManSpawn __instance)
            {
                object tank_prefabs = m_TankRuntimePrefabs.GetValue(__instance);
                FieldInfo std_field = tank_prefabs.GetType().GetField("stdPrefab", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo net_field = tank_prefabs.GetType().GetField("netPrefab", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                Transform stdPrefab = (Transform)std_field.GetValue(tank_prefabs);
                Transform netPrefab = (Transform)net_field.GetValue(tank_prefabs);

                Console.WriteLine($"TEST PREFABS EQUALITY: {stdPrefab == netPrefab}");

                PatchTankStart.ReplaceTank(stdPrefab);
                PatchTankStart.ReplaceTank(netPrefab);

                Console.WriteLine($"start called. std_prefab: [{stdPrefab.name}], net_prefab: [{netPrefab.name}]");

                return;
            }
        }
    }
}

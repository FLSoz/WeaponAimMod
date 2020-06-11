using System.Reflection;
using System;
using System.Linq;
using Harmony;
using UnityEngine;

namespace WeaponAimMod.src
{
    internal class IngressPoint
    {
        public static void Main()
        {
            Console.WriteLine(Physics.gravity);
            // WeaponAimMod.src.WrappedDataHolder.unitGravity = Physics.gravity;
            // WeaponAimMod.src.WrappedDataHolder.gravityMagnitude = Physics.gravity.magnitude;
            // WeaponAimMod.src.WrappedDataHolder.unitGravity.Normalize();
            HarmonyInstance.Create("flsoz.ttmm.weaponaim.mod").PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}

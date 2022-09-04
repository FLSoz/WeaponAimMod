using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace WeaponAimMod
{
    [RequireComponent(typeof(FireData))]
    public class ModuleWeaponAimHelper : Module
    {
        public float muzzleVelocityOverride;
        internal TimedFuseData timedFuseData;
        internal Vector3 barrelOffset;
        public bool ignoreOffset;
    }
}

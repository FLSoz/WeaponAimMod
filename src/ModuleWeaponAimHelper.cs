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
        [SerializeField]
        internal GimbalAimer.AxisConstraint firstAxis = GimbalAimer.AxisConstraint.Free;
        [SerializeField]
        internal GimbalAimer.AxisConstraint secondAxis = GimbalAimer.AxisConstraint.Free;
        [SerializeField]
        internal TimedFuseData timedFuseData;
        [SerializeField]
        internal Vector3 gimbalOffset;
        [SerializeField]
        internal Vector3 intermediateGimbalOffset;
        [SerializeField]
        internal float barrelLength;
        public bool ignoreOffset;
        public bool useHighArc;
        [SerializeField]
        internal bool useGravity;
    }
}

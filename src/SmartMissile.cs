using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace WeaponAimMod
{
    [RequireComponent(typeof(SeekingProjectile))]
    public class SmartMissile : MonoBehaviour
    {
        public float time;
        public float expireTime;
        public float totalTime;
        public Vector3 position;
        public Vector3 velocity;
        public Visible target;
        public bool finalCorrection;

        public bool disableLead;
        public float velocityOverride;
        public bool disableBallistic;
    }
}

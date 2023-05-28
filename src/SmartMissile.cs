using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;

namespace WeaponAimMod
{
    [RequireComponent(typeof(SeekingProjectile))]
    public class SmartMissile : MonoBehaviour
    {
        public float shotTime;
        public float expireTime;
        public float totalTime;

        public float boostStrength;

        public Vector3 targetPosition;
        public Vector3 targetVelocity;
        public Vector3 targetAcceleration;
        public Visible target;
        public bool finalCorrection;

        public bool disableLead;
        public float velocityOverride;
        public bool disableBallistic;

        public bool boostersFiring;

        private Rigidbody _rbody;
        private Rigidbody rbody
        {
            get {
                if (_rbody.IsNull())
                {
                    _rbody = this.GetComponentInParents<Rigidbody>(true);
                }
                return _rbody;
            }
            set { _rbody = value; }
        }

        public float GetMaxVelocity(float timeRemaining)
        {
            if (this.velocityOverride == 0.0f) {
                return this.rbody.velocity.magnitude + timeRemaining * this.boostStrength;
            }
            return this.velocityOverride;
        }

        private static readonly FieldInfo m_Force = AccessTools.Field(typeof(BoosterJet), "m_Force");
        public void Init()
        {
            if (this.velocityOverride == 0)
            {
                this.disableBallistic = false;
                BoosterJet[] boosters = this.GetComponentsInChildren<BoosterJet>();
                foreach (BoosterJet booster in boosters)
                {
                    this.boostStrength += (float)m_Force.GetValue(booster);
                }
            }
        }

        public void OnPool()
        {
            this.rbody = this.GetComponentInParents<Rigidbody>(true);
        }
        public void OnSpawn()
        {
            if (this.rbody.IsNull())
            {
                // only happens with Random additions stuff
                this.rbody = this.GetComponentInParents<Rigidbody>(true);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Harmony;
using UnityEngine;


namespace WeaponAimMod.src
{
    [RequireComponent(typeof(Tank))]
    public class TargetManager : TechComponent
    {
        [SerializeField]
        private Vector3[] acceleration;
        [SerializeField]
        private Vector3[] velocity;
        private Rigidbody rbody;
        private int index;
        private int currentWindow;
        private const int windowSize = 10;
        private Vector3 windowAcceleration;

        public Vector3 Acceleration {
            get {
                return this.currentWindow == 0 ? Vector3.zero : this.windowAcceleration / this.currentWindow;
            }
        }

        public static Vector3 GetAcceleration(Tank tank)
        {
            if (tank != null)
            {
                TargetManager targetManager = tank.GetComponentInParent<TargetManager>();
                if (targetManager != null)
                {
                    return targetManager.Acceleration;
                }
            }
            return Vector3.zero;
        }

        private void FixedUpdate()
        {
            if (!Singleton.Manager<ManPauseGame>.inst.IsPaused)
            {
                if (this.rbody != null)
                {
                    int lastIndex = this.index - 1;
                    if (lastIndex == -1)
                    {
                        lastIndex += windowSize;
                    }

                    Vector3 newVelocity = this.rbody.velocity;
                    this.velocity[this.index] = newVelocity;
                    Vector3 thisAcceleration = newVelocity - this.velocity[lastIndex];
                    if (this.currentWindow < windowSize && this.index != 0)
                    {
                        this.windowAcceleration += thisAcceleration;
                    }
                    else
                    {
                        this.windowAcceleration += thisAcceleration - this.acceleration[this.index];
                    }
                    this.acceleration[this.index] = thisAcceleration;

                    // increment. Modulo is slow apparently
                    if (this.currentWindow < windowSize)
                    {
                        this.currentWindow++;
                    }
                    this.index++;
                    if (this.index == windowSize)
                    {
                        this.index = 0;
                    }
                }
                else
                {
                    this.rbody = base.Tech.rbody;
                }
            }
        }

        private void PrePool()
        {
            this.acceleration = new Vector3[windowSize];
            this.velocity = new Vector3[windowSize];
            this.windowAcceleration = Vector3.zero;
            this.index = 0;
            this.currentWindow = 0;
        }

        private void OnPool() {
            this.rbody = base.GetComponent<Rigidbody>();
            if (this.rbody == null)
            {
                this.rbody = null;
            }
        }

        private void OnDepool()
        {
            this.rbody = null;
        }

        // Token: 0x060039E5 RID: 14821 RVA: 0x0012612D File Offset: 0x0012432D
        private void OnSpawn()
        {
            // this.rbody = base.Tech.rbody;
        }

        // Token: 0x060039E6 RID: 14822 RVA: 0x0012614B File Offset: 0x0012434B
        private void OnRecycle()
        {
        }
    }
}

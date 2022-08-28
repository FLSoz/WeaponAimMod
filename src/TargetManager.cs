using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using RuntimeDebugDraw;


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

        private LineRenderer trajectoryRenderer;
        private const int predictionLength = 10;
        private const float predictionSeconds = 5.0f;

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

        public static void DrawTarget(Tank tank)
        {
            if (WeaponAimMod.DEBUG && tank != null)
            {
                TargetManager targetManager = tank.GetComponentInParent<TargetManager>();
                if (targetManager != null)
                {
                    targetManager.SetDrawTarget();
                    return;
                }
            }
        }

        internal bool ToDrawTarget = false;

        internal void SetDrawTarget()
        {
            this.ToDrawTarget = true;
        }

        internal void DrawTarget()
        {
            WeaponAimMod.logger.Debug($"DRAWING TARGET BOX AROUND {this.Tech.name}");
            Bounds bounds = this.Tech.blockBounds;
            Vector3 center = this.Tech.boundsCentreWorld;
            float x = bounds.extents.x;
            float y = bounds.extents.y;
            float z = bounds.extents.z;
            // draw the 12 edges
            // z (forwards)
            Draw.DrawLine(center + new Vector3(x, y, z), center + new Vector3(x, y, -z), Color.red);
            Draw.DrawLine(center + new Vector3(-x, y, z), center + new Vector3(-x, y, -z), Color.red);
            Draw.DrawLine(center + new Vector3(x, -y, z), center + new Vector3(x, -y, -z), Color.red);
            Draw.DrawLine(center + new Vector3(-x, -y, z), center + new Vector3(-x, -y, -z), Color.red);
            // y (verticals)
            Draw.DrawLine(center + new Vector3(x, y, z), center + new Vector3(x, -y, z), Color.red);
            Draw.DrawLine(center + new Vector3(-x, y, z), center + new Vector3(-x, -y, z), Color.red);
            Draw.DrawLine(center + new Vector3(x, y, -z), center + new Vector3(x, -y, -z), Color.red);
            Draw.DrawLine(center + new Vector3(-x, y, -z), center + new Vector3(-x, -y, -z), Color.red);
            // x (right)
            Draw.DrawLine(center + new Vector3(x, y, z), center + new Vector3(-x, y, z), Color.red);
            Draw.DrawLine(center + new Vector3(x, -y, z), center + new Vector3(-x, -y, z), Color.red);
            Draw.DrawLine(center + new Vector3(x, y, -z), center + new Vector3(-x, y, -z), Color.red);
            Draw.DrawLine(center + new Vector3(x, -y, -z), center + new Vector3(-x, -y, -z), Color.red);
        }

        private void FixedUpdate()
        {
            WeaponAimMod.logger.Trace("TargetManager FixedUpdate");
            WeaponAimMod.logger.Trace(this.Tech.name);
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
                    WeaponAimMod.logger.Trace("Before velocity index");
                    this.velocity[this.index] = newVelocity;
                    WeaponAimMod.logger.Trace("Set current velocity");
                    Vector3 thisAcceleration = newVelocity - this.velocity[lastIndex];
                    if (this.currentWindow < windowSize && this.index != 0)
                    {
                        this.windowAcceleration += thisAcceleration;
                    }
                    else
                    {
                        this.windowAcceleration += thisAcceleration - this.acceleration[this.index];
                    }
                    WeaponAimMod.logger.Trace("Before acceleration index");
                    this.acceleration[this.index] = thisAcceleration;
                    WeaponAimMod.logger.Trace("Set current acceleration");

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
                    WeaponAimMod.logger.Trace("Drawing debug line");

                    if (WeaponAimMod.DEBUG && !this.Tech.IsPlayer)
                    {
                        WeaponAimMod.logger.Debug($"RENDERING DEBUG LINE FOR TECH {this.Tech.name}");
                        this.trajectoryRenderer.enabled = true;
                        this.trajectoryRenderer.positionCount = predictionLength;
                        this.trajectoryRenderer.SetPositions(this.GetTrajectoryPrediction());
                    }
                    else
                    {
                        this.trajectoryRenderer.enabled = false;
                    }
                    WeaponAimMod.logger.Trace("Drew debug line");
                }
                else
                {
                    WeaponAimMod.logger.Trace("Fetching tech rbody");
                    this.rbody = base.Tech.rbody;
                    WeaponAimMod.logger.Trace("Fetched tech rbody");
                }
            }
        }

        private void OnUpdate()
        {
            if (this.ToDrawTarget)
            {
                this.ToDrawTarget = false;
                this.DrawTarget();
            }
        }
        
        private Vector3[] GetTrajectoryPrediction()
        {
            Vector3[] result = new Vector3[predictionLength];
            for (int i = 0; i < predictionLength; i++)
            {
                float frameTime = i * (predictionSeconds / predictionLength);
                Vector3 velocity = this.rbody.velocity;
                Vector3 acceleration = this.Acceleration;
                result[i] = this.rbody.position + (velocity * frameTime) + (acceleration * frameTime * frameTime / 2);
            }
            return result;
        }

        private bool PredictionEnabled = true;

        private void PrePool()
        {
            WeaponAimMod.logger.Debug("TargetManager PrePool");
            this.acceleration = new Vector3[windowSize];
            this.velocity = new Vector3[windowSize];
            this.windowAcceleration = Vector3.zero;
            this.index = 0;
            this.currentWindow = 0;
        }

        private void OnPool()
        {
            WeaponAimMod.logger.Debug("TargetManager OnPool");
            this.rbody = base.GetComponent<Rigidbody>();
            if (this.rbody == null)
            {
                this.rbody = base.Tech?.rbody;
            }
            base.Tech.UpdateEvent.Subscribe(new Action(this.OnUpdate));

            this.trajectoryRenderer = base.gameObject.GetComponent<LineRenderer>();
            if (this.trajectoryRenderer == null)
            {
                this.trajectoryRenderer = base.gameObject.AddComponent<LineRenderer>();
            }
            WeaponAimMod.logger.Debug("Setting up Trajectory Renderer");
            trajectoryRenderer.material = new Material(Shader.Find("Sprites/Default"));
            trajectoryRenderer.widthMultiplier = 0.2f;
            trajectoryRenderer.positionCount = 2;
            trajectoryRenderer.SetPosition(0, Vector3.zero);
            trajectoryRenderer.SetPosition(1, Vector3.zero);
            trajectoryRenderer.startColor = Color.red;
            trajectoryRenderer.endColor = Color.red;
            trajectoryRenderer.enabled = true;
            WeaponAimMod.logger.Debug("Setup Trajectory Renderer");
        }

        private void OnDepool()
        {
            WeaponAimMod.logger.Debug("TargetManager OnDepool");
            this.rbody = null;
        }

        // Token: 0x060039E5 RID: 14821 RVA: 0x0012612D File Offset: 0x0012432D
        private void OnSpawn()
        {
            WeaponAimMod.logger.Debug("TargetManager OnSpawn");
            // this.rbody = base.Tech.rbody;
        }

        // Token: 0x060039E6 RID: 14822 RVA: 0x0012614B File Offset: 0x0012434B
        private void OnRecycle()
        {
            WeaponAimMod.logger.Debug("TargetManager OnRecycle");
        }
    }
}
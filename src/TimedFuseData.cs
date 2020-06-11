using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Serialization;

namespace WeaponAimMod
{
    [RequireComponent(typeof(FireData))]
    public class TimedFuseData : MonoBehaviour
    {
        public float m_FuseTime = 10.0f;
        public float offset = 0.0f;
    }
}

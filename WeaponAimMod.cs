using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechComponentInjector;
using System.Reflection;
using WeaponAimMod.src;
using HarmonyLib;
using NLog;
using LogManager;


namespace WeaponAimMod
{
    public class WeaponAimMod : ModBase
    {
        internal const string HarmonyID = "flsoz.ttmm.weaponaim.mod";
        internal static readonly Harmony harmony = new Harmony(HarmonyID);
        private static bool Inited = false;

        internal static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        internal static void ConfigureLogger()
        {
            Manager.LogConfig config = new Manager.LogConfig
            {
                layout = "${longdate} | ${level:uppercase=true:padding=-5:alignmentOnTruncation=left} | ${logger:shortName=true} | ${message}  ${exception}",
                keepOldFiles = false,
                minLevel = LogLevel.Trace
            };
            Manager.RegisterLogger(logger, config);
        }

        public static Type[] LoadBefore()
        {
            return new Type[] { typeof(TechComponentInjector.TechComponentInjector) };
        }

        public override void EarlyInit()
        {
            if (!Inited)
            {
                ConfigureLogger();
                IngressPoint.SetupConfig();
                IngressPoint.SetupUI();
                Inited = true;
            }
        }

        public override bool HasEarlyInit()
        {
            return true;
        }

        public override void DeInit()
        {
            TechComponentInjector.TechComponentInjector.RemoveTechComponentToInject(typeof(TargetManager));
            TechComponentInjector.TechComponentInjector.RemoveTechComponentToInject(typeof(OctantVision));
            harmony.UnpatchAll(HarmonyID);
        }

        public override void Init()
        {
            TechComponentInjector.TechComponentInjector.AddTechComponentToInject(typeof(TargetManager));
            TechComponentInjector.TechComponentInjector.AddTechComponentToInject(typeof(OctantVision));
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}

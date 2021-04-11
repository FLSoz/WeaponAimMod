using System.Reflection;
using System;
using System.Linq;
using Harmony;
using UnityEngine;
using ModHelper.Config;
using Nuterra.NativeOptions;


namespace WeaponAimMod.src
{
    // Do the lead patch
    internal class WeaponAimSettings
    {
        // public static Vector3 unitGravity;
        // public static float gravityMagnitude;
        public static bool PlayerLead = true;
        public static bool EnemyLead = false;
        public static bool PlayerMissileLead = true;
        public static bool EnemyMissileLead = false;
        public static bool SmartMissile = true;
    }

    internal class IngressPoint
    {
        public static ModConfig Config; // It'd be helpful to save the ModConfig class if you'd like to use it later
        
        const string ModName = "Weapon Aim Mod";
        public static OptionToggle PlayerLead;
        public static OptionToggle EnemyLead;
        public static OptionToggle PlayerMissileLead;
        public static OptionToggle EnemyMissileLead;
        public static OptionToggle SmartMissile;

        public static void Main()
        {
            // Console.WriteLine(Physics.gravity);
            // SozCore.IngressPoint.AddRequiredTechComponent(typeof(TargetManager));
            // .WriteLine("Assume added TargetManager as requried component");

            // WeaponAimMod.src.WrappedDataHolder.unitGravity = Physics.gravity;
            // WeaponAimMod.src.WrappedDataHolder.gravityMagnitude = Physics.gravity.magnitude;
            // WeaponAimMod.src.WrappedDataHolder.unitGravity.Normalize();
            HarmonyInstance.Create("flsoz.ttmm.weaponaim.mod").PatchAll(Assembly.GetExecutingAssembly());

            // mod Config
            Config = new ModConfig();
            Config.BindConfig<WeaponAimSettings>(null, "PlayerLead"); // Using BindConfig will set UseRef as true, changing some methods to work with the RefList.
            Config.BindConfig<WeaponAimSettings>(null, "EnemyLead"); // If 'Setting2' was loaded from the config.json, it will apply the variable to the field.
            Config.BindConfig<WeaponAimSettings>(null, "SmartMissile"); // Using BindConfig will set UseRef as true, changing some methods to work with the RefList.
            Config.BindConfig<WeaponAimSettings>(null, "PlayerMissileLead"); // If 'Setting2' was loaded from the config.json, it will apply the variable to the field.
            Config.BindConfig<WeaponAimSettings>(null, "EnemyMissileLead"); // If 'Setting2' was loaded from the config.json, it will apply the variable to the field.

            // UI
            PlayerLead = new OptionToggle("Player leading", ModName, WeaponAimSettings.PlayerLead);
            PlayerLead.onValueSaved.AddListener(() => { WeaponAimSettings.PlayerLead = PlayerLead.SavedValue; Config.WriteConfigJsonFile(); });

            EnemyLead = new OptionToggle("Enemy leading", ModName, WeaponAimSettings.EnemyLead);
            EnemyLead.onValueSaved.AddListener(() => { WeaponAimSettings.EnemyLead = EnemyLead.SavedValue; Config.WriteConfigJsonFile(); });

            EnemyMissileLead = new OptionToggle("Enemy-fired missile leading", ModName, WeaponAimSettings.EnemyMissileLead);
            EnemyMissileLead.onValueSaved.AddListener(() => { WeaponAimSettings.EnemyMissileLead = EnemyMissileLead.SavedValue; Config.WriteConfigJsonFile(); });

            PlayerMissileLead = new OptionToggle("Player-fired missile leading", ModName, WeaponAimSettings.PlayerMissileLead);
            PlayerMissileLead.onValueSaved.AddListener(() => { WeaponAimSettings.PlayerMissileLead = PlayerMissileLead.SavedValue; Config.WriteConfigJsonFile(); });

            SmartMissile = new OptionToggle("Smart missiles", ModName, WeaponAimSettings.SmartMissile);
            SmartMissile.onValueSaved.AddListener(() => { WeaponAimSettings.SmartMissile = SmartMissile.SavedValue; Config.WriteConfigJsonFile(); });
        }
    }
}

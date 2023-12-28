using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Lethal_Doors.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace LethalDoors
{
    [BepInPlugin(modGUID, modName, modVersion )]
    public class LethalDoors : BaseUnityPlugin
    {
        private const string modGUID = "saintk.LethalDoors";
        private const string modName = "Lethal Doors";
        private const string modVersion = "1.0.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);
        internal ManualLogSource mls;

        public static LethalDoors Instance { get; private set; }


        void Awake()
        { 
            if (Instance == null)
            {
                Instance = this;
            }

            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);

            mls.LogInfo("saintkendrick's Lethal Doors Mod");
            harmony.PatchAll(typeof(DoorInteractionPatch));
            mls.LogInfo("saintkendrick's Door Patch");


        }
        public static void Log(string message)
        {
            if (Instance != null)
            {
                Instance.mls.LogInfo(message);
            }
        }

    }
}

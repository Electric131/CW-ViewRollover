using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace ViewRollover
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGUID = "Electric131.ViewRollover";
        public const string ModName = "ViewRollover";
        public const string ModVersion = "1.0.1";

        public static ManualLogSource? logger;

        private void Awake()
        {
            logger = Logger;
            logger.LogInfo($"Plugin {ModGUID} loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));

            logger.LogInfo($"Patches created successfully");
        }

        private static void CalculateNewViews(RoomStatsHolder instance)
        {
            logger.LogInfo("Calculating new views..");

            // CurrentDay will be the new day (ie. When this runs at the end of day 3, CurrentDay will already be 4)
            int oldViewMultiple = BigNumbers.GetScoreToViews(1, instance.CurrentDay - 1);
            int newViewMultiple = BigNumbers.GetScoreToViews(1, instance.CurrentDay);

            int oldQuotaTarget = BigNumbers.GetQuota(Mathf.FloorToInt((instance.CurrentDay - 2) / (float)instance.DaysPerQutoa + 1E-08f));

            int remainingViews = (instance.CurrentQuota * oldViewMultiple) - (oldQuotaTarget * oldViewMultiple);
            int returnScore = remainingViews / newViewMultiple;

            logger.LogInfo("Remaining views: " + remainingViews);
            logger.LogInfo("Transferred views: " + (returnScore * newViewMultiple));

            int newScore = (returnScore > 0) ? returnScore : 0; // Ensure the player doesn't get a negative score
            logger.LogInfo("New score: " + newScore);
            instance.CurrentQuota = newScore;
        }

        [HarmonyPatch(typeof(RoomStatsHolder), nameof(RoomStatsHolder.CalculateNewQuota))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> CalculateNewQuotaPatch(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldc_I4_0)
            )
            .ThrowIfInvalid("Failed to find where views is set to 0!")
            .RemoveInstructions(3) // Remove both 'Ldarg_0' and 'Ldc_I4_0' and the code that sets CurrentQuota
            // Replace the line 'this.CurrentQuota = 0;' with a call to CalculateNewViews here
            .Insert(
                new CodeInstruction(OpCodes.Ldarg_0),   // this (RoomStatsHolder)
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Plugin), nameof(Plugin.CalculateNewViews)))
            )
            .InstructionEnumeration();
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonerDefaults {
  public class PrisonerDefaults : Mod {
    public PrisonerDefaults(ModContentPack content) : base(content) {
      var harmony = new Harmony("xyz.msws.prisonerdefaults");
      harmony.PatchAll();
    }

    // Allow for people to delete the prisoner policy w/o breaking the game
    [CanBeNull]
    public static FoodPolicy getPrisonerFoodPolicy() {
      var policies = Current.Game.foodRestrictionDatabase.AllFoodRestrictions;
      return policies.FirstOrDefault(
        p => p.label.Equals("Prisoner".Translate()));
    }
  }


  // Patch to assign the prisoner food policy to prisoners instead of the default one
  [HarmonyPatch(typeof(Pawn_FoodRestrictionTracker),
    nameof(Pawn_FoodRestrictionTracker.CurrentFoodPolicy), MethodType.Getter)]
  public static class PatchPawn_FooodRestrictionTracker_CurrentFoodPolicy {
    private static readonly FieldInfo curPolicyField =
      AccessTools.Field(typeof(Pawn_FoodRestrictionTracker), "curPolicy");

    public static bool Prefix(Pawn_FoodRestrictionTracker __instance) {
      if (!__instance.pawn.IsPrisonerOfColony) return true;
      if (curPolicyField.GetValue(__instance) != null) return true;
      var prisonerPolicy = PrisonerDefaults.getPrisonerFoodPolicy();
      if (prisonerPolicy == null) return true;

      curPolicyField.SetValue(__instance, prisonerPolicy);
      return false;
    }
  }

  // Patch to automatically create the Prisoner food policy for new games
  [HarmonyPatch(typeof(FoodRestrictionDatabase),
    "GenerateStartingFoodRestrictions")]
  public static class
    PatchFoodRestrictionDatabase_GenerateStartingFoodRestrictions {
    public static void Postfix(FoodRestrictionDatabase __instance) {
      var restriction = __instance.MakeNewFoodRestriction();
      restriction.label = "Prisoner".Translate();
      restriction.CopyFrom(__instance.DefaultFoodRestriction());
    }
  }

  // Patch to automatically switch prisoners to the default food policy upon recruitment
  [HarmonyPatch(typeof(RecruitUtility), nameof(RecruitUtility.Recruit))]
  public static class PatchRecruitUtility_Recruit {
    public static void Postfix(Pawn pawn, Faction faction,
      Pawn recruiter = null) {
      if (pawn == null || faction == null) return;
      var prisonerPolicy = PrisonerDefaults.getPrisonerFoodPolicy();
      if (prisonerPolicy == null) return;

      // Only auto-switch if the prisoner is using the Prisoner food policy
      if (pawn.foodRestriction.CurrentFoodPolicy != prisonerPolicy) return;

      // Switch to the default food policy
      pawn.foodRestriction.CurrentFoodPolicy = Current.Game
       .foodRestrictionDatabase.DefaultFoodRestriction();
    }
  }
}
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace QualityRange
{
    [StaticConstructorOnStartup]
    internal static class VerbUtility
    {
        public static Dictionary<ThingDef, List<VerbProperties>> oldProperties = new Dictionary<ThingDef, List<VerbProperties>>();
        public static List<ThingWithComps> processedWeapons = new List<ThingWithComps>();
        static VerbUtility()
        {
            new Harmony("Phoneixx.QualityRange").PatchAll();
        }

        public static void TryExtendWeapon(ThingWithComps weapon)
        {
            DrawStatsReport_Patch.interruptWork = true;
            if (weapon.def.IsRangedWeapon && weapon.TryGetQuality(out QualityCategory qc))
            {
                var comp = weapon.GetComp<CompEquippable>();
                if (comp != null)
                {
                    if (oldProperties.TryGetValue(weapon.def, out var oldVerbs))
                    {
                        Traverse.Create(weapon.def).Field("verbs").SetValue(oldVerbs);
                    }
                    else
                    {
                        oldProperties[weapon.def] = weapon.def.Verbs;
                    }
                    comp.verbTracker = new VerbTracker(comp);
                    var qualityMultiplier = GetQualityMultiplier(qc);
                    foreach (Verb verb in comp.AllVerbs)
                    {
                        if (verb.verbProps.range > 1.42)
                        {
                            ExtendVerbRangeBy(verb, qualityMultiplier);
                        }
                    }
                }
            }
            DrawStatsReport_Patch.interruptWork = false;
        }

        private static float GetQualityMultiplier(QualityCategory qualityCategory)
        {
            switch (qualityCategory)
            {
                case QualityCategory.Awful: return 0.8f;
                case QualityCategory.Poor: return 0.9f;
                case QualityCategory.Normal: return 1f;
                case QualityCategory.Good: return 1.1f;
                case QualityCategory.Excellent: return 1.2f;
                case QualityCategory.Masterwork: return 1.35f;
                case QualityCategory.Legendary: return 1.5f;
                default: return 1f;
            }
        }

        private static void ExtendVerbRangeBy(Verb verb, float multiplier)
        {
            var newProperties = new VerbProperties();
            foreach (var fieldInfo in typeof(VerbProperties).GetFields())
            {
                try
                {
                    var newField = verb.verbProps.GetType().GetField(fieldInfo.Name);
                    newField.SetValue(newProperties, fieldInfo.GetValue(verb.verbProps));
                }
                catch { }
            }
            newProperties.range *= multiplier;
            verb.verbProps = newProperties;
        }
    }

    [HarmonyPatch(typeof(ThingDef), "Verbs", MethodType.Getter)]
    public static class ThingDef_Verbs_Patch
    {
        public static ThingWithComps weaponToLookUp;
        private static bool Prefix(ref List<VerbProperties> __result)
        {
            if (weaponToLookUp != null)
            {
                var comp = weaponToLookUp.GetComp<CompEquippable>();
                if (comp != null)
                {
                    __result = comp.AllVerbs.Select(x => x.verbProps).ToList();
                    return false;
                }
            }
            return true;
        }
    }
    
    [HarmonyPatch(typeof(StatsReportUtility), "DrawStatsReport", new Type[] { typeof(Rect), typeof(Thing) })]
    public static class DrawStatsReport_Patch
    {
        public static bool interruptWork;
        private static void Prefix(Rect rect, Thing thing, out List<VerbProperties> __state)
        {
            __state = null;
            if (!interruptWork && thing is ThingWithComps weapon && weapon.def.IsRangedWeapon)
            {
                ThingDef_Verbs_Patch.weaponToLookUp = weapon;
                var comp = weapon.GetComp<CompEquippable>();
                if (comp != null)
                {
                    __state = weapon.def.Verbs.ListFullCopy();
                    var customVerbs = comp.AllVerbs.Select(x => x.verbProps).ToList();
                    Traverse.Create(weapon.def).Field("verbs").SetValue(customVerbs);
                }
            }
        }
    
        private static void Postfix(List<VerbProperties> __state)
        {
            if (__state != null)
            {
                Traverse.Create(ThingDef_Verbs_Patch.weaponToLookUp.def).Field("verbs").SetValue(__state);
                ThingDef_Verbs_Patch.weaponToLookUp = null;
            }
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), "SpawnSetup")]
    public static class Thing_SpawnSetup_Patch
    {
        public static void Postfix(ThingWithComps __instance)
        {
            VerbUtility.TryExtendWeapon(__instance);
        }
    }
    
    [HarmonyPatch(typeof(CompQuality), "SetQuality")]
    public static class SetQuality_Patch
    {
        public static void Postfix(CompQuality __instance)
        {
            VerbUtility.TryExtendWeapon(__instance.parent);
        }
    }
    
    [HarmonyPatch(typeof(CompQuality), "PostExposeData")]
    public static class CompQuality_ExposeData_Patch
    {
        public static void Postfix(CompQuality __instance)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                VerbUtility.TryExtendWeapon(__instance.parent);
            }
        }
    }
    
    [HarmonyPatch(typeof(ThingWithComps), "ExposeData")]
    public static class Thing_ExposeData_Patch
    {
        public static void Postfix(ThingWithComps __instance)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                VerbUtility.TryExtendWeapon(__instance);
            }
        }
    }
}
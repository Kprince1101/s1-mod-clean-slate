#if (IL2CPPMELON)
using S1Effects = Il2CppScheduleOne.Effects;
using S1Product = Il2CppScheduleOne.Product;
using EffectList = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Effects.Effect>;
#elif (IL2CPPBEPINEX)
using S1Effects = ScheduleOne.Effects;
using S1Product = ScheduleOne.Product;
using EffectList = Il2CppSystem.Collections.Generic.List<ScheduleOne.Effects.Effect>;
#elif (MONOMELON || MONOBEPINEX)
using S1Effects = ScheduleOne.Effects;
using S1Product = ScheduleOne.Product;
using EffectList = System.Collections.Generic.List<ScheduleOne.Effects.Effect>;
#endif
using System;
using HarmonyLib;
using S1API.Logging;
using S1API.Products;

namespace S1API.Internal.Patches
{
    /// <summary>
    /// INTERNAL: Applies mod-registered mixing reactions after the game's mixing calculation.
    /// </summary>
    [HarmonyPatch(typeof(S1Effects.EffectMixCalculator), "MixProperties")]
    internal static class MixReactionPatches
    {
        private static readonly Log Logger = new Log("MixReactionPatches");

        private const int MaxProperties = 8;

        /// <summary>
        /// Applies registered mixing reactions to the effect list the game produced for a mix.
        /// </summary>
        /// <param name="__result">The mix result (Harmony-injected); reassigned to a cloned, transformed list so the game's own list is never mutated.</param>
        /// <param name="newProperty">The effect the mixed-in ingredient contributed.</param>
        /// <param name="drugType">The drug being mixed.</param>
        [HarmonyPostfix]
        private static void MixProperties_Postfix(
            ref EffectList __result,
            S1Effects.Effect newProperty,
            S1Product.EDrugType drugType)
        {
            try
            {
                if (__result == null || newProperty == null)
                    return;

                var rules = MixReactions.Snapshot();
                if (rules.Length == 0)
                    return;

                var mixerId = newProperty.ID;

                // The game can return a shared list (e.g. a product definition's Properties for a named recipe),
                // so never mutate __result directly. Clone once, only if a rule actually fires.
                EffectList working = null;

                foreach (var rule in rules)
                {
                    if (rule.Drug.HasValue && (int)rule.Drug.Value != (int)drugType)
                        continue;

                    if (!string.Equals(rule.MixerEffectId, mixerId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var current = working ?? __result;
                    if (IndexOfId(current, rule.WhenContainsId) < 0)
                        continue;

                    var addEffect = MixReactions.ResolveAddResult(rule);
                    if (addEffect == null)
                        continue;

                    if (working == null)
                        working = Clone(__result);

                    var matchIndex = IndexOfId(working, rule.WhenContainsId);
                    if (matchIndex < 0)
                        continue;

                    if (rule.Replace)
                    {
                        working[matchIndex] = addEffect;
                        RemoveDuplicateIds(working, addEffect.ID);
                    }
                    else if (working.Count < MaxProperties && IndexOfId(working, addEffect.ID) < 0)
                    {
                        working.Add(addEffect);
                    }
                }

                if (working != null)
                    __result = working;
            }
            catch (Exception ex)
            {
                Logger.Error($"Mixing reaction postfix failed: {ex}");
            }
        }

        private static EffectList Clone(EffectList source)
        {
            var copy = new EffectList();
            if (source == null)
                return copy;
            for (int i = 0; i < source.Count; i++)
                copy.Add(source[i]);
            return copy;
        }

        private static int IndexOfId(EffectList list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var effect = list[i];
                if (effect != null && string.Equals(effect.ID, id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static void RemoveDuplicateIds(EffectList list, string id)
        {
            var first = IndexOfId(list, id);
            if (first < 0)
                return;

            for (int i = list.Count - 1; i > first; i--)
            {
                var effect = list[i];
                if (effect != null && string.Equals(effect.ID, id, StringComparison.OrdinalIgnoreCase))
                    list.RemoveAt(i);
            }
        }
    }
}

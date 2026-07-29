#if (IL2CPPMELON)
using S1Properties = Il2CppScheduleOne.Effects;
#elif (MONOMELON || MONOBEPINEX || IL2CPPBEPINEX)
using S1Properties = ScheduleOne.Effects;
#endif
using System;
using System.Collections.Generic;
using S1API.Internal.Properties;
using S1API.Properties.Interfaces;

namespace S1API.Products
{
    /// <summary>
    /// Registers custom mixing reactions: deterministic rules that transform the effects a mix produces.
    /// A rule fires when a product is mixed with an ingredient contributing a specific effect and the result
    /// already carries another specific effect, letting mods build combo and chain effects.
    /// </summary>
    /// <remarks>
    /// Rules are applied after the game's own mixing calculation, on every client (the calculation is
    /// deterministic, so no networking is required as long as every peer has the same rules and effects).
    /// </remarks>
    public static class MixReactions
    {
        /// <summary>
        /// INTERNAL: A single registered mixing reaction.
        /// </summary>
        internal sealed class Rule
        {
            internal string MixerEffectId { get; set; } = string.Empty;
            internal string WhenContainsId { get; set; } = string.Empty;
            internal PropertyBase AddResult { get; set; } = null!;
            internal DrugType? Drug { get; set; }
            internal bool Replace { get; set; }
            internal S1Properties.Effect? S1Resolved { get; set; }
        }

        private static readonly object Gate = new object();
        private static readonly List<Rule> Rules = new List<Rule>();

        /// <summary>
        /// Adds a mixing reaction rule.
        /// </summary>
        /// <param name="mixerEffect">The effect the mixed-in ingredient contributes (drives this reaction).</param>
        /// <param name="whenResultContains">An effect that must already be present in the mix result for the rule to fire.</param>
        /// <param name="addResult">The effect produced by the reaction.</param>
        /// <param name="drug">Restrict the rule to a specific drug, or null to apply to all drugs.</param>
        /// <param name="replaceMatched">
        /// If <c>true</c>, the produced effect replaces <paramref name="whenResultContains"/>;
        /// if <c>false</c>, it is added alongside it (up to the 8-effect cap).
        /// </param>
        public static void AddRule(
            PropertyBase mixerEffect,
            PropertyBase whenResultContains,
            PropertyBase addResult,
            DrugType? drug = null,
            bool replaceMatched = false)
        {
            if (mixerEffect == null)
                throw new ArgumentNullException(nameof(mixerEffect));
            if (whenResultContains == null)
                throw new ArgumentNullException(nameof(whenResultContains));
            if (addResult == null)
                throw new ArgumentNullException(nameof(addResult));

            lock (Gate)
            {
                Rules.Add(new Rule
                {
                    MixerEffectId = mixerEffect.ID,
                    WhenContainsId = whenResultContains.ID,
                    AddResult = addResult,
                    Drug = drug,
                    Replace = replaceMatched
                });
            }
        }

        /// <summary>
        /// Removes all registered mixing reaction rules.
        /// </summary>
        public static void Clear()
        {
            lock (Gate)
            {
                Rules.Clear();
            }
        }

        /// <summary>
        /// INTERNAL: Snapshot of the current rules for the mixing patch.
        /// </summary>
        internal static Rule[] Snapshot()
        {
            lock (Gate)
            {
                return Rules.ToArray();
            }
        }

        /// <summary>
        /// INTERNAL: Resolves and caches the produced effect for a rule.
        /// </summary>
        internal static S1Properties.Effect? ResolveAddResult(Rule rule)
        {
            if (rule.S1Resolved != null)
                return rule.S1Resolved;

            var resolved = PropertyResolver.ResolveToGameProperties(new[] { rule.AddResult });
            if (resolved.Count > 0)
                rule.S1Resolved = resolved[0];

            return rule.S1Resolved;
        }
    }
}

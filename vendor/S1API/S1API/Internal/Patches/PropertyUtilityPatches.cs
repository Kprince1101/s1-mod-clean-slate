#if (IL2CPPMELON)
using S1Product = Il2CppScheduleOne.Product;
using S1Properties = Il2CppScheduleOne.Effects;
using StringList = Il2CppSystem.Collections.Generic.List<string>;
using EffectList = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Effects.Effect>;
#elif (IL2CPPBEPINEX)
using S1Product = ScheduleOne.Product;
using S1Properties = ScheduleOne.Effects;
using StringList = Il2CppSystem.Collections.Generic.List<string>;
using EffectList = Il2CppSystem.Collections.Generic.List<ScheduleOne.Effects.Effect>;
#elif (MONOMELON || MONOBEPINEX)
using S1Product = ScheduleOne.Product;
using S1Properties = ScheduleOne.Effects;
using StringList = System.Collections.Generic.List<string>;
using EffectList = System.Collections.Generic.List<ScheduleOne.Effects.Effect>;
#endif
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using S1API.Logging;

namespace S1API.Internal.Patches
{
    /// <summary>
    /// INTERNAL: Safety net so a mod-created effect is never lost when the game resolves effects by ID.
    /// <c>PropertyUtility.GetProperties(List&lt;string&gt;)</c> silently drops any ID it cannot find in its lookup;
    /// this recovers custom effects from the mixing maps and the mixing-ingredient list, which our registry
    /// always populates, so a custom effect survives even if a load-order edge case leaves it out of the lookup.
    /// </summary>
    [HarmonyPatch]
    internal static class PropertyUtilityPatches
    {
        private static readonly Log Logger = new Log("PropertyUtilityPatches");

        private static readonly S1Product.EDrugType[] MixDrugs =
        {
            S1Product.EDrugType.Marijuana,
            S1Product.EDrugType.Methamphetamine,
            S1Product.EDrugType.Cocaine,
            S1Product.EDrugType.Shrooms
        };

        private static MethodBase TargetMethod()
        {
            // The GetProperties(List<string>) overload, distinguished from GetProperties(int tier).
            return typeof(S1Product.PropertyUtility)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(method => method.Name == "GetProperties"
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType != typeof(int));
        }

        [HarmonyPostfix]
        private static void GetProperties_Postfix(StringList __0, EffectList __result)
        {
            try
            {
                if (__0 == null || __result == null || __result.Count == __0.Count)
                    return;

                var productManager = S1Product.ProductManager.Instance;
                if (productManager == null)
                    return;

                for (int i = 0; i < __0.Count; i++)
                {
                    var id = __0[i];
                    if (string.IsNullOrEmpty(id) || ContainsId(__result, id))
                        continue;

                    var effect = FindInMixMaps(productManager, id) ?? FindInIngredients(productManager, id);
                    if (effect != null && !ContainsId(__result, id))
                        __result.Add(effect);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"GetProperties recovery failed: {ex}");
            }
        }

        private static bool ContainsId(EffectList list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var effect = list[i];
                if (effect != null && string.Equals(effect.ID, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static S1Properties.Effect FindInMixMaps(S1Product.ProductManager productManager, string id)
        {
            foreach (var drug in MixDrugs)
            {
                var map = productManager.GetMixerMap(drug);
                if (map == null || map.Effects == null)
                    continue;

                for (int i = 0; i < map.Effects.Count; i++)
                {
                    var mixerMapEffect = map.Effects[i];
                    if (mixerMapEffect?.Property != null &&
                        string.Equals(mixerMapEffect.Property.ID, id, StringComparison.OrdinalIgnoreCase))
                        return mixerMapEffect.Property;
                }
            }

            return null;
        }

        private static S1Properties.Effect FindInIngredients(S1Product.ProductManager productManager, string id)
        {
            var validMixIngredients = productManager.ValidMixIngredients;
            if (validMixIngredients == null)
                return null;

            for (int i = 0; i < validMixIngredients.Count; i++)
            {
                var properties = validMixIngredients[i]?.Properties;
                if (properties == null)
                    continue;

                for (int j = 0; j < properties.Count; j++)
                {
                    var effect = properties[j];
                    if (effect != null && string.Equals(effect.ID, id, StringComparison.OrdinalIgnoreCase))
                        return effect;
                }
            }

            return null;
        }
    }
}

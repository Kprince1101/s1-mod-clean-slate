using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MelonLoader;

namespace LegionCore.Diagnostics
{
    // Reflects real member lists (properties/fields/methods) straight off the loaded
    // Il2CppInterop-generated types this project keeps guessing wrong about - TerrainData's
    // GetHeights/SetHeights broke compilation, Sprites/Default rendered the storefront shell
    // as a transparent "ghost". One investigation pass instead of chasing one signature at a
    // time, each costing a full game-launch round trip. Reflecting on types already loaded
    // in-process (via typeof(...)) is more reliable than inspecting the DLL from an external
    // tool/script - all dependencies are already resolved by the game/MelonLoader itself.
    //
    // Defensive by design, not just around the outside: the first real run threw a
    // TypeLoadException just from reading one property's PropertyType (TerrainData's
    // DetailPrototype[]-returning property - Il2CppReferenceArray<DetailPrototype> apparently
    // violates a generic constraint under this build's interop), and because the old version
    // only wrote the file once at the very end, that one bad property lost the entire report -
    // Terrain, TreeInstance, Shader, everything. Every individual member access is now its own
    // try/catch so one broken property/field/method logs an error line and the rest of the
    // report still comes through.
    //
    // Writes a single text file next to this mod's own DLL, meant to become
    // docs/reference/<topic>-api.md once read - not committed as-is.
    public static class ApiSurfaceDump
    {
        public static void WriteReport(string fileName, params Type[] types)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"LegionCore API surface dump - {DateTime.Now:u}");
            sb.AppendLine(new string('=', 80));

            foreach (var type in types)
            {
                try
                {
                    DumpType(type, sb);
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  [ERROR dumping type {type?.FullName}: {ex.GetType().Name} - {ex.Message}]");
                }
            }

            try
            {
                AppendShaderList(sb);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  [ERROR dumping shader list: {ex.GetType().Name} - {ex.Message}]");
            }

            var path = GetOutputPath(fileName);
            try
            {
                File.WriteAllText(path, sb.ToString());
                MelonLogger.Msg($"LegionCore-Diagnostics: wrote API surface dump to {path}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"LegionCore-Diagnostics: failed to write dump to {path} - {ex.Message}");
            }
        }

        private static void DumpType(Type type, StringBuilder sb)
        {
            sb.AppendLine();
            if (type == null)
            {
                sb.AppendLine("---- (null type) ----");
                return;
            }

            sb.AppendLine($"---- {type.FullName} (assembly: {type.Assembly.GetName().Name}) ----");

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static;

            sb.AppendLine("  Properties:");
            foreach (var p in SafeGetMembers(() => type.GetProperties(flags), sb))
            {
                TrySafe(sb, $"property '{p.Name}'", () =>
                    sb.AppendLine($"    {p.PropertyType} {p.Name} {{ {(p.CanRead ? "get; " : "")}{(p.CanWrite ? "set; " : "")}}}"));
            }

            sb.AppendLine("  Fields:");
            foreach (var f in SafeGetMembers(() => type.GetFields(flags), sb))
            {
                TrySafe(sb, $"field '{f.Name}'", () =>
                    sb.AppendLine($"    {f.FieldType} {f.Name}"));
            }

            sb.AppendLine("  Methods:");
            foreach (var m in SafeGetMembers(() => type.GetMethods(flags).Where(m => !m.IsSpecialName).ToArray(), sb))
            {
                TrySafe(sb, $"method '{m.Name}'", () =>
                {
                    var ps = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType} {p.Name}"));
                    sb.AppendLine($"    {m.ReturnType} {m.Name}({ps})");
                });
            }
        }

        // GetProperties/GetFields/GetMethods themselves could throw for a hostile type, not
        // just the individual member accesses afterward - same defensive posture, one level up.
        private static T[] SafeGetMembers<T>(Func<T[]> getter, StringBuilder sb)
        {
            try
            {
                var members = getter();
                // Sorting can itself throw if a comparer touches a broken member - sort inside
                // the try so a bad sort still yields the unsorted list instead of nothing.
                Array.Sort(members, (a, b) => string.CompareOrdinal(GetMemberName(a), GetMemberName(b)));
                return members;
            }
            catch (Exception ex)
            {
                sb.AppendLine($"    [ERROR enumerating members: {ex.GetType().Name} - {ex.Message}]");
                return Array.Empty<T>();
            }
        }

        private static string GetMemberName<T>(T member) => member is MemberInfo mi ? mi.Name : member?.ToString() ?? "";

        private static void TrySafe(StringBuilder sb, string label, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"    [ERROR on {label}: {ex.GetType().Name} - {ex.Message}]");
            }
        }

        // Shader.GetShaderCount/GetShaderInfo aren't a runtime API at all - those are
        // UnityEditor.ShaderUtil build-inspection methods (editor-only), not
        // UnityEngine.Shader. That was my own mistake, not another interop gap.
        // Resources.FindObjectsOfTypeAll<Shader>() is the real runtime equivalent: every
        // Shader object currently loaded in memory. By the time this runs (game fully
        // loaded), that should cover whatever the vanilla scene/UI/vehicles are actually
        // using - a practical, real list instead of a hypothetical compiled-but-unused set.
        private static void AppendShaderList(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("---- Shaders currently loaded (Resources.FindObjectsOfTypeAll<Shader>()) ----");
            var shaders = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Shader>();
            sb.AppendLine($"  Total loaded: {shaders.Length}");

            var names = new List<string>();
            foreach (var s in shaders)
            {
                try { names.Add(s.name); }
                catch (Exception ex) { sb.AppendLine($"  [ERROR reading a shader name: {ex.GetType().Name} - {ex.Message}]"); }
            }
            names.Sort(StringComparer.Ordinal);
            foreach (var n in names)
                sb.AppendLine($"  '{n}'");
        }

        private static string GetOutputPath(string fileName)
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            return Path.Combine(dir, fileName);
        }
    }
}

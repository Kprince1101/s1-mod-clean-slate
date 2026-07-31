using System;
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
                DumpType(type, sb);

            AppendShaderList(sb);

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
            foreach (var p in type.GetProperties(flags).OrderBy(p => p.Name))
                sb.AppendLine($"    {p.PropertyType} {p.Name} {{ {(p.CanRead ? "get; " : "")}{(p.CanWrite ? "set; " : "")}}}");

            sb.AppendLine("  Fields:");
            foreach (var f in type.GetFields(flags).OrderBy(f => f.Name))
                sb.AppendLine($"    {f.FieldType} {f.Name}");

            sb.AppendLine("  Methods:");
            foreach (var m in type.GetMethods(flags).Where(m => !m.IsSpecialName).OrderBy(m => m.Name))
            {
                var ps = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType} {p.Name}"));
                sb.AppendLine($"    {m.ReturnType} {m.Name}({ps})");
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
            foreach (var s in shaders.OrderBy(s => s.name))
                sb.AppendLine($"  '{s.name}'");
        }

        private static string GetOutputPath(string fileName)
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            return Path.Combine(dir, fileName);
        }
    }
}

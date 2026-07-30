// Diffs LegionCore's watched vanilla members between an old and a new Assembly-CSharp.dll
// (Mono branch build; run this against the Mono decompiled/managed assemblies, not the
// Il2Cpp interop stubs). Usage:
//   dotnet run -- <old Assembly-CSharp.dll> <new Assembly-CSharp.dll> [watched-members.txt]
using Mono.Cecil;

if (args.Length < 2)
{
    Console.WriteLine("Usage: mw-update-check <old.dll> <new.dll> [watched-members.txt]");
    return 1;
}

string watchedPath = args.Length > 2 ? args[2] : Path.Combine(AppContext.BaseDirectory, "watched-members.txt");
if (!File.Exists(watchedPath))
    watchedPath = Path.Combine(Directory.GetCurrentDirectory(), "watched-members.txt");

var watched = File.ReadAllLines(watchedPath)
    .Select(l => l.Trim())
    .Where(l => l.Length > 0 && !l.StartsWith('#'))
    .ToList();

using var oldModule = ModuleDefinition.ReadModule(args[0]);
using var newModule = ModuleDefinition.ReadModule(args[1]);

Console.WriteLine("# LegionCore watched-member diff\n");
Console.WriteLine($"Old: `{args[0]}`  \nNew: `{args[1]}`\n");
Console.WriteLine("| Member | Status |");
Console.WriteLine("|---|---|");

int changedCount = 0;
foreach (var entry in watched)
{
    int lastDot = entry.LastIndexOf('.');
    if (lastDot < 0) continue;
    string typeName = entry[..lastDot];
    string memberName = entry[(lastDot + 1)..];

    var oldType = oldModule.GetType(typeName);
    var newType = newModule.GetType(typeName);

    if (oldType == null || newType == null)
    {
        Console.WriteLine($"| `{entry}` | TYPE MISSING ({(oldType == null ? "old" : "new")}) |");
        changedCount++;
        continue;
    }

    string? oldSig = DescribeMember(oldType, memberName);
    string? newSig = DescribeMember(newType, memberName);

    if (oldSig == null && newSig == null)
    {
        Console.WriteLine($"| `{entry}` | MEMBER MISSING (both) |");
        changedCount++;
    }
    else if (oldSig == null || newSig == null)
    {
        Console.WriteLine($"| `{entry}` | REMOVED ({(oldSig == null ? "was absent, now present" : "removed in new")}) |");
        changedCount++;
    }
    else if (oldSig != newSig)
    {
        Console.WriteLine($"| `{entry}` | CHANGED: `{oldSig}` -> `{newSig}` |");
        changedCount++;
    }
    else
    {
        Console.WriteLine($"| `{entry}` | unchanged |");
    }
}

Console.WriteLine($"\n{changedCount} of {watched.Count} watched members changed or missing.");
return 0;

static string? DescribeMember(TypeDefinition type, string memberName)
{
    var method = type.Methods.FirstOrDefault(m => m.Name == memberName);
    if (method != null)
        return $"{method.ReturnType.FullName} {method.Name}({string.Join(", ", method.Parameters.Select(p => p.ParameterType.FullName))})";

    var field = type.Fields.FirstOrDefault(f => f.Name == memberName);
    if (field != null)
        return $"field {field.FieldType.FullName}";

    var prop = type.Properties.FirstOrDefault(p => p.Name == memberName);
    if (prop != null)
        return $"property {prop.PropertyType.FullName}";

    var nested = type.NestedTypes.FirstOrDefault(t => t.Name == memberName);
    if (nested != null)
        return $"nested type {nested.FullName}";

    return null;
}

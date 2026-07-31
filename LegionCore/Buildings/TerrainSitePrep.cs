using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace LegionCore.Buildings
{
    // Clears terrain-painted trees and (once FlattenHeights is un-stubbed - see below)
    // flattens terrain height within a building-local-space rectangle (see SitePrepOptions).
    // Client-local edits only - Unity's Terrain component has no netcode - which matches
    // GRQD/LegionCore's existing single-player-only scope (see VehicleApi.Navigate's
    // host-only note). Works in the building's own rotated local space (not a world-space
    // AABB) so a rotated building still gets correct per-side padding.
    internal static class TerrainSitePrep
    {
        public static bool Run(Transform buildingTransform, SitePrepOptions o)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                MelonLogger.Warning("LegionCore-Buildings: TerrainSitePrep - no active terrain.");
                return false;
            }

            int treesRemoved;
            try
            {
                treesRemoved = ClearTrees(terrain, buildingTransform, o);
            }
            catch (System.Exception ex)
            {
                // Tree clearing is one part of a larger site-spawn sequence (shell, then this,
                // then the parking pad, then a notification) - letting an exception here
                // unwind the whole call killed all of those downstream steps for one bad tree
                // read, not just this one. Degrade instead of crash.
                MelonLogger.Error($"LegionCore-Buildings: tree clearing failed - {ex.Message}");
                treesRemoved = 0;
            }

            // FlattenHeights is temporarily disabled - TerrainData.GetHeights/SetHeights use
            // float[,] (2D arrays), which this build's Il2CppInterop-generated TerrainData
            // stub doesn't bind normally (GetHeights came back typed Il2CppObjectBase,
            // SetHeights wasn't found at all - real build errors, not a guess). Needs the
            // actual generated TerrainData member list from Legion's machine before this can
            // be fixed for real, rather than guessing at a replacement signature.
            MelonLogger.Warning("LegionCore-Buildings: terrain flattening skipped - " +
                "TerrainData.GetHeights/SetHeights aren't usable under this build's IL2CPP interop yet.");

            MelonLogger.Msg($"LegionCore-Buildings: site prep - removed {treesRemoved} trees " +
                $"(flattening pending) in local rect x[{o.LocalXMin:F1},{o.LocalXMax:F1}] z[{o.LocalZMin:F1},{o.LocalZMax:F1}].");
            return true;
        }

        private static bool IsInsideLocalRect(Vector3 worldPos, Transform buildingTransform, SitePrepOptions o)
        {
            var local = buildingTransform.InverseTransformPoint(worldPos);
            return local.x >= o.LocalXMin && local.x <= o.LocalXMax
                && local.z >= o.LocalZMin && local.z <= o.LocalZMax;
        }

        private static int ClearTrees(Terrain terrain, Transform buildingTransform, SitePrepOptions o)
        {
            var data = terrain.terrainData;
            if (data == null)
            {
                MelonLogger.Warning("LegionCore-Buildings: Terrain.terrainData is null - skipping tree clear.");
                return 0;
            }

            // Unity's Mono API guarantees treeInstances is at least an empty array; this
            // build's IL2CppInterop-generated TerrainData apparently doesn't hold that
            // guarantee (confirmed real crash: NullReferenceException here). Could mean either
            // no terrain-painted trees exist in this scene at all (Schedule I may place trees
            // as regular scene GameObjects instead - a different removal approach entirely),
            // or just that the getter needs different handling under this interop. Log which
            // it is instead of guessing.
            var instances = data.treeInstances;
            if (instances == null)
            {
                MelonLogger.Warning("LegionCore-Buildings: TerrainData.treeInstances returned null - " +
                    "no terrain-painted trees to clear (or unsupported here). If trees are still visible " +
                    "in-game, they're likely placed as scene GameObjects, not terrain instances, and need " +
                    "a different removal approach.");
                return 0;
            }

            var kept = new List<TreeInstance>(instances.Length);
            int removed = 0;

            for (int i = 0; i < instances.Length; i++)
            {
                var worldPos = Vector3.Scale(instances[i].position, data.size) + terrain.transform.position;
                if (IsInsideLocalRect(worldPos, buildingTransform, o)) removed++;
                else kept.Add(instances[i]);
            }

            data.treeInstances = kept.ToArray();
            return removed;
        }

        // FlattenHeights removed for now, not just disabled - it called TerrainData.
        // GetHeights/SetHeights (Mono-style float[,] signatures), and those don't compile
        // against this build's Il2CppInterop-generated TerrainData stub (GetHeights resolved
        // to Il2CppObjectBase - can't be indexed; SetHeights wasn't found at all). Rewriting
        // this needs the real member list from that generated stub - see the request in
        // conversation. The world-AABB-from-local-rect-corners scan approach above (and
        // IsInsideLocalRect) still apply once a working height read/write API is confirmed.
    }
}

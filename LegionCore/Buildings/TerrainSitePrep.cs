using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace LegionCore.Buildings
{
    // Clears terrain-painted trees and flattens terrain height within a building-local-space
    // rectangle (see SitePrepOptions). Both TerrainData.treeInstances reassignment and
    // SetHeights are client-local edits - Unity's Terrain component has no netcode - which
    // matches GRQD/LegionCore's existing single-player-only scope (see VehicleApi.Navigate's
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

            float flattenWorldY = buildingTransform.position.y + o.FlattenLocalY;

            int treesRemoved = ClearTrees(terrain, buildingTransform, o);
            FlattenHeights(terrain, buildingTransform, o, flattenWorldY);

            MelonLogger.Msg($"LegionCore-Buildings: site prep - removed {treesRemoved} trees, flattened local rect " +
                $"x[{o.LocalXMin:F1},{o.LocalXMax:F1}] z[{o.LocalZMin:F1},{o.LocalZMax:F1}] to y={flattenWorldY:F2}.");
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
            var instances = data.treeInstances;
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

        private static void FlattenHeights(Terrain terrain, Transform buildingTransform, SitePrepOptions o, float flattenWorldY)
        {
            var data = terrain.terrainData;
            var terrainPos = terrain.transform.position;
            var size = data.size;
            int res = data.heightmapResolution;

            // Conservative world-space scan box from the local rect's 4 corners - covers a
            // rotated footprint without under-scanning; each candidate pixel is then re-tested
            // against the actual local rect below.
            var corners = new[]
            {
                buildingTransform.TransformPoint(new Vector3(o.LocalXMin, 0f, o.LocalZMin)),
                buildingTransform.TransformPoint(new Vector3(o.LocalXMax, 0f, o.LocalZMin)),
                buildingTransform.TransformPoint(new Vector3(o.LocalXMax, 0f, o.LocalZMax)),
                buildingTransform.TransformPoint(new Vector3(o.LocalXMin, 0f, o.LocalZMax)),
            };
            float worldXMin = float.MaxValue, worldXMax = float.MinValue, worldZMin = float.MaxValue, worldZMax = float.MinValue;
            foreach (var c in corners)
            {
                worldXMin = Mathf.Min(worldXMin, c.x);
                worldXMax = Mathf.Max(worldXMax, c.x);
                worldZMin = Mathf.Min(worldZMin, c.z);
                worldZMax = Mathf.Max(worldZMax, c.z);
            }

            int xBase = Mathf.Clamp(Mathf.FloorToInt((worldXMin - terrainPos.x) / size.x * (res - 1)), 0, res - 1);
            int xEnd = Mathf.Clamp(Mathf.CeilToInt((worldXMax - terrainPos.x) / size.x * (res - 1)), 0, res - 1);
            int zBase = Mathf.Clamp(Mathf.FloorToInt((worldZMin - terrainPos.z) / size.z * (res - 1)), 0, res - 1);
            int zEnd = Mathf.Clamp(Mathf.CeilToInt((worldZMax - terrainPos.z) / size.z * (res - 1)), 0, res - 1);
            int xCount = xEnd - xBase + 1;
            int zCount = zEnd - zBase + 1;
            if (xCount <= 0 || zCount <= 0) return;

            float normalizedHeight = Mathf.Clamp01((flattenWorldY - terrainPos.y) / size.y);
            var existing = data.GetHeights(xBase, zBase, xCount, zCount);
            var heights = new float[zCount, xCount];

            for (int zi = 0; zi < zCount; zi++)
            {
                for (int xi = 0; xi < xCount; xi++)
                {
                    float u = (float)(xBase + xi) / (res - 1);
                    float v = (float)(zBase + zi) / (res - 1);
                    var worldPos = new Vector3(terrainPos.x + u * size.x, 0f, terrainPos.z + v * size.z);
                    heights[zi, xi] = IsInsideLocalRect(worldPos, buildingTransform, o) ? normalizedHeight : existing[zi, xi];
                }
            }

            data.SetHeights(xBase, zBase, heights);
        }
    }
}

using LegionCore.Buildings;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(CleanSlate.Plugin), "Clean Slate", "0.0.1", "Legion", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace CleanSlate
{
    public class Plugin : MelonMod
    {
        // Candidate storefront lot corners, real in-game readings from Legion's scouting trip
        // (docs/clean-slate-spec.md "Storefront Site"). Left/right give the front wall's
        // real-world width and facing direction; depth is provisional (the spec's back
        // reading duplicated the front one) so StorefrontShellOptions' default Depth is used
        // until a real back-of-lot reading replaces it.
        private static readonly Vector3 StorefrontLotLeft = new(117.88f, 1.11f, -1.02f);
        private static readonly Vector3 StorefrontLotRight = new(128.72f, 1.49f, -2.45f);

        // Site prep padding/parking sizing - see docs/clean-slate-spec.md "Storefront Site"
        // for the "left side (sewer entrance) stays as-is, right side gets a small parking
        // lot, doors on both the sidewalk and parking sides" direction.
        private const float FrontClearPadding = 3f;
        private const float BackClearPadding = 2f;
        private const float EastWallGap = 0.5f;
        private const float ParkingPadLength = 8f;
        private const float ParkingPadDepth = 5f;

        private bool _sent;
        private bool _storefrontSpawned;
        private bool _apiDumpLogged;
        private GameObject? _storefrontShell;
        private GameObject? _parkingPad;

        // Terrain.terrainData showed up null on one run even after LegionCore.Api.IsGameReady
        // fired - not a signature/API-shape question (no guessing there), just a plain "is this
        // reference actually assigned yet" timing check. Give the terrain a few seconds to
        // finish whatever's assigning terrainData before falling back to spawning anyway (with
        // tree-clear/flatten skipped, same as today) rather than silently doing this once and
        // giving up forever if it's still null on the very first ready frame.
        private int _terrainWaitFrames;
        private const int TerrainWaitFrameLimit = 300;

        public override void OnInitializeMelon()
        {
            LegionCore.Api.Initialize();
            LoggerInstance.Msg("Clean Slate loaded, waiting for NotificationsManager...");
        }

        public override void OnUpdate()
        {
            LegionCore.Api.CheckVersion();

            if (!_apiDumpLogged && LegionCore.Api.IsGameReady)
            {
                _apiDumpLogged = true;
                // Temporary investigation pass, not a permanent feature: dumps real reflected
                // member lists for the terrain types PrepareSite needs (GetHeights/SetHeights
                // broke compilation) plus every shader actually compiled into this build (the
                // shell's primitives render translucent/"ghostly" under Sprites/Default - a
                // transparent-blend shader, wrong fit for solid architecture). One pass
                // instead of chasing each surprise with its own round trip. Writes
                // LegionCore_ApiDump.txt next to CleanSlate.dll in the Mods folder.
                LegionCore.Diagnostics.ApiSurfaceDump.WriteReport("LegionCore_ApiDump.txt",
                    typeof(Terrain), typeof(TerrainData), typeof(TreeInstance), typeof(Shader));
            }

            if (!_storefrontSpawned && LegionCore.Api.IsGameReady)
            {
                bool terrainReady = Terrain.activeTerrain != null && Terrain.activeTerrain.terrainData != null;
                if (!terrainReady && _terrainWaitFrames < TerrainWaitFrameLimit)
                {
                    _terrainWaitFrames++;
                }
                else
                {
                    if (!terrainReady)
                    {
                        LoggerInstance.Warning($"CleanSlate: Terrain.terrainData still null after " +
                            $"{TerrainWaitFrameLimit} frames - spawning anyway, tree clear/flatten will be skipped.");
                    }
                    _storefrontSpawned = true;
                    SpawnStorefrontSite();
                }
            }

            if (_sent || !LegionCore.Api.Notifications.IsReady) return;

            LegionCore.Api.Notifications.Send("Clean Slate", "Plugin loaded and wrapper is working.");
            LoggerInstance.Msg("Clean Slate: sent proof-of-life notification via LegionCore.");
            _sent = true;
        }

        // M2 build step: a primitive-built shell at the candidate lot, sized/faced from the
        // two real corner readings above, plus terrain prep (tree clear + flatten) and a
        // parking pad on the east ("right") side, per Legion's direction: left side (sewer
        // entrance) untouched, right side flattened with a couple parking spaces and its own
        // door in addition to the sidewalk-facing front door.
        private void SpawnStorefrontSite()
        {
            try
            {
                SpawnStorefrontSiteInner();
            }
            catch (System.Exception ex)
            {
                // This is several steps in sequence (shell, site prep, parking pad,
                // notification) - an unguarded exception partway through has already once
                // silently skipped every step after it (TerrainSitePrep's tree-clear crash).
                // Surface anything unexpected instead of letting it vanish into MelonLoader's
                // per-frame exception log with no context on which step failed.
                LoggerInstance.Error($"CleanSlate: SpawnStorefrontSite failed - {ex}");
            }
        }

        private void SpawnStorefrontSiteInner()
        {
            var flatLeft = new Vector3(StorefrontLotLeft.x, 0f, StorefrontLotLeft.z);
            var flatRight = new Vector3(StorefrontLotRight.x, 0f, StorefrontLotRight.z);
            var widthDir = flatRight - flatLeft;
            if (widthDir.sqrMagnitude < 0.0001f)
            {
                LoggerInstance.Warning("CleanSlate: storefront lot corners are identical - skipping site spawn.");
                return;
            }
            widthDir.Normalize();

            // Yaw that rotates local +X onto widthDir (derivation: Quaternion.Euler(0,Y,0) *
            // Vector3.right = (cos Y, 0, -sin Y), so Y = atan2(-widthDir.z, widthDir.x)).
            float yawDeg = Mathf.Atan2(-widthDir.z, widthDir.x) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(0f, yawDeg, 0f);

            var options = new StorefrontShellOptions
            {
                Width = Vector3.Distance(flatLeft, flatRight),
            };

            // Real bug, not the known flattening-disabled issue: StorefrontLotLeft.y (1.11) is
            // a raw scouted reading off the lot's sloped/uneven ground (spec: "ground needs to
            // be flattened to street level"), not street level itself - the spec's separate
            // "front, middle-ish" reading sits at y=0.04, over a meter lower. Passing that
            // elevated y straight into SetPositionAndRotation put the whole shell a meter-plus
            // above where it should sit. flatLeft already zeroes y for the width/rotation math
            // above; use it for the spawn origin too instead of the unzeroed corner reading.
            _storefrontShell = LegionCore.Api.Buildings.SpawnStorefrontShell(flatLeft, rotation, options);
            if (_storefrontShell == null)
            {
                LoggerInstance.Msg("CleanSlate: storefront shell spawn failed.");
                return;
            }
            LoggerInstance.Msg($"CleanSlate: storefront shell spawned at {flatLeft}, width={options.Width:F2}m.");

            var buildingTransform = _storefrontShell.transform;

            // Local rect: X in [0, west wall face] stays untouched (sewer entrance side);
            // extends east past the wall far enough to cover the parking pad; small front/back
            // padding for the sidewalk approach and rear clearance.
            var siteOptions = new SitePrepOptions
            {
                LocalXMin = 0f,
                LocalXMax = options.Width + EastWallGap + ParkingPadDepth + 1f,
                LocalZMin = -FrontClearPadding,
                LocalZMax = options.Depth + BackClearPadding,
                FlattenLocalY = 0f,
            };
            bool prepped = LegionCore.Api.Buildings.PrepareSite(buildingTransform, siteOptions);
            LoggerInstance.Msg(prepped
                ? "CleanSlate: storefront site prepped (tree clear attempted - see LegionCore-Buildings log for count; flattening still disabled)."
                : "CleanSlate: storefront site prep failed - no active terrain.");

            // Parking pad sits flush against the east wall - pad-local +X runs along the
            // building's depth (front-to-back), pad-local +Z extends east (away from the
            // wall). See LegionCore.Buildings.ParkingPadOptions for the axis convention.
            float padOriginZ = (options.Depth + ParkingPadLength) / 2f;
            var padOrigin = buildingTransform.TransformPoint(new Vector3(options.Width + EastWallGap, 0f, padOriginZ));
            var padRotation = buildingTransform.rotation * Quaternion.Euler(0f, 90f, 0f);
            var parkingOptions = new ParkingPadOptions { Length = ParkingPadLength, Depth = ParkingPadDepth };

            _parkingPad = LegionCore.Api.Buildings.SpawnParkingPad(padOrigin, padRotation, parkingOptions);
            LoggerInstance.Msg(_parkingPad != null
                ? $"CleanSlate: parking pad spawned at {padOrigin}."
                : "CleanSlate: parking pad spawn failed.");

            // Stationary and far from GRQD's van/dock testing area - call out exact
            // coordinates so it's easy to navigate to rather than stumbled on.
            if (LegionCore.Api.Notifications.IsReady)
            {
                LegionCore.Api.Notifications.Send("Clean Slate",
                    $"Storefront site built near ({StorefrontLotLeft.x:F0}, {StorefrontLotLeft.z:F0}).");
            }
        }
    }
}

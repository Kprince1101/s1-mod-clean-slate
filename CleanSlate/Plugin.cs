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

        private bool _sent;
        private bool _storefrontSpawned;
        private GameObject? _storefrontShell;

        public override void OnInitializeMelon()
        {
            LegionCore.Api.Initialize();
            LoggerInstance.Msg("Clean Slate loaded, waiting for NotificationsManager...");
        }

        public override void OnUpdate()
        {
            LegionCore.Api.CheckVersion();

            if (!_storefrontSpawned && LegionCore.Api.IsGameReady)
            {
                _storefrontSpawned = true;
                SpawnStorefrontShell();
            }

            if (_sent || !LegionCore.Api.Notifications.IsReady) return;

            LegionCore.Api.Notifications.Send("Clean Slate", "Plugin loaded and wrapper is working.");
            LoggerInstance.Msg("Clean Slate: sent proof-of-life notification via LegionCore.");
            _sent = true;
        }

        // First real M2 build step: a primitive-built shell at the candidate lot, sized/faced
        // from the two real corner readings (see StorefrontLotLeft/Right above). No terrain
        // flattening, functional door, or interior yet - see docs/clean-slate-spec.md "M2
        // Storefront Site" for what's deferred and why.
        private void SpawnStorefrontShell()
        {
            var flatLeft = new Vector3(StorefrontLotLeft.x, 0f, StorefrontLotLeft.z);
            var flatRight = new Vector3(StorefrontLotRight.x, 0f, StorefrontLotRight.z);
            var widthDir = flatRight - flatLeft;
            if (widthDir.sqrMagnitude < 0.0001f)
            {
                LoggerInstance.Warning("CleanSlate: storefront lot corners are identical - skipping shell spawn.");
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

            _storefrontShell = LegionCore.Api.Buildings.SpawnStorefrontShell(StorefrontLotLeft, rotation, options);
            if (_storefrontShell != null)
                LoggerInstance.Msg($"CleanSlate: storefront shell spawned at {StorefrontLotLeft}, width={options.Width:F2}m.");
            else
                LoggerInstance.Msg("CleanSlate: storefront shell spawn failed.");
        }
    }
}

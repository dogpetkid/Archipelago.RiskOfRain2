using System.Collections.Generic;
using RoR2;

namespace Archipelago.RiskOfRain2.Handlers
{
    class StageBlockerHandler : IHandler
    {

        // A list of stages that should be blocked because they are locked by archipelago
        // uses scene names: https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Developer-Reference/Scene-Names/
        List<int> blocked_stages;

        public StageBlockerHandler()
        {
            // TODO figure out what stages to block from the settings or YAML or something
            blocked_stages = new List<int>();

            // XXX remove test code to populate list
            blocked_stages.Add(15); // golemplains
            blocked_stages.Add(16); // golemplains2
            blocked_stages.Add(39); // snowyforest
            blocked_stages.Add(12); // foggyswamp
            blocked_stages.Add(3); // ancientloft
            blocked_stages.Add(47); // wispgraveyard
            blocked_stages.Add(41); // sulfurpools
            blocked_stages.Add(37); // shipgraveyard
            blocked_stages.Add(35); // rootjungle

            // TODO create a way to prevent hardlock when trying to load to commencement
        }

        public void Hook()
        {
            On.RoR2.Run.CanPickStage += Run_CanPickStage;
        }

        public void UnHook()
        {
            On.RoR2.Run.CanPickStage -= Run_CanPickStage;
        }

        private bool Run_CanPickStage(On.RoR2.Run.orig_CanPickStage orig, Run self, SceneDef scenedef)
        {
            Log.LogDebug($"Checking CanPickStage for {scenedef.nameToken}..."); // XXX remove extra debug
            int index = (int) scenedef.sceneDefIndex;
            foreach (int block in blocked_stages)
            {
                // if the stage is blocked, it cannot be picked
                if (index == block)
                {
                    Log.LogDebug("blocking."); // XXX remove extra debug
                    return false;
                }
            }

            Log.LogDebug("passing through."); // XXX remove extra debug

            return orig(self, scenedef);
        }

    }
}

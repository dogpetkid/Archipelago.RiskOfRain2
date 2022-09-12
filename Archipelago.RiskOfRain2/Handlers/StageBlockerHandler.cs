using EntityStates;
using R2API.Utils;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Handlers
{
    class StageBlockerHandler : IHandler
    {
        // setup all scene indexes as megic numbers
        // scenes from https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Developer-Reference/Scene-Names/
        // main scenes
        public const int ancientloft = 3;       // Aphelian Sanctuary
        public const int arena = 4;             // Void Fields
        public const int blackbeach = 7;        // Distant Roost
        public const int blackbeach2 = 8;       // Distant Roost
        public const int dampcavesimple = 10;   // Abyssal Depths
        public const int foggyswamp = 12;       // Wetland Aspect
        public const int frozenwall = 13;       // Rallypoint Delta
        public const int golemplains = 15;      // Titanic Plains
        public const int golemplains2 = 16;     // Titanic Plains
        public const int goolake = 17;          // Abandoned Aqueduct
        public const int itancientloft = 20;    // The Simulacrum
        public const int itdampcave = 21;       // The Simulacrum
        public const int itfrozenwall = 22;     // The Simulacrum
        public const int itgolemplains = 23;    // The Simulacrum
        public const int itgoolake = 24;        // The Simulacrum
        public const int itmoon = 25;           // The Simulacrum
        public const int itskymeadow = 26;      // The Simulacrum
        public const int moon2 = 32;            // Commencement
        public const int rootjungle = 35;       // Sundered Grove
        public const int shipgraveyard = 37;    // Siren's Call
        public const int skymeadow = 38;        // Sky Meadow
        public const int snowyforest = 39;      // Siphoned Forest
        public const int sulfurpools = 41;      // Sulfur Pools
        public const int voidstage = 45;        // Void Locus
        public const int voidraid = 46;         // The Planetarium
        public const int wispgraveyard = 47;    // Scorched Acres
        // hidden realms
        public const int artifactworld = 5;     // Hidden Realm: Bulwark's Ambry
        public const int bazaar = 6;            // Hidden Realm: Bazaar Between Time
        public const int goldshores = 14;       // Hidden Realm: Gilded Coast
        public const int limbo = 27;            // Hidden Realm: A Moment, Whole
        public const int mysteryspace = 33;     // Hidden Realm: A Moment, Fractured


        // A list of stages that should be blocked because they are locked by archipelago
        // uses scene names: https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Developer-Reference/Scene-Names/
        List<int> blocked_stages;

        public StageBlockerHandler()
        {
            blocked_stages = new List<int>();

            BlockAll();
            // TODO fix first stage unblock
        }

        public void Hook()
        {
            On.RoR2.Run.CanPickStage += Run_CanPickStage;
            On.RoR2.TeleporterInteraction.AttemptToSpawnAllEligiblePortals += TeleporterInteraction_AttemptToSpawnAllEligiblePortals1;
            On.RoR2.SeerStationController.SetTargetScene += SeerStationController_SetTargetScene;
            On.EntityStates.Interactables.MSObelisk.ReadyToEndGame.OnEnter += ReadyToEndGame_OnEnter;
            On.EntityStates.Interactables.MSObelisk.TransitionToNextStage.FixedUpdate += TransitionToNextStage_FixedUpdate;
            On.RoR2.PortalDialerController.PortalDialerIdleState.OnActivationServer += PortalDialerIdleState_OnActivationServer;
            On.RoR2.FrogController.Pet += FrogController_Pet;
            //On.RoR2.PortalSpawner.AttemptSpawnPortalServer += PortalSpawner_AttemptSpawnPortalServer;
            //On.RoR2.PortalSpawner.Start += PortalSpawner_Start;
            //On.RoR2.GenericInteraction.RoR2_IInteractable_GetInteractability += GenericInteraction_RoR2_IInteractable_GetInteractability;
            //On.RoR2.GenericInteraction.RoR2_IInteractable_OnInteractionBegin += GenericInteraction_RoR2_IInteractable_OnInteractionBegin;
            On.RoR2.SceneExitController.SetState += SceneExitController_SetState;
        }

        public void UnHook()
        {
            On.RoR2.Run.CanPickStage -= Run_CanPickStage;
            On.RoR2.TeleporterInteraction.AttemptToSpawnAllEligiblePortals -= TeleporterInteraction_AttemptToSpawnAllEligiblePortals1;
            On.RoR2.SeerStationController.SetTargetScene -= SeerStationController_SetTargetScene;
            On.EntityStates.Interactables.MSObelisk.ReadyToEndGame.OnEnter -= ReadyToEndGame_OnEnter;
            On.EntityStates.Interactables.MSObelisk.TransitionToNextStage.FixedUpdate -= TransitionToNextStage_FixedUpdate;
            On.RoR2.PortalDialerController.PortalDialerIdleState.OnActivationServer -= PortalDialerIdleState_OnActivationServer;
            On.RoR2.FrogController.Pet -= FrogController_Pet;
            //On.RoR2.PortalSpawner.AttemptSpawnPortalServer -= PortalSpawner_AttemptSpawnPortalServer;
            //On.RoR2.PortalSpawner.Start -= PortalSpawner_Start;
            //On.RoR2.GenericInteraction.RoR2_IInteractable_GetInteractability -= GenericInteraction_RoR2_IInteractable_GetInteractability;
            //On.RoR2.GenericInteraction.RoR2_IInteractable_OnInteractionBegin -= GenericInteraction_RoR2_IInteractable_OnInteractionBegin;
            On.RoR2.SceneExitController.SetState -= SceneExitController_SetState;
        }

        public void BlockAll()
        {
            // TODO add support for only blocking environments known to be in the pool
            // (eg. simulacrum should not be blocked if not in the pool, otherwise it would be permanently locked)

            // scenes from https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Developer-Reference/Scene-Names/
            // block all main scenes
            Block(ancientloft);        // Aphelian Sanctuary
            Block(arena);              // Void Fields
            Block(blackbeach);         // Distant Roost
            Block(blackbeach2);        // Distant Roost
            Block(dampcavesimple);     // Abyssal Depths
            Block(foggyswamp);         // Wetland Aspect
            Block(frozenwall);         // Rallypoint Delta
            Block(golemplains);        // Titanic Plains
            Block(golemplains2);       // Titanic Plains
            Block(goolake);            // Abandoned Aqueduct
            Block(itancientloft);      // The Simulacrum
            Block(itdampcave);         // The Simulacrum
            Block(itfrozenwall);       // The Simulacrum
            Block(itgolemplains);      // The Simulacrum
            Block(itgoolake);          // The Simulacrum
            Block(itmoon);             // The Simulacrum
            Block(itskymeadow);        // The Simulacrum
            Block(moon2);              // Commencement
            Block(rootjungle);         // Sundered Grove
            Block(shipgraveyard);      // Siren's Call
            Block(skymeadow);          // Sky Meadow
            Block(snowyforest);        // Siphoned Forest
            Block(sulfurpools);        // Sulfur Pools
            Block(voidstage);          // Void Locus
            Block(voidraid);           // The Planetarium
            Block(wispgraveyard);      // Scorched Acres
            // block all hidden realms
            Block(artifactworld);      // Hidden Realm: Bulwark's Ambry
            Block(bazaar);             // Hidden Realm: Bazaar Between Time
            Block(goldshores);         // Hidden Realm: Gilded Coast
            Block(limbo);              // Hidden Realm: A Moment, Whole
            Block(mysteryspace);       // Hidden Realm: A Moment, Fractured
        }

        public void UnBlockAll()
        {
            blocked_stages.Clear();
        }

        /**
         * Blocks a given environment.
         * Returns true if the stage was blocked by this call.
         */
        public bool Block(int index)
        {
            if (blocked_stages.Contains(index)) return false;
            Log.LogDebug($"Blocking {index}."); // XXX remove extra debug
            blocked_stages.Add(index);
            return true;
        }

        /**
         * Unblocks a given environment.
         * Returns true if the stage was unblocked by this call.
         */
        public bool UnBlock(int index)
        {
            // TODO the initial unblock will occur after the game starts the first stage, this should be fixed
            Log.LogDebug($"UnBlocking {index}."); // XXX remove extra debug
            return blocked_stages.Remove(index);
        }

        /**
         * Returns true if a stage is blocked.
         */
        public bool CheckBlocked(int index)
        {
            // Checking the list linearly should be fine.
            // Hooking update methods were avoided as much as they could be and the list itself is short.
            foreach (int block in blocked_stages)
            {
                if (index == block) return true;
            }
            return false;
        }

        /**
         * Swap the teleporter to use the next stage instead of go to Commencement if the environment is not unlocked.
         */
        private void SceneExitController_SetState(On.RoR2.SceneExitController.orig_SetState orig, SceneExitController self, SceneExitController.ExitState newState)
        {
            // This method can be used to block going to other environments,
            //  however there are other methods ways that are more intuitive or friendly to the user.

            if (
                // only attempt to switch anything if the exit state is finish, ie SetState will attempt to teleport
                newState == SceneExitController.ExitState.Finished &&
                // if there is no set destination, there is no need to block here
                (bool)self.destinationScene)
            {
                if (
                    // if the next scene is the moon...
                    (int)self.destinationScene.sceneDefIndex == moon2 &&
                    // and the moon should be blocked...
                    CheckBlocked(moon2)
                )
                {
                    Log.LogDebug("Blocking portal alignment for moon2.");
                    // then actually go to the next stage
                    self.useRunNextStageScene = true;
                    // TODO maybe make the teleporter completely unable to align with the moon for user friendliness
                }
                // XXX
                //if (
                //    // if the next scene is the arena...
                //    (int)self.destinationScene.sceneDefIndex == arena &&
                //    // and the arena should be blocked...
                //    CheckBlocked(arena)
                //)
                //{
                //    Log.LogDebug("Blocking entrance to arena.");
                //    // then actually go to the next stage
                //    self.useRunNextStageScene = true;
                //    // TODO maybe there is a way to block interaction with the portal itself...
                //}
                //if (
                //    // if the next scene is the voidstage...
                //    (int)self.destinationScene.sceneDefIndex == voidstage &&
                //    // and the voidstage should be blocked...
                //    CheckBlocked(voidstage)
                //)
                //{
                //    Log.LogDebug("Blocking entrance to voidstage.");
                //    // then actually go to the next stage
                //    self.useRunNextStageScene = true;
                //    // TODO maybe there is a way to block the voidstage portal itself
                //}
                // not blocking voidraid:
                // NOTE: Planetarium has two entrances, one in Void Locus and one in Commencement
                // Since this currently seems like an edge case where the player would truely decide to do both
                //  if the player gets the Planetarium portal from Void Locus, they can travel there.
                // Only the glass frog interaction in Commencement will be blocked.
                // This also prevents the player from becoming stuck.
            }
            orig(self, newState);
        }

        /**
         * Block interaction with the Void Fields and Void Locus portal if the environment is not unlocked.
         */
        // TODO test with arena
        // TODO test without arena
        // TODO test with voidstage
        // TODO test without voidstage
        private void GenericInteraction_RoR2_IInteractable_OnInteractionBegin(On.RoR2.GenericInteraction.orig_RoR2_IInteractable_OnInteractionBegin orig, GenericInteraction self, Interactor activator)
        {
            Log.LogDebug($"GenericInteraction_RoR2_IInteractable_OnInteractionBegin: contextToken {self.contextToken}"); // XXX remove possibly noisy debug
            switch (self.contextToken) {
                case "PORTAL_ARENA_CONTEXT":
                    if (CheckBlocked(arena))
                    {
                        ChatMessage.SendColored("The void rejects you.", new Color(0x88, 0x02, 0xd6));
                        return;
                    }
                    break;
                case "PORTAL_VOID_CONTEXT":
                    if (CheckBlocked(voidstage))
                    {
                        ChatMessage.SendColored("The void rejects you.", new Color(0x88, 0x02, 0xd6));
                        return;
                    }
                    break;
                // not blocking voidraid:
                // NOTE: Planetarium has two entrances, one in Void Locus and one in Commencement
                // Since this currently seems like an edge case where the player would truely decide to do both
                //  if the player gets the Planetarium portal from Void Locus, they can travel there.
                // Only the glass frog interaction in Commencement will be blocked.
                // This also prevents the player from becoming stuck.

                // Arguably the other portals could be handled here as well,
                // however it seems more user friendly to just not spawn the portal at all rather
                // than spawn the portal and make it unable to be interacted with.
            }
            Log.LogDebug($"GenericInteraction_RoR2_IInteractable_OnInteractionBegin: pass through"); // XXX remove possilby noisy debug
            orig(self, activator);
        }

        /**
         * Block interaction with the Void Fields portal if the environment is not unlocked.
         */
        [Obsolete]
        private Interactability GenericInteraction_RoR2_IInteractable_GetInteractability(On.RoR2.GenericInteraction.orig_RoR2_IInteractable_GetInteractability orig, GenericInteraction self, Interactor activator)
        {
            // XXX blocking chests?
            // XXX doesn't block portal
            Log.LogDebug($"GenericInteraction_RoR2_IInteractable_GetInteractability: contextToken {self.contextToken}"); // XXX remove possibly noisy debug
            switch (self.contextToken) {
                case "PORTAL_ARENA_CONTEXT":
                    if (CheckBlocked(arena))
                    {
                        ChatMessage.SendColored("The void rejects you.", new Color(0x88, 0x02, 0xd6));
                        return Interactability.ConditionsNotMet;
                    }
                    break;
                // Arguably the other portals could be handled here as well,
                // however it seems more user friendly to just not spawn the portal at all rather
                // than spawn the portal and make it unable to be interacted with.
            }
            Log.LogDebug($"GenericInteraction_RoR2_IInteractable_GetInteractability: pass through"); // XXX remove possilby noisy debug
            return orig(self, activator);
        }

        /**
         * Block the spawning of the Void Locus portal if the environment is not unlocked.
         */
        [Obsolete]
        private bool PortalSpawner_AttemptSpawnPortalServer(On.RoR2.PortalSpawner.orig_AttemptSpawnPortalServer orig, PortalSpawner self)
        {
            Log.LogDebug("PortalSpawner_AttemptSpawnPortalServer"); // XXX remove extra debug

            if (CheckBlocked(voidstage))
            {
                // inspired by https://github.com/harbingerofme/DebugToolkit/blob/a23a5e1b3e6651d4684fb52ee29fae52d8c0b3c2/Code/DT-Commands/CurrentRun.cs#L83

                // block voidstage
                if (self.portalSpawnCard == LegacyResourcesAPI.Load<InteractableSpawnCard>("SpawnCards/InteractableSpawnCard/iscDeepVoidPortal"))
                {
                    Log.LogDebug("Blocking portal spawn for voidstage from PortalSpawner_AttemptSpawnPortalServer.");
                    return false;
                }

                // not blocking voidraid:
                // NOTE: Planetarium has two entrances, one in Void Locus and one in Commencement
                // Since this currently seems like an edge case where the player would truely decide to do both
                //  if the player gets the Planetarium portal from Void Locus, they can travel there.
                // Only the glass frog interaction in Commencement will be blocked.
                // This also prevents the player from becoming stuck.

                // cannot block arena:
                // NOTE: It would be nice to block the portal to void fields in the same way.
                // This however can't happen because the portal is already added to the scene
                //  and so it would not spawn using this method.
                // On top of that, the portal does not have a spawn card so it cannot be distinguished
                //  even if this method was used to spawn it.
            }
            return orig(self);
        }

        /**
         * Block the spawning of the Void Locus portal if the environment is not unlocked.
         */
        [Obsolete]
        private void PortalSpawner_Start(On.RoR2.PortalSpawner.orig_Start orig, PortalSpawner self)
        {
            Log.LogDebug("PortalSpawner_Start"); // XXX remove extra debug

            if (CheckBlocked(voidstage))
            {
                // inspired by https://github.com/harbingerofme/DebugToolkit/blob/a23a5e1b3e6651d4684fb52ee29fae52d8c0b3c2/Code/DT-Commands/CurrentRun.cs#L83

                // block voidstage
                if (self.portalSpawnCard == LegacyResourcesAPI.Load<InteractableSpawnCard>("SpawnCards/InteractableSpawnCard/iscDeepVoidPortal"))
                {
                    Log.LogDebug("Blocking portal spawn for voidstage from PortalSpawner_Start.");
                    return;
                }

                // not blocking voidraid:
                // NOTE: Planetarium has two entrances, one in Void Locus and one in Commencement
                // Since this currently seems like an edge case where the player would truely decide to do both
                //  if the player gets the Planetarium portal from Void Locus, they can travel there.
                // Only the glass frog interaction in Commencement will be blocked.
                // This also prevents the player from becoming stuck.

                // cannot block arena:
                // NOTE: It would be nice to block the portal to void fields in the same way.
                // This however can't happen because the portal is already added to the scene
                //  and so it would not spawn using this method.
                // On top of that, the portal does not have a spawn card so it cannot be distinguished
                //  even if this method was used to spawn it.
            }
            orig(self);
        }

        /**
         * Block players from petting the frog and refund them if the Planetarium is not unlocked.
         */
        private void FrogController_Pet(On.RoR2.FrogController.orig_Pet orig, FrogController self, Interactor interactor)
        {
            // We block usage of the frog out of quality of life.
            // It would feel unfail to use 10 coins just to not spawn a portal or spawn a portal the user cannot use.
            // By adding coins back to the users inventory, it shows that the transaction cannot go through.
            // Adding a message also makes this even more clear.

            if (CheckBlocked(voidraid))
            {
                Log.LogDebug("Blocking petting the frog for planetarium.");
                // Only host can refund the coin and having the host send the message prevents duplicate messages.
                if (NetworkServer.active)
                {
                    Log.LogDebug("blocking planetarium as host.");
                    //// refund the lunar coin if the player who payed the coin is this client's player
                    //interactor.GetComponent<NetworkUser>().AwardLunarCoins(1); // (only the server actually executes the contents of this method) // XXX
                    foreach (NetworkUser local in NetworkUser.readOnlyLocalPlayersList)
                    {
                        Log.LogDebug("Refunding coins...");
                        local.AwardLunarCoins(1);
                        // This does in fact give more coins back in multiplayer since every player would get a coin.
                        // I don't have a solution for this right now : ^)
                    }

                    ChatMessage.SendColored("The frog does not want to be pet.", Color.white);
                }
                return;
            }
            orig(self, interactor);
        }

        /**
         * Prevent the dialer from changing states if the Bulwark's Ambry is not unlocked.
         */
        private void PortalDialerIdleState_OnActivationServer(On.RoR2.PortalDialerController.PortalDialerIdleState.orig_OnActivationServer orig, BaseState self, Interactor interactor)
        {
            if (CheckBlocked(artifactworld))
            {
                // give a message so the user is aware the portal dialer interaction is blocked
                ChatMessage.SendColored("Causes NRE... Not sure why.", new Color(0xd8, 0x7f, 0x20));
                return;
            }
            orig(self, interactor);
        }

        /**
         * Block going to A Monument, Whole if the environment is not unlocked.
         */
        private void TransitionToNextStage_FixedUpdate(On.EntityStates.Interactables.MSObelisk.TransitionToNextStage.orig_FixedUpdate orig, EntityStates.Interactables.MSObelisk.TransitionToNextStage self)
        {
            // If the player decides to commit to Obliterating,
            //  they transition state should simply end the game normally
            //  (since the player should not be allowed into limbo).
            if (CheckBlocked(limbo))
            {
                // run normal obliterate ending
                Run.instance.BeginGameOver(RoR2Content.GameEndings.ObliterationEnding);
                self.outer.SetNextState(new Idle());
            }
            orig(self);
        }

        /**
         * Give a warning before attempting to Obliterate while A Monument, Whole is still blocked.
         */
        private void ReadyToEndGame_OnEnter(On.EntityStates.Interactables.MSObelisk.ReadyToEndGame.orig_OnEnter orig, EntityStates.Interactables.MSObelisk.ReadyToEndGame self)
        {
            // Giving this warning is important for fairness.
            // This is because if the player decides to still Obliterate,
            //  we are just going to forcefully end the run.

            // Check if this is the server running this OnEnter, since mutliplayer clients could run this.
            // This is used to prevent duplicate messages being sent in multiplayer.
            if (NetworkServer.active && CheckBlocked(limbo))
            {
                for (int i = 0; i < CharacterMaster.readOnlyInstancesList.Count; i++)
                {
                    if (CharacterMaster.readOnlyInstancesList[i].inventory.GetItemCount(RoR2Content.Items.LunarTrinket) > 0)
                    {
                        ChatMessage.SendColored("Despite having Beads, you are not yet ready...", new Color(0x5d, 0xd5, 0xe2));
                        break;
                    }
                }
            }
            orig(self);
        }

        /**
         * Block shop interation with Bazaar Seers for environments that are blocked.
         */
        private void SeerStationController_SetTargetScene(On.RoR2.SeerStationController.orig_SetTargetScene orig, SeerStationController self, SceneDef sceneDef)
        {
            // For the seers, we will not change their behavior for how they pick environments.
            // This behaviour could be changed but would require changing logic in the middle of SetUpSeerStations() which would take IL Hooks.
            // This has the consequence that seers can pick environments that are blocked.
            // In that case, we can just block the seer be able to be interacted with.
            // We also should hide the destination of the Seer since the it will not be reenabled when the player obtains the environment.

            int index = (int) sceneDef.sceneDefIndex;
            if (CheckBlocked(index))
            {
                self.GetComponent<PurchaseInteraction>().SetAvailable(false);
                Log.LogDebug($"Bazaar Seer attempted to pick scene {index}; blocked.");
                return;
            }
            orig(self, sceneDef);
        }

        /**
         * Block portals for blocked environments that would be spawned by the finishing teleporter event.
         */
        private void TeleporterInteraction_AttemptToSpawnAllEligiblePortals1(On.RoR2.TeleporterInteraction.orig_AttemptToSpawnAllEligiblePortals orig, TeleporterInteraction self)
        {
            // If the player unlocks the environments while they have orbs, they can still recieved the portals.
            // But as soon as the teleporter finishes, we will not give them the portals.
            // There could be a more friendly alternative but this should be fine.

            // the portals spawned by the teleporter event are for:
            // Hidden Realm: Bazaar Between Time
            // Hidden Realm: Gilded Coast
            // Hidden Realm: A Moment, Fractured

            if (CheckBlocked(bazaar))
            {
                if (self.shouldAttemptToSpawnShopPortal) Log.LogDebug("Blue / bazaar portal blocked.");
                self.shouldAttemptToSpawnShopPortal = false;
            }
            if (CheckBlocked(goldshores))
            {
                if (self.shouldAttemptToSpawnGoldshoresPortal) Log.LogDebug("Gold / goldshores portal blocked.");
                self.shouldAttemptToSpawnGoldshoresPortal = false;
            }
            if (CheckBlocked(mysteryspace))
            {
                if (self.shouldAttemptToSpawnMSPortal) Log.LogDebug("Celestial / mysteryspace portal blocked.");
                self.shouldAttemptToSpawnMSPortal = false;
            }
            orig(self);
        }

        /**
         * Forcefully fail to the CanPickStage check for stages that are blocked.
         */
        private bool Run_CanPickStage(On.RoR2.Run.orig_CanPickStage orig, Run self, SceneDef scenedef)
        {
            Log.LogDebug($"Checking CanPickStage for {scenedef.nameToken}...");
            int index = (int) scenedef.sceneDefIndex;
            if (CheckBlocked(index))
            {
                // if the stage is blocked, it cannot be picked
                Log.LogDebug("blocking.");
                return false;
            }

            Log.LogDebug("passing through.");

            return orig(self, scenedef);
        }

    }
}

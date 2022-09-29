using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using RoR2;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using UnityEngine.Networking;

namespace Archipelago.RiskOfRain2.Handlers
{
    class LocationHandler : IHandler
    {
        // NOTE every mention of a "location" refers to the archipelago location checks
        // NOTE every mention of a "environment" refers to the risk of rain 2 scenes that are loaded and played


        // setup all scene indexes as megic numbers
        // scenes from https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Developer-Reference/Scene-Names/
        // main scenes
        public const int ancientloft = 3;       // Aphelian Sanctuary
        public const int blackbeach = 7;        // Distant Roost
        public const int blackbeach2 = 8;       // Distant Roost TODO environment varients should probably be treated the same
        public const int dampcavesimple = 10;   // Abyssal Depths
        public const int foggyswamp = 12;       // Wetland Aspect
        public const int frozenwall = 13;       // Rallypoint Delta
        public const int golemplains = 15;      // Titanic Plains
        public const int golemplains2 = 16;     // Titanic Plains TODO environment varients should probably be treated the same
        public const int goolake = 17;          // Abandoned Aqueduct
        public const int rootjungle = 35;       // Sundered Grove
        public const int shipgraveyard = 37;    // Siren's Call
        public const int skymeadow = 38;        // Sky Meadow
        public const int snowyforest = 39;      // Siphoned Forest
        public const int sulfurpools = 41;      // Sulfur Pools
        public const int wispgraveyard = 47;    // Scorched Acres

        public struct LocationInformationTemplate
        {
            public int chest_count { get; set; }
            public int shrine_count { get; set; }
            public int scavenger_count { get; set; }
            public int radio_scanner_count { get; set; }
            public int newt_alter_count { get; set; }
        }

        public static LocationInformationTemplate buildTemplateFromSlotData(Dictionary<string, object> SlotData)
        {
            LocationInformationTemplate locationtemplate = new LocationInformationTemplate();
            if (SlotData is not null)
            {
                if (SlotData.TryGetValue("chests_per_stage", out var chests_per_stage)) locationtemplate.chest_count = Convert.ToInt32(chests_per_stage);
                if (SlotData.TryGetValue("shrines_per_stage", out var shrines_per_stage)) locationtemplate.shrine_count = Convert.ToInt32(shrines_per_stage);
                if (SlotData.TryGetValue("scavengers_per_stage", out var scavengers_per_stage)) locationtemplate.scavenger_count = Convert.ToInt32(scavengers_per_stage);
                if (SlotData.TryGetValue("scanner_per_stage", out var scanner_per_stage)) locationtemplate.radio_scanner_count = Convert.ToInt32(scanner_per_stage);
                if (SlotData.TryGetValue("altars_per_stage", out var altars_per_stage)) locationtemplate.newt_alter_count = Convert.ToInt32(altars_per_stage);
            }
            return locationtemplate;
        }

        /// <summary>
        /// These values are sourced from the RoR2 Archipelago world code.
        /// These are used to determine the id values of locations.
        /// </summary>
        private readonly struct ArchipelagoLocationOffsets
        {
            // these values come from worlds/ror2/Locations.py in Archipelago
            public const int ror2_locations_start_orderedstage = 38000 + 250;
            public const int offset_ChestsPerEnvironment = 0;
            public const int offset_ShrinesPerEnvironment = 0 + 20;
            public const int offset_ScavengersPerEnvironment = 0 + 20 + 20;
            public const int offset_ScannersPerEnvironment = 0 + 20 + 20 + 1;
            public const int offset_AltarsPerEnvironment = 0 + 20 + 20 + 1 + 1;
            public const int allocation = 0 + 20 + 20 + 1 + 1 + 2;
        }

        private ArchipelagoSession session;
        private LocationInformationTemplate originallocationstemplate;
        private Dictionary<int, LocationInformationTemplate> currentlocations;

        public LocationHandler(ArchipelagoSession session, LocationInformationTemplate locationstemplate)
        {
            Log.LogDebug($"Location handler constructor.");
            this.session = session;
            originallocationstemplate = locationstemplate;
            currentlocations = new Dictionary<int, LocationInformationTemplate>();


            itemSatisfiesLocation = new Queue<bool>();

            InitialSetupLocationDict(locationstemplate);

            CatchUpLocationDict();
        }

        /// <summary>
        /// Calling adds the location template to each environment so they can be individually tracked later.
        /// </summary>
        /// <param name="locationstemplate">Template to assign to all relevant environments.</param>
        // TODO this should probably become generic so that environment sets can be passed in (e.g. normal environments, simulacrum environments, etc)
        private void InitialSetupLocationDict(LocationInformationTemplate locationstemplate)
        {
            currentlocations.Add(ancientloft,       locationstemplate); // Aphelian Sanctuary
            currentlocations.Add(blackbeach,        locationstemplate); // Distant Roost
            currentlocations.Add(blackbeach2,       locationstemplate); // Distant Roost
            currentlocations.Add(dampcavesimple,    locationstemplate); // Abyssal Depths
            currentlocations.Add(foggyswamp,        locationstemplate); // Wetland Aspect
            currentlocations.Add(frozenwall,        locationstemplate); // Rallypoint Delta
            currentlocations.Add(golemplains,       locationstemplate); // Titanic Plains
            currentlocations.Add(golemplains2,      locationstemplate); // Titanic Plains
            currentlocations.Add(goolake,           locationstemplate); // Abandoned Aqueduct
            currentlocations.Add(rootjungle,        locationstemplate); // Sundered Grove
            currentlocations.Add(shipgraveyard,     locationstemplate); // Siren's Call
            currentlocations.Add(skymeadow,         locationstemplate); // Sky Meadow
            currentlocations.Add(snowyforest,       locationstemplate); // Siphoned Forest
            currentlocations.Add(sulfurpools,       locationstemplate); // Sulfur Pools
            currentlocations.Add(wispgraveyard,     locationstemplate); // Scorched Acres
        }

        /// <summary>
        /// This is used to have the location handler catch up to the archipelago session.
        /// This is because the player may have completed checks, died, and restarted the session and we do not need to have the player repeat checks.
        /// </summary>
        private void CatchUpLocationDict()
        {
            Log.LogDebug("CatchUpLocationDict"); // XXX
            ReadOnlyCollection<long> completedchecks = session.Locations.AllLocationsChecked;

            // TODO time complexity probably doesn't matter here, but there probably is a more efficient way to do this
            // a probably better way to do it would be iterate over all completed chests, skip numbers which don't relate to ror2, and change the values per number
            // instead of the converse of checking the numbers for every possible check

            // a copy is needed because the one being enumerated over cannot be changed
            Dictionary<int, LocationInformationTemplate> locationscopy = new Dictionary<int, LocationInformationTemplate>(currentlocations);

            foreach (KeyValuePair<int, LocationInformationTemplate> kvp in locationscopy)
            {
                int index = kvp.Key;
                LocationInformationTemplate location = kvp.Value;
                int environment_start_id = index*ArchipelagoLocationOffsets.allocation + ArchipelagoLocationOffsets.ror2_locations_start_orderedstage;
                Log.LogDebug($"index {index}"); // XXX
                Log.LogDebug($"environment_start_id {environment_start_id}"); // XXX

                // catch up chests
                for (int n=0; n < originallocationstemplate.chest_count; n++)
                {
                    Log.LogDebug($"catch up chest {n}"); // XXX
                    // check each chest if it has been seen
                    if (completedchecks.Contains(n + ArchipelagoLocationOffsets.offset_ChestsPerEnvironment + environment_start_id))
                    {
                        Log.LogDebug($"chest {n} is complete"); // XXX
                        location.chest_count--; // a completed chest for this environment has been found
                    }
                    // if we see a chest missing, imply the ones that succeed it are also missing
                    else break;
                }

                // XXX handle matching the locations to with what archipelago has

                currentlocations[index] = location;
            }
        }

        public void Hook()
        {
            // Chests
            On.RoR2.ChestBehavior.ItemDrop += ChestBehavior_ItemDrop;
            On.RoR2.PickupDropletController.CreatePickupDroplet_PickupIndex_Vector3_Vector3 += PickupDropletController_CreatePickupDroplet;
            // Shrines
            // Scavengers
            // Radio Scanners
            On.RoR2.SceneDirector.PopulateScene += SceneDirector_PopulateScene;
            On.RoR2.RadiotowerTerminal.GrantUnlock += RadiotowerTerminal_GrantUnlock;
            // Newt Altars
            On.RoR2.PortalStatueBehavior.GrantPortalEntry += PortalStatueBehavior_GrantPortalEntry;
        }

        public void UnHook()
        {
            // Chests
            On.RoR2.ChestBehavior.ItemDrop -= ChestBehavior_ItemDrop;
            On.RoR2.PickupDropletController.CreatePickupDroplet_PickupIndex_Vector3_Vector3 -= PickupDropletController_CreatePickupDroplet;
            // Shrines
            // Scavengers
            // Radio Scanners
            On.RoR2.SceneDirector.PopulateScene -= SceneDirector_PopulateScene;
            On.RoR2.RadiotowerTerminal.GrantUnlock -= RadiotowerTerminal_GrantUnlock;
            // Newt Altars
            On.RoR2.PortalStatueBehavior.GrantPortalEntry -= PortalStatueBehavior_GrantPortalEntry;
        }

        private uint chestitemsPickedUp = 0; // is used to count the number of items
        // XXX get this in from the YAML
        private uint itemPickupStep = 2; // is set to the interval between archipelago locations from chest-like objects
        private Queue<bool> itemSatisfiesLocation; // used to figure out which items should complete locations

        /// <summary>
        /// Resets all overhead variables that should be reinitialized when entering a new environment.
        /// </summary>
        public void ResetStageSpecific()
        // XXX call this so that items to drop cannot be carried over between stages
        {
            chestitemsPickedUp = 0;
            itemSatisfiesLocation.Clear();
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Chest like objects

        // To not have to write IL code, some weird hooks will be used.
        // The idea is to count the number of items that will be spawned and then intercept them as they are spawning
        //  to prevent only consume items we want to use as locations.

        private void ChestBehavior_ItemDrop(On.RoR2.ChestBehavior.orig_ItemDrop orig, RoR2.ChestBehavior self)
        {
            Log.LogDebug("ChestBehavior_ItemDrop"); // XXX
            if(NetworkServer.active && self.dropPickup != PickupIndex.none && self.dropCount >= 1)
            {
                int currentenvironment = (int)SceneCatalog.mostRecentSceneDef.sceneDefIndex;
                LocationInformationTemplate locationsinenvironment = currentlocations[currentenvironment];

                Log.LogDebug($"environment {currentenvironment} has {locationsinenvironment.chest_count} remaining"); // XXX
                if (locationsinenvironment.chest_count>0)
                {
                    for (int i=self.dropCount; i>0; i--)
                    {
                        chestitemsPickedUp++;
                        itemSatisfiesLocation.Enqueue(0 == chestitemsPickedUp%itemPickupStep);
                    }
                }
            }

            orig(self); // the original will end up calling PickupDropletController_CreatePickupDroplet
        }

        private void PickupDropletController_CreatePickupDroplet(On.RoR2.PickupDropletController.orig_CreatePickupDroplet_PickupIndex_Vector3_Vector3 orig, RoR2.PickupIndex pickupIndex, UnityEngine.Vector3 position, UnityEngine.Vector3 velocity)
        {
            Log.LogDebug("PickupDropletController_CreatePickupDroplet"); // XXX
            // XXX is from the item blacklist Ijwu created is important to impose here?

            Log.LogDebug($"itemSatisfiesLocation.Count {itemSatisfiesLocation.Count}"); // XXX

            // check if the item being dropped satisfies the chest requirement
            if (itemSatisfiesLocation.Count > 0 && itemSatisfiesLocation.Dequeue())
            {
                Log.LogDebug("satisfied"); // XXX
                int currentenvironment = (int)SceneCatalog.mostRecentSceneDef.sceneDefIndex;
                LocationInformationTemplate locationsinenvironment = currentlocations[currentenvironment];

                int environment_start_id = currentenvironment*ArchipelagoLocationOffsets.allocation + ArchipelagoLocationOffsets.ror2_locations_start_orderedstage;
                int chest_number = originallocationstemplate.chest_count - locationsinenvironment.chest_count;
                locationsinenvironment.chest_count--;

                LocationChecksPacket packet = new LocationChecksPacket();
                packet.Locations = new List<long> { chest_number + ArchipelagoLocationOffsets.offset_ChestsPerEnvironment + environment_start_id }.ToArray();
                Log.LogDebug($"planning to send location {packet.Locations[0]}"); // XXX
                // why synchronous? that's how Ijwu had done it before, unsure of the specific reasoning:
                // https://github.com/Ijwu/Archipelago.RiskOfRain2/blob/4318f37e7aa3fea258830de0d08a41014b19228b/Archipelago.RiskOfRain2/ArchipelagoItemLogicController.cs#L311
                session.Socket.SendPacket(packet);

                currentlocations[currentenvironment] = locationsinenvironment; // save the changes to the locations

                // TODO maybe items should also go to the player?
                if (true) return;
            }
            orig(pickupIndex, position, velocity);
        }
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Shrine like objects

        // All shrines behave differently and there is no inheritance to a common shrine object
        // Therefore all shrine types will have to be handled differently.

        // XXX Gold
        // XXX Blood
        // XXX Chance
        // XXX Combat
        // XXX Order
        // XXX Mountain
        // XXX Woods

        ////////////////////////////////////////////////////////////////////////////////////////////////////

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Scavenger

        // Scavengers will be counted by the number of bags opened.

        // XXX implement scavbackpak uses ChestBehavior_ItemDrop

        ////////////////////////////////////////////////////////////////////////////////////////////////////

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Radio scanner

        // Radio scanners will need to be forcefully spawned even if the player has purchased them
        //  otherwise the check would be impossible to complete.

        private void SceneDirector_PopulateScene(On.RoR2.SceneDirector.orig_PopulateScene orig, SceneDirector self)
        {
            Log.LogDebug("SceneDirector_PopulateScene"); // XXX
            orig(self); // let the director do it's own thing first as to not get in the way

            int currentenvironment = (int)SceneCatalog.mostRecentSceneDef.sceneDefIndex;
            LocationInformationTemplate locationsinenvironment = currentlocations[currentenvironment];

            if (locationsinenvironment.radio_scanner_count > 0)
            // we always want to always spawn a radio scanner if it is a location
            {
                Log.LogDebug("attempt to spawn scanner"); // XXX

                // the format for spawning is stolen directly from how rusty/lock boxes are spawned
                Xoroshiro128Plus xoroshiro128PlusRadioScanner = new Xoroshiro128Plus(self.rng.nextUlong);
                DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(LegacyResourcesAPI.Load<SpawnCard>("SpawnCards/InteractableSpawnCard/iscRadarTower"), new DirectorPlacementRule
                {
                    placementMode = DirectorPlacementRule.PlacementMode.Random
                }, xoroshiro128PlusRadioScanner));
            }
        }

        private void RadiotowerTerminal_GrantUnlock(On.RoR2.RadiotowerTerminal.orig_GrantUnlock orig, RadiotowerTerminal self, Interactor interactor)
        {
            Log.LogDebug("RadiotowerTerminal_GrantUnlock"); // XXX
            int currentenvironment = (int)SceneCatalog.mostRecentSceneDef.sceneDefIndex;
            LocationInformationTemplate locationsinenvironment = currentlocations[currentenvironment];

            if (locationsinenvironment.radio_scanner_count <= 0)
            {
                orig(self, interactor);
                return;
            }

           int environment_start_id = currentenvironment*ArchipelagoLocationOffsets.allocation + ArchipelagoLocationOffsets.ror2_locations_start_orderedstage;
           int radio_number = originallocationstemplate.radio_scanner_count - locationsinenvironment.radio_scanner_count;
           locationsinenvironment.radio_scanner_count--;

           LocationChecksPacket packet = new LocationChecksPacket();
           packet.Locations = new List<long> { radio_number + ArchipelagoLocationOffsets.offset_ScannersPerEnvironment + environment_start_id }.ToArray();
           Log.LogDebug($"planning to send location {packet.Locations[0]}"); // XXX
           // why synchronous? that's how Ijwu had done it before, unsure of the specific reasoning:
           // https://github.com/Ijwu/Archipelago.RiskOfRain2/blob/4318f37e7aa3fea258830de0d08a41014b19228b/Archipelago.RiskOfRain2/ArchipelagoItemLogicController.cs#L311
           session.Socket.SendPacket(packet);
           Log.LogDebug($"environment {currentenvironment} now has {locationsinenvironment.radio_scanner_count} remaining"); // XXX

           currentlocations[currentenvironment] = locationsinenvironment; // save the changes to the locations

            // still play the effect for the scanner and lock it from being used again
            EffectManager.SpawnEffect(self.unlockEffect, new EffectData
            {
                origin = self.transform.position
            }, transmit: true);
            self.SetHasBeenPurchased(newHasBeenPurchased: true);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Newt Altars

        private void PortalStatueBehavior_GrantPortalEntry(On.RoR2.PortalStatueBehavior.orig_GrantPortalEntry orig, PortalStatueBehavior self)
        {
            Log.LogDebug("PortalStatueBehavior_GrantPortalEntry"); // XXX

            if (self.portalType == PortalStatueBehavior.PortalType.Shop)
            {
                Log.LogDebug("altar used ie blue portal"); // XXX

                int currentenvironment = (int)SceneCatalog.mostRecentSceneDef.sceneDefIndex;
                LocationInformationTemplate locationsinenvironment = currentlocations[currentenvironment];

                if (locationsinenvironment.newt_alter_count <= 0)
                {
                    orig(self); // if there are no checks to be sent, nothing needs to be done
                    return;
                }

                int environment_start_id = currentenvironment*ArchipelagoLocationOffsets.allocation + ArchipelagoLocationOffsets.ror2_locations_start_orderedstage;
                int altar_number = originallocationstemplate.newt_alter_count - locationsinenvironment.newt_alter_count;
                locationsinenvironment.newt_alter_count--;

                LocationChecksPacket packet = new LocationChecksPacket();
                packet.Locations = new List<long> { altar_number + ArchipelagoLocationOffsets.offset_AltarsPerEnvironment + environment_start_id }.ToArray();
                Log.LogDebug($"planning to send location {packet.Locations[0]}"); // XXX
                // why synchronous? that's how Ijwu had done it before, unsure of the specific reasoning:
                // https://github.com/Ijwu/Archipelago.RiskOfRain2/blob/4318f37e7aa3fea258830de0d08a41014b19228b/Archipelago.RiskOfRain2/ArchipelagoItemLogicController.cs#L311
                session.Socket.SendPacket(packet);
                Log.LogDebug($"environment {currentenvironment} now has {locationsinenvironment.newt_alter_count} remaining"); // XXX

                currentlocations[currentenvironment] = locationsinenvironment; // save the changes to the locations

                // TODO do I want to block the other altars from being used? ie only one altar use per stage

                return; // don't run the original as we do not want to spawn the portal effect when we are going to stop the portal from spawning
            }

            orig(self);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////

    }
}

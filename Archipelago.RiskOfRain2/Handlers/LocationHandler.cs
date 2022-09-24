using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using RoR2;
using System;
using System.Collections.Generic;
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
            // XXX use this
            public int shrine_count { get; set; }
            // XXX use this
            public int radio_scanner_count { get; set; }
            // XXX use this
            public int newt_alter_count { get; set; }
        }

        // XXX write a comment summary about this
        private readonly struct ArchipelagoLocationOffsets
        {
            // these values come from worlds/ror2/Locations.py in Archipelago
            public const int ror2_locations_start_orderedstage = 38000 + 250;
            public const int offset_ChestsPerEnvironment = 0;
            public const int offset_ShrinesPerEnvironment = 20;
            public const int offset_ScavengersPerEnvironment = 40;
            public const int offset_ScannersPerEnvironment = 41;
            public const int offset_AltarsPerEnvironment = 42;
            public const int allocation = 44;
        }

        private ArchipelagoSession session;
        private LocationInformationTemplate originallocationstemplate;
        private Dictionary<int, LocationInformationTemplate> currentlocations;

        // XXX I need to instantiate this so the locations are actually handled
        public LocationHandler(ArchipelagoSession session, LocationInformationTemplate locationstemplate)
        {
            this.session = session;
            originallocationstemplate = locationstemplate;
            currentlocations = new Dictionary<int, LocationInformationTemplate>();

            InitialSetupLocationDict(locationstemplate);

            // XXX somehow need to determine the already completed locations
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

        public void Hook()
        {
            On.RoR2.ChestBehavior.ItemDrop += ChestBehavior_ItemDrop;
            On.RoR2.PickupDropletController.CreatePickupDroplet_PickupIndex_Vector3_Vector3 += PickupDropletController_CreatePickupDroplet;
        }

        public void UnHook()
        {
            On.RoR2.ChestBehavior.ItemDrop -= ChestBehavior_ItemDrop;
            On.RoR2.PickupDropletController.CreatePickupDroplet_PickupIndex_Vector3_Vector3 -= PickupDropletController_CreatePickupDroplet;
        }

        /// <summary>
        private uint chestitemsPickedUp = 0; // is used to count the number of items
        private uint itemPickupStep = 2; // is set to the interval between archipelago locations from chest-like objects
        // TODO get this in from the YAML
        private Queue<bool> itemSatisfiesLocation; // used to figure out which items should complete locations

        /// Resets all overhead variables that should be reinitialized when entering a new environment.
        /// </summary>
        public void ResetStageSpecific()
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
            if(NetworkServer.active && !(self.dropPickup == PickupIndex.none) && self.dropCount >= 1)
            {
                int currentenvironment = (int)SceneCatalog.mostRecentSceneDef.sceneDefIndex;
                LocationInformationTemplate locationsinenvironment = currentlocations[currentenvironment];

                if (locationsinenvironment.chest_count>0)
                {
                    for (int i=self.dropCount; i>0; i--)
                    {
                        chestitemsPickedUp++;
                        itemSatisfiesLocation.Enqueue(0 == chestitemsPickedUp%itemPickupStep);
                    }
                }
            }

            orig(self);
        }

        private void PickupDropletController_CreatePickupDroplet(On.RoR2.PickupDropletController.orig_CreatePickupDroplet_PickupIndex_Vector3_Vector3 orig, RoR2.PickupIndex pickupIndex, UnityEngine.Vector3 position, UnityEngine.Vector3 velocity)
        {
            // check if the item being dropped satisfies the chest requirement
            if (itemSatisfiesLocation.Count > 0 && itemSatisfiesLocation.Dequeue())
            {
                int currentenvironment = (int)SceneCatalog.mostRecentSceneDef.sceneDefIndex;
                LocationInformationTemplate locationsinenvironment = currentlocations[currentenvironment];

                int environment_start_id = currentenvironment*ArchipelagoLocationOffsets.allocation + ArchipelagoLocationOffsets.ror2_locations_start_orderedstage;
                int chest_number = originallocationstemplate.chest_count - locationsinenvironment.chest_count;
                locationsinenvironment.chest_count--;

                LocationChecksPacket packet = new LocationChecksPacket();
                packet.Locations = new List<long> { chest_number + ArchipelagoLocationOffsets.offset_ChestsPerEnvironment + environment_start_id }.ToArray();
                // why synchronous? that's how Ijwu had done it before:
                // https://github.com/Ijwu/Archipelago.RiskOfRain2/blob/4318f37e7aa3fea258830de0d08a41014b19228b/Archipelago.RiskOfRain2/ArchipelagoItemLogicController.cs#L311
                session.Socket.SendPacket(packet);

                // TODO maybe items should also go to the player?
                if (true) return;
            }
            orig(pickupIndex, position, velocity);
        }
        ////////////////////////////////////////////////////////////////////////////////////////////////////

    }
}

using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.RiskOfRain2.UI;
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


        public enum LocationTypes
        {
            chest,
            shrine,
            scavenger,
            radio_scanner,
            newt_altar,
            // NOTE add additional location types above this comment
            MAX // used to sent the length of LocationInformationTemplates
        }

        public static readonly string[] LocationTypesSlotName = new string[(int)LocationTypes.MAX] // use max to enforce correct amount of names
        {
            // These names should match those in the slot data
            "chests_per_stage",
            "shrines_per_stage",
            "scavengers_per_stage",
            "scanner_per_stage",
            "altars_per_stage"
        };

        public static readonly string[] LocationTypesShortName = new string[(int)LocationTypes.MAX] // use max to enforce correct amount of names
        {
            // These names are used for debug
            "chests",
            "shrines",
            "scavengers",
            "scanner",
            "altars"
        };

        /// <summary>
        /// These values are sourced from the RoR2 Archipelago world code.
        /// These are used to determine the id values of locations.
        /// </summary>
        private class ArchipelagoLocationOffsets
        {
            // these values come from worlds/ror2/Locations.py in Archipelago
            public const int ror2_locations_start_orderedstage = 38000 + 250;
            public static readonly int[] offset = new int[(int)LocationTypes.MAX + 1] // use max+1 to enforce correct amount of offsets
            {
                0,
                0 + 20,
                0 + 20 + 20,
                0 + 20 + 20 + 1,
                0 + 20 + 20 + 1 + 1,
                0 + 20 + 20 + 1 + 1 + 2
            };
            // NOTE offset[(int)LocationTypes.MAX] will give the size allocated to locations in each environment
            public static readonly int allocation = offset[(int)LocationTypes.MAX];
        }

        // Create a class to interface the template information.
        // This is so readabilty and the ability to index the template with the LocationTypes enum.
        public class LocationInformationTemplate
        {
            private int[] data = new int[(int)LocationTypes.MAX];

            public int this[int i]
            {
                get => data[i];
                set => data[i] = value;
            }

            public int this[LocationTypes type]
            {
                get => data[(int)type];
                set => data[(int)type] = value;
            }

            /// <returns>The sum of all locations in the template.</returns>
            public int total()
            {
                int sum = 0;
                for (int type = 0; type < (int)LocationTypes.MAX; type++) sum += data[type];
                return sum;
            }

            public LocationInformationTemplate copy()
            {
                LocationInformationTemplate copy = new LocationInformationTemplate();
                for (int type = 0; type < (int)LocationTypes.MAX; type++) copy[type] = data[type];
                return copy;
            }
        }


        public static LocationInformationTemplate buildTemplateFromSlotData(Dictionary<string, object> SlotData)
        {
            LocationInformationTemplate locationtemplate = new LocationInformationTemplate();
            if (SlotData is not null)
            {
                // construct the find the amount of each type of location dictated by the slot data
                for (int type = 0; type < (int)LocationTypes.MAX; type++)
                {
                    // only set the value if the slot has the amoutn of locations for that type
                    if (SlotData.TryGetValue(LocationTypesSlotName[type], out var type_per_stage)) locationtemplate[type] = Convert.ToInt32(type_per_stage);
                }
            }
            return locationtemplate;
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
            // TODO separate out the DLC locations
        }

        /// <summary>
        /// This is used to have the location handler catch up to the archipelago session.
        /// This is because the player may have completed checks, died, and restarted the session and we do not need to have the player repeat checks.
        /// </summary>
        private void CatchUpLocationDict()
        {
            Log.LogDebug("CatchUpLocationDict");
            ReadOnlyCollection<long> completedchecks = session.Locations.AllLocationsChecked;

            // TODO time complexity probably doesn't matter here, but there probably is a more efficient way to do this
            // a probably better way to do it would be iterate over all completed chests, skip numbers which don't relate to ror2, and change the values per number
            // instead of the converse of checking the numbers for every possible check

            // a copy is needed because the one being enumerated over cannot be changed
            Dictionary<int, LocationInformationTemplate> locationscopy = new Dictionary<int, LocationInformationTemplate>(currentlocations);

            foreach (KeyValuePair<int, LocationInformationTemplate> kvp in locationscopy)
            {
                int index = kvp.Key;
                LocationInformationTemplate location = kvp.Value.copy(); // because the value is an object in both Dictionaries, we want a copy instead of the reference
                int environment_start_id = index*ArchipelagoLocationOffsets.allocation + ArchipelagoLocationOffsets.ror2_locations_start_orderedstage;
                Log.LogDebug($"Doing catch up on environment: index {index}");
                Log.LogDebug($"environment_start_id {environment_start_id}");

                // catch each individual check type up
                for (int type = 0; type < (int)LocationTypes.MAX; type++)
                {

                    for (int n = 0; n < originallocationstemplate[type]; n++)
                    {
                        // check each location if it has been seen
                        if (completedchecks.Contains(n + ArchipelagoLocationOffsets.offset[type] + environment_start_id))
                        {
                            location[type]--; // a location completed has been found for this environment
                        }
                        // if we see a location missing, imply the ones that succeed it are also missing
                        else break;
                    }
                    Log.LogDebug($"caught up to {LocationTypesShortName[type]} {location[type]}");
                }

                currentlocations[(int)index] = location;
            }
        }

        public void Hook()
        {
            // Etc
            On.RoR2.SceneCatalog.OnActiveSceneChanged += SceneCatalog_OnActiveSceneChanged;
            On.RoR2.SceneCollection.AddToWeightedSelection += SceneCollection_AddToWeightedSelection;
            // Chests
            On.RoR2.ChestBehavior.ItemDrop += ChestBehavior_ItemDrop_Chest;
            On.RoR2.Artifacts.SacrificeArtifactManager.OnServerCharacterDeath += SacrificeArtifactManager_OnServerCharacterDeath;
            On.RoR2.PickupDropletController.CreatePickupDroplet_PickupIndex_Vector3_Vector3 += PickupDropletController_CreatePickupDroplet_Chest;
            // Shrines
            On.RoR2.PortalStatueBehavior.GrantPortalEntry += PortalStatueBehavior_GrantPortalEntry_Gold;
            On.RoR2.ShrineBloodBehavior.AddShrineStack += ShrineBloodBehavior_AddShrineStack;
            On.RoR2.CharacterMaster.GiveMoney += CharacterMaster_GiveMoney;
            On.RoR2.ShrineChanceBehavior.AddShrineStack += ShrineChanceBehavior_AddShrineStack;
            On.RoR2.PickupDropletController.CreatePickupDroplet_PickupIndex_Vector3_Vector3 += PickupDropletController_CreatePickupDroplet_ChanceShrine;
            On.RoR2.ShrineCombatBehavior.AddShrineStack += ShrineCombatBehavior_AddShrineStack;
            On.RoR2.ShrineRestackBehavior.AddShrineStack += ShrineRestackBehavior_AddShrineStack;
            On.RoR2.BossGroup.DropRewards += BossGroup_DropRewards;
            On.RoR2.ShrineHealingBehavior.AddShrineStack += ShrineHealingBehavior_AddShrineStack;
            // Scavengers
            On.EntityStates.ScavBackpack.Opening.OnEnter += Opening_OnEnter;
            On.RoR2.ChestBehavior.ItemDrop += ChestBehavior_ItemDrop_Scavenger;
            On.RoR2.PickupDropletController.CreatePickupDroplet_PickupIndex_Vector3_Vector3 += PickupDropletController_CreatePickupDroplet_Scavenger;
            // Radio Scanners
            On.RoR2.SceneDirector.PopulateScene += SceneDirector_PopulateScene;
            On.RoR2.RadiotowerTerminal.GrantUnlock += RadiotowerTerminal_GrantUnlock;
            // Newt Altars
            On.RoR2.PortalStatueBehavior.GrantPortalEntry += PortalStatueBehavior_GrantPortalEntry_Blue;
        }

        public void UnHook()
        {
            // Etc
            On.RoR2.SceneCatalog.OnActiveSceneChanged -= SceneCatalog_OnActiveSceneChanged;
            On.RoR2.SceneCollection.AddToWeightedSelection -= SceneCollection_AddToWeightedSelection;
            // Chests
            On.RoR2.ChestBehavior.ItemDrop -= ChestBehavior_ItemDrop_Chest;
            On.RoR2.Artifacts.SacrificeArtifactManager.OnServerCharacterDeath -= SacrificeArtifactManager_OnServerCharacterDeath;
            On.RoR2.PickupDropletController.CreatePickupDroplet_PickupIndex_Vector3_Vector3 -= PickupDropletController_CreatePickupDroplet_Chest;
            // Shrines
            On.RoR2.PortalStatueBehavior.GrantPortalEntry -= PortalStatueBehavior_GrantPortalEntry_Gold;
            On.RoR2.ShrineBloodBehavior.AddShrineStack -= ShrineBloodBehavior_AddShrineStack;
            On.RoR2.CharacterMaster.GiveMoney -= CharacterMaster_GiveMoney;
            On.RoR2.ShrineChanceBehavior.AddShrineStack -= ShrineChanceBehavior_AddShrineStack;
            On.RoR2.PickupDropletController.CreatePickupDroplet_PickupIndex_Vector3_Vector3 -= PickupDropletController_CreatePickupDroplet_ChanceShrine;
            On.RoR2.ShrineCombatBehavior.AddShrineStack -= ShrineCombatBehavior_AddShrineStack;
            On.RoR2.ShrineRestackBehavior.AddShrineStack -= ShrineRestackBehavior_AddShrineStack;
            On.RoR2.BossGroup.DropRewards -= BossGroup_DropRewards;
            On.RoR2.ShrineHealingBehavior.AddShrineStack -= ShrineHealingBehavior_AddShrineStack;
            // Scavengers
            On.EntityStates.ScavBackpack.Opening.OnEnter -= Opening_OnEnter;
            On.RoR2.ChestBehavior.ItemDrop -= ChestBehavior_ItemDrop_Scavenger;
            On.RoR2.PickupDropletController.CreatePickupDroplet_PickupIndex_Vector3_Vector3 -= PickupDropletController_CreatePickupDroplet_Scavenger;
            // Radio Scanners
            On.RoR2.SceneDirector.PopulateScene -= SceneDirector_PopulateScene;
            On.RoR2.RadiotowerTerminal.GrantUnlock -= RadiotowerTerminal_GrantUnlock;
            // Newt Altars
            On.RoR2.PortalStatueBehavior.GrantPortalEntry -= PortalStatueBehavior_GrantPortalEntry_Blue;
        }

        public ArchipelagoLocationCheckProgressBarUI itemBar = null;
        public ArchipelagoLocationCheckProgressBarUI shrineBar = null;

        // NOTE the counters are not used to store the actual count, they used for detecting when to send locations
        private uint chestitemsPickedUp = 0; // is used to count the number of items
        private uint shrinesUsed = 0; // is used to count the number of items

        public uint itemPickupStep = 3; // is the interval at which archipelago locations are sent from chest-like objects; 1 is every, 2 is every other, etc
        public uint shrineUseStep = 3; // is the interval at which archipelago locations are sent from shrine objects; 1 is every, 2 is every other, etc

        private bool chestblockitem = false; // used to keep track of when the chest's item(s) are blocked as a location check
        private bool sacrificeitem = false; // used to keep track of when an item is being dropped by the sacrifice artifiact
        private bool chanceshrineblockitem = false; // used to keep track of when the blood shrine is attempting to give gold so the gold can be blocked
        private bool chanceshrinebeat = false; // used to keep track of if the chance shrine intended on rewarding a check
        private bool bloodshrineblockgold = false; // used to keep track of when the blood shrine is attempting to give gold so the gold can be blocked
        private int scavbackpackHash = 0; // used to keep track of which chest is the scavenger backpack
        private bool scavbackpackWasLocation = false; // used to track if the scavenger backpack that was opened was used as a location
        private bool scavbackpackblockitem = false; // used to keep track of when the scavenger backpack's items are blocked from a location check

        private void updateBar(LocationTypes loctype)
        {
            ArchipelagoLocationCheckProgressBarUI bar = null;
            int amount = 0;
            int step = 1;
            switch (loctype)
            {
                case LocationTypes.chest:
                    bar = itemBar;
                    amount = (int) chestitemsPickedUp;
                    step = (int) itemPickupStep;
                    break;
                case LocationTypes.shrine:
                    bar = shrineBar;
                    amount = (int) shrinesUsed;
                    step = (int) shrineUseStep;
                    break;
            }

            if (null != bar)
            {
                bar.UpdateCheckProgress(amount % step, step);
                // use the default color with checks, use the alt color when out of checks
                bar.ChangeBarColor(0 < checkAvailable(loctype) ? ArchipelagoLocationCheckProgressBarUI.defaultColor : ArchipelagoLocationCheckProgressBarUI.altColor);
            }
        }

        private void sendLocation(int id)
        {
            LocationChecksPacket packet = new LocationChecksPacket();
            packet.Locations = new List<long> { id }.ToArray();
            Log.LogDebug($"planning to send location {id}"); // XXX
            // why synchronous? that's how Ijwu had done it before, unsure of the specific reasoning:
            // https://github.com/Ijwu/Archipelago.RiskOfRain2/blob/4318f37e7aa3fea258830de0d08a41014b19228b/Archipelago.RiskOfRain2/ArchipelagoItemLogicController.cs#L311
            session.Socket.SendPacket(packet);
        }

        /// <summary>
        /// Checks the remaing checks of a specific type in the current environment. <br/>
        /// If the type given is LocationTypes.MAX, the total of all locations remaining will be returned.
        /// </summary>
        /// <param name="loctype">The type of location to check.</param>
        /// <returns>Returns the amount of remaining locations.</returns>
        private int checkAvailable(LocationTypes loctype) // TODO make a method to check the nth location
        {
            int currentenvironment = (int)SceneCatalog.mostRecentSceneDef.sceneDefIndex;
            if (!currentlocations.TryGetValue((int)currentenvironment, out var locationsinenvironment))
            // prevent KeyNotFoundException by using TryGetValue
            {
                // if the locations in the environment are not being tracked, there must be 0 locations
                return 0;
            }

            if (LocationTypes.MAX == loctype)
            {
                return locationsinenvironment.total();
            }
            return locationsinenvironment[loctype];
        }

        /// <summary>
        /// Send the next available location for the current environment of that specified type.
        /// </summary>
        /// <remarks>
        /// NOTE this does not account for pickup steps.
        /// </remarks>
        /// <param name="loctype">The type of location to send.</param>
        /// <returns>
        /// Returns true if a location send attempt was made.
        /// (Sending a location who's item has been collected will still return true.)
        /// </returns>
        private bool sendNextAvailable(LocationTypes loctype) // TODO make a method to send the nth location
        {
            if (LocationTypes.MAX == loctype) throw new ArgumentException("MAX is not a sendable location type.");

            int currentenvironment = (int)SceneCatalog.mostRecentSceneDef.sceneDefIndex;
            if (!currentlocations.TryGetValue((int)currentenvironment, out var locationsinenvironment))
            // prevent KeyNotFoundException by using TryGetValue
            {
                // if the locations in the environment that are not being tracked, then there is no check to send
                return false;
            }

            int environment_start_id = currentenvironment * ArchipelagoLocationOffsets.allocation + ArchipelagoLocationOffsets.ror2_locations_start_orderedstage;

            // check if there is a check to be done
            // if there are none, then return false
            if (locationsinenvironment[loctype] == 0) return false;

            int next_index = originallocationstemplate[loctype] - locationsinenvironment[loctype];
            int offset_in_allocation = ArchipelagoLocationOffsets.offset[(int)loctype];
            locationsinenvironment[loctype]--;
            ArchipelagoLocationsInEnvironmentController.count[loctype] = locationsinenvironment[loctype];

            // update UI to the results of sending the location
            ArchipelagoTotalChecksObjectiveController.CurrentChecks++;
            if (0 == ArchipelagoLocationsInEnvironmentController.count.total()) ArchipelagoLocationsInEnvironmentController.RemoveObjective();
            else ArchipelagoLocationsInEnvironmentController.AddObjective();

            currentlocations[(int)currentenvironment] = locationsinenvironment; // save changes to the count

            sendLocation(next_index + offset_in_allocation + environment_start_id);

            return true; // a location must have been sent
            // (don't care if the item for said location has already be collected)
            // (don't care if the location has been sent before, though it shouldn't happen if everything is working)

        }

        /// <summary>
        /// Resets all overhead variables that should be reinitialized when entering a new environment.
        /// </summary>
        private void SceneCatalog_OnActiveSceneChanged(On.RoR2.SceneCatalog.orig_OnActiveSceneChanged orig, UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
        {
            orig(oldScene, newScene);
            // We want to hook directly to SceneCatalog_OnActiveSceneChanged rather than delegate
            //  to SceneCatalog_OnActiveSceneChanged so that we can take advantage of the changed mostRecentSceneDef.
            Log.LogDebug($"sceneDefIndex {(int)SceneCatalog.mostRecentSceneDef.sceneDefIndex}");

            // don't reset the counters on moving between stages
            // this could make it absurdly hard to complete checks on very high step sizes
            //chestitemsPickedUp = 0;
            //shrinesUsed = 0;

            // reset the values in case the shrine was somehow busy when the stage changed
            chestblockitem = false;
            sacrificeitem = false;
            chanceshrineblockitem = false;
            chanceshrinebeat = false;
            bloodshrineblockgold = false;
            scavbackpackHash = 0;
            scavbackpackWasLocation = false;
            scavbackpackblockitem = false;

            // update the bars for the new scene
            updateBar(LocationTypes.chest);
            updateBar(LocationTypes.shrine);

            // update the UI to match the new environment
            for (int type = 0; type < (int)LocationTypes.MAX; type++)
            {
                ArchipelagoLocationsInEnvironmentController.count[type] = checkAvailable((LocationTypes)type);
            }
            if (0 == ArchipelagoLocationsInEnvironmentController.count.total()) ArchipelagoLocationsInEnvironmentController.RemoveObjective();
            else ArchipelagoLocationsInEnvironmentController.AddObjective();

            // TODO maybe the make sure the ArchipelagoTotalChecksObjectiveController.CurrentChecks gets synced here (since sending a location increments it and could possibly desync it?)
        }

        private void SceneCollection_AddToWeightedSelection(On.RoR2.SceneCollection.orig_AddToWeightedSelection orig, SceneCollection self, WeightedSelection<SceneDef> dest, Func<SceneDef, bool> canAdd)
        {
            // In explore mode we will give help the player a little by adjusting the RNG to favor locations where checks need to still be performed.
            // This should help the player not get stuck in an RNG hell where they simply cannot roll into the stages they need to go to to complte things.

            orig(self, dest, canAdd);
            if (null == dest) return; // prevent NRE
            for (int i=0; i < dest.Count; i++)
            {
                // add 1 weight to per location left in an environment
                int environment_index = (int) dest.choices[i].value.sceneDefIndex;
                if (currentlocations.TryGetValue(environment_index, out var locations))
                {
                    int addweight = locations.total();
                    Log.LogDebug($"Environment {environment_index} with weight {dest.choices[i].weight} has {addweight} locations, adjusting weight.");
                    dest.ModifyChoiceWeight(i, dest.choices[i].weight + addweight);
                    Log.LogDebug($"Adjusted weight to {dest.choices[i].weight}.");
                }
                else Log.LogDebug($"Environment {environment_index} with weight {dest.choices[i].weight} does not have locations.");
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Chest like objects

        // To not have to write IL code, some weird hooks will be used.
        // The idea is to count the number of items that will be spawned and then intercept them as they are spawning
        //  to prevent only consume items we want to use as locations.

        /// <summary>
        /// Call on opening a chest. This accounts for the step in item pickups uses and submits locations.
        /// </summary>
        /// <returns>Returns true if a location was submitted.</returns>
        private bool chestOpened()
        {
            bool locationavailable = 0 < checkAvailable(LocationTypes.chest);

            // only count when checks are avaiable OR when counting does not roll over
            if (locationavailable || 0 != (chestitemsPickedUp + 1) % itemPickupStep)
            {
                chestitemsPickedUp++;
                Log.LogDebug("chest counted as towards the locations");
                updateBar(LocationTypes.chest);
            }
            else
            {
                Log.LogDebug("chest not counted as towards the locations");
            }

            // only send checks when rolling over
            if (locationavailable && 0 == chestitemsPickedUp % itemPickupStep) return sendNextAvailable(LocationTypes.chest);
            return false;
        }

        private void ChestBehavior_ItemDrop_Chest(On.RoR2.ChestBehavior.orig_ItemDrop orig, RoR2.ChestBehavior self)
        {
            // All chest like objects drop 1 item, this includes scavenger backpacks which just call this method several times.
            // Therefore we need to manually make sure the call here is not from the backpack.
            if(NetworkServer.active && self.dropPickup != PickupIndex.none && scavbackpackHash != self.GetHashCode())
            {
                chestblockitem = chestOpened();
            }

            orig(self); // the original will end up calling PickupDropletController_CreatePickupDroplet as well as other things
            chestblockitem = false;
        }

        private void SacrificeArtifactManager_OnServerCharacterDeath(On.RoR2.Artifacts.SacrificeArtifactManager.orig_OnServerCharacterDeath orig, DamageReport damageReport)
        {
            sacrificeitem = true;
            // OnServerCharacterDeath has a percent chance of calling CreatePickupDroplet_Chest.
            // Only when it is called will we want to treat it as a chest being opened.
            orig(damageReport);
            sacrificeitem = false;
        }

        private void PickupDropletController_CreatePickupDroplet_Chest(On.RoR2.PickupDropletController.orig_CreatePickupDroplet_PickupIndex_Vector3_Vector3 orig, RoR2.PickupIndex pickupIndex, UnityEngine.Vector3 position, UnityEngine.Vector3 velocity)
        {
            // check if the item being dropped is being asked to not drop
            if (chestblockitem)
            {
                Log.LogDebug($"chest item {pickupIndex} was used to satisfy a location and thus is consumed");
                return;
            }
            // check if the item is being dropped by sacrifice
            else if (sacrificeitem)
            {
                // if the item is from sacrifice, treat it as opening a chest
                if (chestOpened())
                {
                    Log.LogDebug($"sacrifice chest item {pickupIndex} was used to satisfy a location and thus is consumed");
                    return;
                }
                Log.LogDebug($"sacrifice chest item {pickupIndex} passed through");
            }
            orig(pickupIndex, position, velocity);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Shrine like objects

        // All shrines behave differently and there is no inheritance to a common shrine object
        // Therefore all shrine types will have to be handled differently.

        /// <summary>
        /// Call on beating a shrine. This accounts for the step in shrine uses and submits locations.
        /// </summary>
        /// <returns>Returns true if a location was submitted.</returns>
        private bool shrineBeat()
        {
            bool locationavailable = 0 < checkAvailable(LocationTypes.shrine);

            // only count when checks are avaiable OR when counting does not roll over
            if (locationavailable || 0 != (shrinesUsed + 1) % shrineUseStep)
            {
                shrinesUsed++;
                Log.LogDebug("shrine counted as towards the locations");
                updateBar(LocationTypes.shrine);
            }
            else
            {
                Log.LogDebug("shrine not counted as towards the locations");
            }

            // only send checks when rolling over
            if (locationavailable && 0 == shrinesUsed % shrineUseStep) return sendNextAvailable(LocationTypes.shrine);
            return false;
        }

        /// <summary>
        /// Determines whether the next shrineBeat() call will return true without calling it.
        /// </summary>
        /// <returns>Returns true if shrineBeat() would submit a location.</returns>
        private bool shrineWillBeLocation()
        {
            return (0 == (shrinesUsed + 1) % shrineUseStep) && (0 < checkAvailable(LocationTypes.shrine));
        }

        /// <summary>
        /// Beats the gold portal shrine when attempting to grant the portal entry.
        /// </summary>
        private void PortalStatueBehavior_GrantPortalEntry_Gold(On.RoR2.PortalStatueBehavior.orig_GrantPortalEntry orig, PortalStatueBehavior self)
        {
            orig(self);
            // using the gold shrine beats it; it already costs enough to use the shrine, so taking the portal away is just crule
            if (self.portalType == PortalStatueBehavior.PortalType.Goldshores) shrineBeat();
        }


        /// <summary>
        /// Using the blood shrine beats the shrine.
        /// </summary>
        private void ShrineBloodBehavior_AddShrineStack(On.RoR2.ShrineBloodBehavior.orig_AddShrineStack orig, ShrineBloodBehavior self, Interactor interactor)
        {
            Log.LogDebug("ShrineBloodBehavior_AddShrineStack"); // XXX remove after gold blocking is verified to not perma-block gold
            bloodshrineblockgold = shrineWillBeLocation(); // block gold only when it will be a check
            Log.LogDebug($"Intend to block gold: {bloodshrineblockgold}"); // XXX
            orig(self, interactor); // XXX somehow block the message about giving money
            bloodshrineblockgold = false;
            // we call beat shrine after setting bloodshrineblockgold to false to let money be collected in case shrineBeat() causes an exception
            shrineBeat(); // using the blood shrine beats it
        }

        /// <summary>
        /// Blood shrine blocks the money that it will give if the shrine was used as a location.
        /// </summary>
        private void CharacterMaster_GiveMoney(On.RoR2.CharacterMaster.orig_GiveMoney orig, CharacterMaster self, uint amount)
        {
            if (!bloodshrineblockgold) orig(self, amount);
            else Log.LogDebug($"CharacterMaster_GiveMoney: Gold blocked because blood shrine."); // XXX
        }

        /// <summary>
        /// Beat the chance shrine when a successful purchase happens.
        /// </summary>
        private void ShrineChanceBehavior_AddShrineStack(On.RoR2.ShrineChanceBehavior.orig_AddShrineStack orig, ShrineChanceBehavior self, Interactor activator)
        {
            Log.LogDebug("ShrineChanceBehavior_AddShrineStack"); // XXX remove after item blocking is verified to not perma-block items
            chanceshrineblockitem = shrineWillBeLocation();
            Log.LogDebug($"Intend to block item: {chanceshrineblockitem}"); // XXX
            chanceshrinebeat = false; // set the value to false, if it is set to true we know an item dropped because of the shrine
            orig(self, activator);
            Log.LogDebug($"Item drop detected: {chanceshrinebeat}"); // XXX
            chanceshrineblockitem = false;
            if (chanceshrinebeat) shrineBeat();
        }

        private void PickupDropletController_CreatePickupDroplet_ChanceShrine(On.RoR2.PickupDropletController.orig_CreatePickupDroplet_PickupIndex_Vector3_Vector3 orig, RoR2.PickupIndex pickupIndex, UnityEngine.Vector3 position, UnityEngine.Vector3 velocity)
        {
            // when an item dropplet is made, we will consider the shrine beat
            chanceshrinebeat = true;
            // Note, this will set the value to true even when the item is not from a shrine.
            // This is why the value needs to be set to false when the shrine intends to actually use the value and observe it.

            // check if the item being dropped is being asked to not drop
            if (chanceshrineblockitem)
            {
                Log.LogDebug($"chance shrine item {pickupIndex} was used to satisfy a location and thus is consumed");
                return;
            }
            orig(pickupIndex, position, velocity);
        }

        /// <summary>
        /// Using the shcange shrine beats it.
        /// </summary>
        private void ShrineCombatBehavior_AddShrineStack(On.RoR2.ShrineCombatBehavior.orig_AddShrineStack orig, ShrineCombatBehavior self, Interactor interactor)
        {
            orig(self, interactor);
            // TODO maybe combat shrine shouldn't be an instant reward
            shrineBeat(); // using the combat shrine beats it
        }

        /// <summary>
        /// Using the order shrine beats it
        /// </summary>
        private void ShrineRestackBehavior_AddShrineStack(On.RoR2.ShrineRestackBehavior.orig_AddShrineStack orig, ShrineRestackBehavior self, Interactor interactor)
        {
            orig(self, interactor);
            shrineBeat(); // using the order shrine beats it
        }

        /// <summary>
        /// When the boss group is attempting to drop bonus rewards, the mountain shrines which granted the bonus are beat.
        /// </summary>
        private void BossGroup_DropRewards(On.RoR2.BossGroup.orig_DropRewards orig, BossGroup self)
        {
            Log.LogDebug($"bonusRewardCount initially: {self.bonusRewardCount}");
            for (int n = self.bonusRewardCount; n > 0; n--)
            {
                Log.LogDebug("bonusRewardCount means a mountain shrine was beat");
                // the only way to raise the bonusRewardCount of a boss is via a mountain shrine

                // beat the mountain shrine per mountain activated when the teleporter finishes
                if (shrineBeat()) self.bonusRewardCount--;
                // each location sent should mean one less bonus
            }
            Log.LogDebug($"bonusRewardCount adjusted: {self.bonusRewardCount}");

            orig(self);
        }

        /// <summary>
        /// Purchasing the each of the last two upgrades of the woods shrine beats the shrine.
        /// </summary>
        private void ShrineHealingBehavior_AddShrineStack(On.RoR2.ShrineHealingBehavior.orig_AddShrineStack orig, ShrineHealingBehavior self, Interactor activator)
        {
            orig(self, activator);
            // the last two purchases of woods shine are checks
            if (self.purchaseCount > self.maxPurchaseCount - 2) shrineBeat();
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Scavenger

        // Scavengers will be counted by the number of bags opened.

        private void Opening_OnEnter(On.EntityStates.ScavBackpack.Opening.orig_OnEnter orig, EntityStates.ScavBackpack.Opening self)
        {
            orig(self);
            scavbackpackHash = self.chestBehavior.GetHashCode();
            scavbackpackWasLocation = sendNextAvailable(LocationTypes.scavenger);
        }

        private void ChestBehavior_ItemDrop_Scavenger(On.RoR2.ChestBehavior.orig_ItemDrop orig, ChestBehavior self)
        {
            // All chest like objects drop 1 item, this includes scavenger backpacks which just call this method several times.
            // Therefore we need to manually make sure the call here is from the backpack.
            if(NetworkServer.active && self.dropPickup != PickupIndex.none && scavbackpackHash == self.GetHashCode())
            {
                // TODO make an option to block scavenger backpacks from dropping items
                scavbackpackblockitem = scavbackpackWasLocation;
            }

            orig(self); // the original will end up calling PickupDropletController_CreatePickupDroplet as well as other things
            scavbackpackblockitem = false;
        }

        private void PickupDropletController_CreatePickupDroplet_Scavenger(On.RoR2.PickupDropletController.orig_CreatePickupDroplet_PickupIndex_Vector3_Vector3 orig, PickupIndex pickupIndex, UnityEngine.Vector3 position, UnityEngine.Vector3 velocity)
        {
            // check if the item being dropped is being asked to not drop
            if (scavbackpackblockitem)
            {
                Log.LogDebug($"scavenger backpack was used as a location so this item will be consumed");
                return;
            }
            orig(pickupIndex, position, velocity);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Radio scanner

        // Radio scanners will need to be forcefully spawned even if the player has purchased them
        //  otherwise the check would be impossible to complete.

        private void SceneDirector_PopulateScene(On.RoR2.SceneDirector.orig_PopulateScene orig, SceneDirector self)
        {
            // XXX somehow SceneDirector_PopulateScene can get called several times in a row, thus spawning a bunch of scanners... why do the calls happen?
            Log.LogDebug("SceneDirector_PopulateScene"); // XXX remove after figuring out why this can get called repeatedly
            // XXX perhaps a solution could be to use flags similar to shrines if there is no apparent reason why scene population can repeat

            orig(self); // let the director do it's own thing first as to not get in the way

            if (0 < checkAvailable(LocationTypes.radio_scanner))
            // we always want to always spawn a radio scanner if it is a location
            {
                Log.LogDebug("Environment has radio_scanner locations, spawning an iscRadarTower.");

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

            if (0 == checkAvailable(LocationTypes.radio_scanner))
            {
                // there are no checks, treat the scanner as if it were a vanilla scanner
                orig(self, interactor);
                return;
            }

            sendNextAvailable(LocationTypes.radio_scanner);

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

        private void PortalStatueBehavior_GrantPortalEntry_Blue(On.RoR2.PortalStatueBehavior.orig_GrantPortalEntry orig, PortalStatueBehavior self)
        {
            if (self.portalType != PortalStatueBehavior.PortalType.Shop)
            {
                orig(self);
                return;
            } // the below code is only applied to blue portal, ie an altar was used


            Log.LogDebug("intercepted blue portal ie altar used; attempt to send check");
            if (false == sendNextAvailable(LocationTypes.newt_altar))
            {
                Log.LogDebug("no check performed; granting blue portal");
                orig(self);
                return;
            }
            else Log.LogDebug("check performed; denying blue portal");

            // refund the lunar coin if the player who payed the coin is this client's player
            //interactor.GetComponent<NetworkUser>().AwardLunarCoins(1); // (only the server actually executes the contents of this method) // TODO give coin only to one person
            foreach (NetworkUser local in NetworkUser.readOnlyLocalPlayersList)
            {
                Log.LogDebug("Refunding coins...");
                local.AwardLunarCoins(1);
                // TODO This does in fact give more coins back in multiplayer since every player would get a coin.
                // I don't have a solution for this right now : ^)
            }

            // don't block the other newts, more than one newt in a stage is rare and if also rewards knowing where newts can spawn when you can find and get to two

            // don't run the original as we do not want to spawn the portal
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////

    }

    // TODO it may be interesting if Baazar seers could allow the player to travel to environments earlier in the loop (ie to give more control over where the player goes)
}

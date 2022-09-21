using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.RiskOfRain2.Extensions;
using Archipelago.RiskOfRain2.Handlers;
using Archipelago.RiskOfRain2.Net;
using Archipelago.RiskOfRain2.UI;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Archipelago.RiskOfRain2
{
    public class ArchipelagoItemLogicController : IDisposable
    {
        public int PickedUpItemCount { get; set; }
        public int ItemPickupStep { get; set; }
        public int CurrentChecks { get; set; }
        public int TotalChecks { get; set; }

        internal StageBlockerHandler Stageblockerhandler { get; set; }

        public delegate void ItemDropProcessedHandler(int pickedUpCount);
        public event ItemDropProcessedHandler OnItemDropProcessed;

        private bool finishedAllChecks = false;
        private ArchipelagoSession session;
        private Queue<KeyValuePair<int, string>> itemReceivedQueue = new Queue<KeyValuePair<int, string>>();
        private Queue<KeyValuePair<int, string>> environmentReceivedQueue = new Queue<KeyValuePair<int, string>>();
        private const int environmentRangeLower = 37700;
        private const int environmentRangeUpper = 37999;
        private PickupIndex[] skippedItems;

        private GameObject smokescreenPrefab;

        private bool IsInGame
        {
            get
            {
                return (RoR2Application.isInSinglePlayer || RoR2Application.isInMultiPlayer) && RoR2.Run.instance != null;
            }
        }

        public ArchipelagoItemLogicController(ArchipelagoSession session)
        {
            this.session = session;
            // XXX I need to make sure the LocationHandler is truely handling the stages instead of this all-in-one class
            On.RoR2.PickupDropletController.CreatePickupDroplet_PickupIndex_Vector3_Vector3 += PickupDropletController_CreatePickupDroplet;
            On.RoR2.RoR2Application.Update += RoR2Application_Update;
            session.Socket.PacketReceived += Session_PacketReceived;
            session.Items.ItemReceived += Items_ItemReceived;

            Log.LogDebug("Okay finished hooking.");
            smokescreenPrefab = Addressables.LoadAssetAsync<GameObject>("Assets/RoR2/Junk/Characters/Bandit/Skills/SmokescreenEffect.prefab").WaitForCompletion();
            Log.LogDebug("Okay, finished geting prefab.");

            skippedItems = new PickupIndex[]
            {
                PickupCatalog.FindPickupIndex(RoR2Content.Equipment.AffixBlue.equipmentIndex),
                //PickupCatalog.FindPickupIndex(RoR2Content.Equipment.AffixEcho.equipmentIndex), // Causes NRE... Not sure why.
                PickupCatalog.FindPickupIndex(RoR2Content.Equipment.AffixHaunted.equipmentIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Equipment.AffixLunar.equipmentIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Equipment.AffixPoison.equipmentIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Equipment.AffixRed.equipmentIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Equipment.AffixWhite.equipmentIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.MiscPickups.LunarCoin.miscPickupIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Items.ArtifactKey.itemIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.Bomb.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.Command.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.EliteOnly.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.Enigma.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.FriendlyFire.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.Glass.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.MixEnemy.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.MonsterTeamGainsItems.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.RandomSurvivorOnRespawn.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.Sacrifice.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.ShadowClone.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.SingleMonsterType.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.Swarms.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.TeamDeath.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.WeakAssKnees.artifactIndex),
                PickupCatalog.FindPickupIndex(RoR2Content.Artifacts.WispOnDeath.artifactIndex),
            };
            Log.LogDebug("Ok, finished browsing catalog.");
        }

        private void Items_ItemReceived(MultiClient.Net.Helpers.ReceivedItemsHelper helper)
        {
            var newItem = helper.DequeueItem();
            EnqueueItem(newItem.Item);
        }

        private void Session_PacketReceived(ArchipelagoPacketBase packet)
        {
            switch (packet.PacketType)
            {
                case ArchipelagoPacketType.Connected:
                    {
                        var connectedPacket = packet as ConnectedPacket;
                        // Add 1 because the user's YAML will contain a value equal to "number of pickups before sent location"
                        ItemPickupStep = Convert.ToInt32(connectedPacket.SlotData["itemPickupStep"]) + 1;
                        TotalChecks = connectedPacket.LocationsChecked.Count() + connectedPacket.MissingChecks.Count();
                        
                        CurrentChecks = connectedPacket.LocationsChecked.Count();

                        ArchipelagoTotalChecksObjectiveController.CurrentChecks = CurrentChecks;
                        ArchipelagoTotalChecksObjectiveController.TotalChecks = TotalChecks;

                        new SyncTotalCheckProgress(CurrentChecks, TotalChecks).Send(NetworkDestination.Clients);

                        // Add up pickedUpItemCount so that resuming a game is possible. The intended behavior is that you immediately receive
                        // all of the items you are granted. This is for restarting (in case you lose a run but are not in commencement). 
                        PickedUpItemCount = connectedPacket.LocationsChecked.Count() * ItemPickupStep;
                        break;
                    }
            }
        }

        public void EnqueueItem(int itemId)
        {
            // convert the itemId to a name here instead of in the main loop
            // this prevents a call to the session in the RoR2Application_Update
            var itemName = session.Items.GetItemName(itemId);
            // We will keep track of the item id as well as since the name cannot be converted back to an id.

            // Separate the environments and items so that the environments can be precollected
            //  when the run starts.
            if (environmentRangeLower <= itemId && itemId <= environmentRangeUpper)
            {
                environmentReceivedQueue.Enqueue(new KeyValuePair<int, string>(itemId, itemName));
            }
            else
            {
                itemReceivedQueue.Enqueue(new KeyValuePair<int, string>(itemId, itemName));
            }

        }

        public void Dispose()
        {
            On.RoR2.PickupDropletController.CreatePickupDroplet_PickupIndex_Vector3_Vector3 -= PickupDropletController_CreatePickupDroplet;
            On.RoR2.RoR2Application.Update -= RoR2Application_Update;

            if (session != null)
            {
                session.Socket.PacketReceived -= Session_PacketReceived;
                session = null;
            }
        }

        /**
         * At the start of a run, we need to precollect all environments before environments are picked for stages.
         */
        public void Precollect()
        {
            while (environmentReceivedQueue.Any())
            {
                Log.LogDebug("Precollecting environment...");
                HandleReceivedEnvironmentQueueItem();
            }
        }

        private void RoR2Application_Update(On.RoR2.RoR2Application.orig_Update orig, RoR2Application self)
        {
            if (IsInGame)
            {
                if (itemReceivedQueue.Any())
                {
                    HandleReceivedItemQueueItem();
                }
                if (environmentReceivedQueue.Any())
                {
                    HandleReceivedEnvironmentQueueItem();
                }
            }

            orig(self);
        }

        private void HandleReceivedEnvironmentQueueItem()
        {
            KeyValuePair<int, string> itemReceived = environmentReceivedQueue.Dequeue();

            int itemIdRecieved = itemReceived.Key;
            string itemNameReceived = itemReceived.Value;

            Log.LogDebug($"Handling environment with itemid {itemIdRecieved} with name {itemNameReceived}");
            Stageblockerhandler.UnBlock(itemIdRecieved - environmentRangeLower);
        }

        private void HandleReceivedItemQueueItem()
        {
            KeyValuePair<int, string> itemReceived = itemReceivedQueue.Dequeue();

            int itemIdRecieved = itemReceived.Key;
            string itemNameReceived = itemReceived.Value;

            Log.LogDebug($"Handling item with itemid {itemIdRecieved} with name {itemNameReceived}");

            switch (itemIdRecieved)
            {
                // TODO move the magic numbers to variables
                // "Common Item"
                case 37002:
                    var common = Run.instance.availableTier1DropList.Choice();
                    GiveItemToPlayers(common);
                    break;
                // "Uncommon Item"
                case 37003:
                    var uncommon = Run.instance.availableTier2DropList.Choice();
                    GiveItemToPlayers(uncommon);
                    break;
                // "Legendary Item"
                case 37004:
                    var legendary = Run.instance.availableTier3DropList.Choice();
                    GiveItemToPlayers(legendary);
                    break;
                // "Boss Item"
                case 37005:
                    var boss = Run.instance.availableBossDropList.Choice();
                    GiveItemToPlayers(boss);
                    break;
                // "Lunar Item"
                case 37006:
                    var lunar = Run.instance.availableLunarCombinedDropList.Choice();
                    var pickupDef = PickupCatalog.GetPickupDef(lunar);
                    if (pickupDef.itemIndex != ItemIndex.None)
                    {
                        GiveItemToPlayers(lunar);
                    }
                    else if (pickupDef.equipmentIndex != EquipmentIndex.None)
                    {
                        GiveEquipmentToPlayers(lunar);
                    }
                    break;
                // "Equipment"
                case 37007:
                    var equipment = Run.instance.availableEquipmentDropList.Choice();
                    GiveEquipmentToPlayers(equipment);
                    break;
                // "Item Scrap, White"
                case 37008:
                    GiveItemToPlayers(PickupCatalog.FindPickupIndex(RoR2Content.Items.ScrapWhite.itemIndex));
                    break;
                // "Item Scrap, Green"
                case 37009:
                    GiveItemToPlayers(PickupCatalog.FindPickupIndex(RoR2Content.Items.ScrapGreen.itemIndex));
                    break;
                // "Item Scrap, Red"
                case 37010:
                    GiveItemToPlayers(PickupCatalog.FindPickupIndex(RoR2Content.Items.ScrapRed.itemIndex));
                    break;
                // "Item Scrap, Yellow"
                case 37011:
                    GiveItemToPlayers(PickupCatalog.FindPickupIndex(RoR2Content.Items.ScrapYellow.itemIndex));
                    break;
                // "Dio's Best Friend"
                case 37001:
                    GiveItemToPlayers(PickupCatalog.FindPickupIndex(RoR2Content.Items.ExtraLife.itemIndex));
                    break;
            }
        }

        private void GiveEquipmentToPlayers(PickupIndex pickupIndex)
        {
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var inventory = player.master.inventory;
                var activeEquipment = inventory.GetEquipment(inventory.activeEquipmentSlot);
                if (!activeEquipment.Equals(EquipmentState.empty))
                {
                    var playerBody = player.master.GetBodyObject();

                    if (playerBody == null)
                    {
                        //TODO: maybe deal with this
                        return;
                    }

                    var pickupInfo = new GenericPickupController.CreatePickupInfo()
                    {
                        pickupIndex = PickupCatalog.FindPickupIndex(activeEquipment.equipmentIndex),
                        position = playerBody.transform.position,
                        rotation = Quaternion.identity
                    };
                    GenericPickupController.CreatePickup(pickupInfo);
                }

                inventory.SetEquipmentIndex(PickupCatalog.GetPickupDef(pickupIndex)?.equipmentIndex ?? EquipmentIndex.None);
                DisplayPickupNotification(pickupIndex);
            }
        }

        private void GiveItemToPlayers(PickupIndex pickupIndex)
        {
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var inventory = player.master.inventory;
                inventory.GiveItem(PickupCatalog.GetPickupDef(pickupIndex)?.itemIndex ?? ItemIndex.None);
                DisplayPickupNotification(pickupIndex);
            }
        }

        private void DisplayPickupNotification(PickupIndex index)
        {
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                CharacterMasterNotificationQueue.PushPickupNotification(player.master, index);
            }
        }

        private void PickupDropletController_CreatePickupDroplet(On.RoR2.PickupDropletController.orig_CreatePickupDroplet_PickupIndex_Vector3_Vector3 orig, PickupIndex pickupIndex, Vector3 position, Vector3 velocity)
        {
            if (skippedItems.Contains(pickupIndex))
            {
                orig(pickupIndex, position, velocity);
                return;
            }

            // Run `HandleItemDrop()` first so that the `PickedUpItemCount` is incremented by the time `ItemDropProcessed()` is called.
            var spawnItem = finishedAllChecks || HandleItemDrop();
            
            if (OnItemDropProcessed != null)
            {
                OnItemDropProcessed(PickedUpItemCount);
            }

            if (spawnItem)
            {
                orig(pickupIndex, position, velocity);
            }

            if (!spawnItem)
            {
                EffectManager.SpawnEffect(smokescreenPrefab, new EffectData() { origin = position }, true);
            }

            new SyncTotalCheckProgress(finishedAllChecks ? TotalChecks : CurrentChecks, TotalChecks).Send(NetworkDestination.Clients);

            if (finishedAllChecks)
            {
                ArchipelagoTotalChecksObjectiveController.RemoveObjective();
                new AllChecksComplete().Send(NetworkDestination.Clients);
            }
        }

        private bool HandleItemDrop()
        {
            PickedUpItemCount += 1;

            if ((PickedUpItemCount % ItemPickupStep) == 0)
            {
                CurrentChecks = PickedUpItemCount / ItemPickupStep;

                ArchipelagoTotalChecksObjectiveController.CurrentChecks = CurrentChecks;

                if (CurrentChecks == TotalChecks)
                {
                    ArchipelagoTotalChecksObjectiveController.CurrentChecks = ArchipelagoTotalChecksObjectiveController.TotalChecks;
                    finishedAllChecks = true;
                }

                var itemSendName = $"ItemPickup{CurrentChecks}";
                //var itemLocationId = session.Locations.GetLocationIdFromName("Risk of Rain 2", itemSendName);
                // TODO come back and move magic number to variable
                var itemLocationId = 38000 + CurrentChecks;
                Log.LogDebug($"Sent out location {itemSendName} (id: {itemLocationId})");

                var packet = new LocationChecksPacket();
                packet.Locations = new List<long> { itemLocationId }.ToArray();

                session.Socket.SendPacket(packet);
                return false;
            }
            return true;
        }
    }
}

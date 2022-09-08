using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.RiskOfRain2.Handlers;
using Archipelago.RiskOfRain2.Net;
using Archipelago.RiskOfRain2.UI;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace Archipelago.RiskOfRain2
{
    //TODO: perhaps only use particular drops as fodder for item pickups (i.e. only chest drops/interactable drops) then set options based on them maybe
    public class ArchipelagoClient : IDisposable
    {
        public delegate void ClientDisconnected(ushort code, string reason, bool wasClean);
        public event ClientDisconnected OnClientDisconnect;

        public Uri LastServerUrl { get; set; }
        internal DeathLinkHandler Deathlinkhandler { get; private set; }

        public ArchipelagoItemLogicController ItemLogic;
        public ArchipelagoLocationCheckProgressBarUI LocationCheckBar;

        private ArchipelagoSession session;
        private bool enableDeathLink = false;
        private DeathLinkService deathLinkService;
        private bool finalStageDeath = true;

        public ArchipelagoClient()
        {

        }

        public void Setup_SetDeathLink(bool enabled)
        {
            enableDeathLink = enabled;
        }

        public void Connect(Uri url, string slotName, string password = null, string[] tags = null)
        {
            ChatMessage.SendColored($"Attempting to connect to Archipelago at ${url}.", Color.green);
            Dispose();

            LastServerUrl = url;

            session = ArchipelagoSessionFactory.CreateSession(url);
            ItemLogic = new ArchipelagoItemLogicController(session);
            LocationCheckBar = new ArchipelagoLocationCheckProgressBarUI();

            List<string> taglist = tags is not null ? tags.ToList<string>() : new List<string>();

            if (enableDeathLink)
            {
                Log.LogDebug("Tagging DeathLink");
                taglist.Add("DeathLink");
            }

            tags = taglist.ToArray();

            var result = session.TryConnectAndLogin("Risk of Rain 2", slotName, new Version(3,4,0), itemsHandlingFlags: ItemsHandlingFlags.AllItems, tags: tags);

            if (!result.Successful)
            {
                LoginFailure failureResult = (LoginFailure)result;
                foreach (var err in failureResult.Errors)
                {
                    ChatMessage.SendColored(err, Color.red);
                    Log.LogError(err);
                }
                return;
            }

            if (enableDeathLink)
            {
                Log.LogDebug("Starting DeathLink service");
                deathLinkService = session.CreateDeathLinkServiceAndEnable();
                Deathlinkhandler = new DeathLinkHandler(deathLinkService);
            }

            LoginSuccessful successResult = (LoginSuccessful)result;
            if (successResult.SlotData.TryGetValue("FinalStageDeath", out var stageDeathObject))
            {
                finalStageDeath = Convert.ToBoolean(stageDeathObject);
            }

            LocationCheckBar.ItemPickupStep = ItemLogic.ItemPickupStep;

            session.Socket.PacketReceived += Session_PacketReceived;
            session.Socket.SocketClosed += Session_SocketClosed;
            ItemLogic.OnItemDropProcessed += ItemLogicHandler_ItemDropProcessed;

            HookGame();
            new ArchipelagoStartMessage().Send(NetworkDestination.Clients);
        }

        public void Dispose()
        {
            if (session != null && session.Socket.Connected)
            {
                session.Socket.Disconnect();
            }
            
            if (ItemLogic != null)
            {
                ItemLogic.OnItemDropProcessed -= ItemLogicHandler_ItemDropProcessed;
                ItemLogic.Dispose();
            }
            
            if (LocationCheckBar != null)
            {
                LocationCheckBar.Dispose();
            }
         
            UnhookGame();
            session = null;
        }

        private void HookGame()
        {
            On.RoR2.UI.ChatBox.SubmitChat += ChatBox_SubmitChat;
            RoR2.Run.onRunDestroyGlobal += Run_onRunDestroyGlobal;
            On.RoR2.Run.BeginGameOver += Run_BeginGameOver;
            ArchipelagoChatMessage.OnChatReceivedFromClient += ArchipelagoChatMessage_OnChatReceivedFromClient;

            Deathlinkhandler?.Hook();
        }

        private void UnhookGame()
        {
            On.RoR2.UI.ChatBox.SubmitChat -= ChatBox_SubmitChat;
            RoR2.Run.onRunDestroyGlobal -= Run_onRunDestroyGlobal;
            On.RoR2.Run.BeginGameOver -= Run_BeginGameOver;
            ArchipelagoChatMessage.OnChatReceivedFromClient -= ArchipelagoChatMessage_OnChatReceivedFromClient;

            Deathlinkhandler?.UnHook();
        }

        private void ArchipelagoChatMessage_OnChatReceivedFromClient(string message)
        {
            if (session.Socket.Connected && !string.IsNullOrEmpty(message))
            {
                var sayPacket = new SayPacket();
                sayPacket.Text = message;
                session.Socket.SendPacket(sayPacket);
            }
        }

        private void ItemLogicHandler_ItemDropProcessed(int pickedUpCount)
        {
            if (LocationCheckBar != null)
            {
                LocationCheckBar.CurrentItemCount = pickedUpCount;
                if ((LocationCheckBar.CurrentItemCount % ItemLogic.ItemPickupStep) == 0)
                {
                    LocationCheckBar.CurrentItemCount = 0;
                }
                else
                {
                    LocationCheckBar.CurrentItemCount = LocationCheckBar.CurrentItemCount % ItemLogic.ItemPickupStep;
                }
            }
            new SyncLocationCheckProgress(LocationCheckBar.CurrentItemCount, LocationCheckBar.ItemPickupStep).Send(NetworkDestination.Clients);
        }

        private void ChatBox_SubmitChat(On.RoR2.UI.ChatBox.orig_SubmitChat orig, ChatBox self)
        {
            var text = self.inputField.text;
            if (session.Socket.Connected && !string.IsNullOrEmpty(text))
            {
                var sayPacket = new SayPacket();
                sayPacket.Text = text;
                session.Socket.SendPacket(sayPacket);

                self.inputField.text = string.Empty;
                orig(self);
            }
            else
            {
                orig(self);
            }
        }

        private void Session_SocketClosed(WebSocketSharp.CloseEventArgs e)
        {
            Dispose();
            new ArchipelagoEndMessage().Send(NetworkDestination.Clients);

            if (OnClientDisconnect != null)
            {
                OnClientDisconnect(e.Code, e.Reason, e.WasClean);
            }
        }

        //public IEnumerator AttemptConnection()
        //{
        //    reconnecting = true;
        //    var retryCounter = 0;

        //    while ((session == null || !session.Socket.Connected)&& retryCounter < 5)
        //    {
        //        ChatMessage.Send($"Connection attempt #{retryCounter+1}");
        //        retryCounter++;
        //        yield return new WaitForSeconds(3f);
        //        Connect(LastServerUrl, connectPacket.Name, connectPacket.Password);
        //    }

        //    if (session == null || !session.Socket.Connected)
        //    {
        //        ChatMessage.SendColored("Could not connect to Archipelago.", Color.red);
        //        Dispose();
        //    }
        //    else if (session != null && session.Socket.Connected)
        //    {
        //        ChatMessage.SendColored("Established Archipelago connection.", Color.green);
        //        new ArchipelagoStartMessage().Send(NetworkDestination.Clients);
        //    }

        //    reconnecting = false;
        //    RecentlyReconnected = true;
        //}

        private void Session_PacketReceived(ArchipelagoPacketBase packet)
        {
            switch (packet.PacketType)
            {
                case ArchipelagoPacketType.Print:
                    {
                        var printPacket = packet as PrintPacket;
                        ChatMessage.Send(printPacket.Text);
                        break;
                    }
                case ArchipelagoPacketType.PrintJSON:
                    {
                        var printJsonPacket = packet as PrintJsonPacket;
                        string text = "";
                        foreach (var part in printJsonPacket.Data)
                        {
                            switch (part.Type)
                            {
                                case JsonMessagePartType.PlayerId:
                                    {
                                        int playerId = int.Parse(part.Text);
                                        text += session.Players.GetPlayerName(playerId);
                                        break;
                                    }
                                case JsonMessagePartType.ItemId:
                                    {
                                        int itemId = int.Parse(part.Text);
                                        text += session.Items.GetItemName(itemId);
                                        break;
                                    }
                                case JsonMessagePartType.LocationId:
                                    {
                                        int locationId = int.Parse(part.Text);
                                        text += session.Locations.GetLocationNameFromId(locationId);
                                        break;
                                    }
                                default:
                                    {
                                        text += part.Text;
                                        break;
                                    }
                            }
                        }
                        ChatMessage.Send(text);
                        break;
                    }
            }
        }
        private void Run_BeginGameOver(On.RoR2.Run.orig_BeginGameOver orig, Run self, GameEndingDef gameEndingDef)
        {
            // If ending is acceptable, finish the archipelago run.
            if (IsEndingAcceptable(gameEndingDef))
            {
                var packet = new StatusUpdatePacket();
                packet.Status = ArchipelagoClientState.ClientGoal;
                session.Socket.SendPacket(packet);

                new ArchipelagoEndMessage().Send(NetworkDestination.Clients);
            }
            orig(self, gameEndingDef);
        }

        private bool IsEndingAcceptable(GameEndingDef gameEndingDef)
        {
            // Acceptable ending types
            var acceptableEndings = new[] { 
                RoR2Content.GameEndings.MainEnding, 
                RoR2Content.GameEndings.ObliterationEnding, 
                RoR2Content.GameEndings.LimboEnding, 
                DLC1Content.GameEndings.VoidEnding 
            };

            // Acceptable stages to die on
            var acceptableLosses = new[]
            {
                "moon",
                "moon2",
                "voidraid"
            };

            return acceptableEndings.Contains(gameEndingDef) 
                  ||(finalStageDeath 
                     && gameEndingDef == RoR2Content.GameEndings.StandardLoss 
                     && acceptableLosses.Contains(Stage.instance.sceneDef.baseSceneName)
                    );
        }

        private void Run_onRunDestroyGlobal(Run obj)
        {
            Dispose();
        }
    }
}

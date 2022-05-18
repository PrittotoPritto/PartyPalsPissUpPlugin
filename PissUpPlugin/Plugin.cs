using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using XivCommon;

namespace PissUpPlugin
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "PissUpPlugin";

        private const string commandName = "/pppp";

        [PluginService]
        internal static DalamudPluginInterface DalamudPluginInterface { get; private set; }
        [PluginService]
        internal static ClientState ClientState { get; private set; }
        [PluginService]
        internal static CommandManager CommandManager { get; private set; }
        [PluginService]
        internal static ChatGui ChatGui { get; private set; }
        [PluginService]
        internal static Framework Framework { get; private set; }
        [PluginService]
        internal static PartyList PartyList { get; private set; }

        public XivCommonBase Common { get; }

        private Configuration Configuration { get; init; }
        private PluginUI PluginUi { get; init; }

        //Running the game
        private Task? GameSessionInProgress = null;

        //Outputting messages
        private bool ChatAvailable = true; //TODO: true for dev reload testing, set to false!
        private Channel<string> MessageSink;
        private struct DelayedReader
        {
            const uint MinDelayMS = 100;
            public DelayedReader(ChannelReader<string> Source)
            {
                RawReader = Source;
                NextAvailable = DateTime.Now;
            }
            private ChannelReader<string> RawReader;
            private DateTime NextAvailable;
            public bool TryRead(out string Out)
            {
                Out = "";
                if (DateTime.Now < NextAvailable)
                {
                    return false;
                }
                else
                {
                    if (RawReader.TryRead(out Out))
                    {
                        NextAvailable = DateTime.Now + TimeSpan.FromMilliseconds(MinDelayMS);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }
        private DelayedReader MessageSource;
        private CancellationTokenSource TaskCancellationTokenSource = new CancellationTokenSource();

        public Plugin()
        {
            this.Common = new XivCommonBase();
            this.Configuration = DalamudPluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(DalamudPluginInterface);
            // you might normally want to embed resources and load them from the manifest stream
            this.PluginUi = new PluginUI(this.Configuration);

            CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the Piss-up Plugin GUI. More commands may come later"
            });

            DalamudPluginInterface.UiBuilder.Draw += DrawUI;
            DalamudPluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            this.MessageSink = Channel.CreateUnbounded<string>();
            this.MessageSource = new DelayedReader(this.MessageSink.Reader);

            Framework.Update += this.OnFrameworkUpdate;
            ClientState.Login += this.OnLogin;
            ClientState.Logout += this.OnLogout;

        }

        private void ClearTask(bool DisposingPlugin = false)
        {
            if (this.GameSessionInProgress != null)
            {
                TaskCancellationTokenSource.Cancel();
                this.GameSessionInProgress.Wait();
                this.GameSessionInProgress.Dispose();
                this.GameSessionInProgress = null;
                TaskCancellationTokenSource.Dispose();
                if (!DisposingPlugin)
                {
                    TaskCancellationTokenSource = new CancellationTokenSource();
                }
            }
        }
        public void Dispose()
        {
            ClearTask(true);

            Framework.Update -= this.OnFrameworkUpdate;
            ClientState.Login -= this.OnLogin;
            ClientState.Logout -= this.OnLogout;
            
            CommandManager.RemoveHandler(commandName);
            this.PluginUi.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
            if (args.Trim().StartsWith("go", System.StringComparison.OrdinalIgnoreCase))
            {
                ClearTask();
                this.GameSessionInProgress = RunGame(this.Configuration);
            }
            else
            {
                this.PluginUi.Visible = true;
            }
        }
        public void OnFrameworkUpdate(Framework framework1)
        {
            if (!this.MessageSource.TryRead(out var Message) || !this.ChatAvailable)
            {
                return;
            }
            this.Common.Functions.Chat.SendMessage(Message);
        }
        private void OnLogin(object? sender, EventArgs args)
        {
            this.ChatAvailable = true;
        }

        private void OnLogout(object? sender, EventArgs args)
        {
            this.ChatAvailable = false;
            ClearTask();
        }
        private void DrawUI()
        {
            this.PluginUi.Draw();
        }
        
        private void DrawConfigUI()
        {
            this.PluginUi.SettingsVisible = true;
        }

        //The section below supports the game running task.

        private struct Roll
        {
            public string Player;
            public uint Value;
        }
        //A roll is expected to and (optional) range rolled, a dice icon, and a value.
        private enum ExpectedRollStage
        {
            RollLimit,
            DiceIcon,
            Value,
            Done
        }

        const string SepText =   "-----------------------------";
        const string StartText = "-----------New Round---------";
        const string EndText =   "-----------Round Over--------";
        async Task RunGame(Configuration Config)
        {
            if (this.GameSessionInProgress != null)
            {
                TaskCancellationTokenSource.Cancel();
                this.GameSessionInProgress.Wait();
                this.GameSessionInProgress.Dispose();
                this.GameSessionInProgress = null;
            }
            CancellationToken TaskCancellationToken = TaskCancellationTokenSource.Token;
            //Set up any variables we'll need in the task. 
            bool IsAlliance = Config.IsAlliance;
            Configuration.Game ThisGame = Config.CurrentGame;
            var MessageSource = MessageSink.Writer;
            //PartyList.IsAlliance doesn't work, so we need to config this.
            string ChatTarget = IsAlliance ? "/a ": "/p "; //end in a space.
            string NumberAddition = ThisGame.DiceValue > 0 ? $" {ThisGame.DiceValue}" : ""; //leading space if it exists
            string ChatCommand = (IsAlliance ? "/dice alliance": "/dice party") + NumberAddition;
            //Set up our chat message delegate ready for attaching.
            List<Roll> PlayerRolls = new List<Roll>();
            ExpectedRollStage InitialRollStage = ExpectedRollStage.RollLimit;
            Regex NumberRe = new Regex(
                ThisGame.DiceValue > 0 ? $"(\\D|^){ThisGame.DiceValue}(\\D|$)" : "^[\\D]+$"
            ); //Abuse this if we have a zero to check for a string with no values.
            ChatGui.OnMessageDelegate OnChatMessage = (XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
            =>
            {
                //Assumption: all the chat types we're interested in are mutually exclusive.
                if (type == XivChatType.Alliance || type == XivChatType.Party || type == XivChatType.CrossParty)
                {

                    //Dalamud.Logging.PluginLog.Log($"Sender: {sender.ToString()}");
                    //Dalamud.Logging.PluginLog.Log($"Value: {message.ToString()}");
                    //Get the player name:
                    string? Player = null;
                    var PlayerOrOwnName = sender.Payloads[0];
                    if (PlayerOrOwnName.Type == PayloadType.RawText)
                    {
                        //Cut off the special character at the start of your own name
                        Player = ((TextPayload)PlayerOrOwnName).Text![1..];
                    }
                    else if (PlayerOrOwnName.Type == PayloadType.Player)
                    {
                        Player = ((PlayerPayload)PlayerOrOwnName).PlayerName;
                    }
                    if (Player == null)
                    {
                        //Dalamud.Logging.PluginLog.Log($"Unexpected start to message sender: {PlayerOrOwnName.ToString()}");
                        return;
                    }
                    //Dalamud.Logging.PluginLog.Log($"Player: {Player}");
                    //See if we've got a dice roll:
                    ExpectedRollStage CurrentStage = InitialRollStage;
                    uint? RollValue = null;
                    foreach (var Payload in message.Payloads)
                    {
                        if (CurrentStage == ExpectedRollStage.Done)
                        {
                            break;
                        }
                        switch(CurrentStage)
                        {
                            case ExpectedRollStage.RollLimit:
                                if (Payload.Type == PayloadType.RawText)
                                {
                                    if (NumberRe.IsMatch(((TextPayload)Payload).Text))
                                    {
                                        //Dalamud.Logging.PluginLog.Log("Roll limit");
                                        CurrentStage = ExpectedRollStage.DiceIcon;
                                    }
                                }
                                break;
                            case ExpectedRollStage.DiceIcon:
                                if (Payload.Type == PayloadType.Icon)
                                {
                                    if (((IconPayload)Payload).Icon == BitmapFontIcon.Dice)
                                    {

                                        //Dalamud.Logging.PluginLog.Log("Found dice icon");
                                        CurrentStage = ExpectedRollStage.Value;
                                    }
                                }
                                break;
                            case ExpectedRollStage.Value:
                                if (Payload.Type == PayloadType.RawText)
                                {
                                    uint MaybeValue = 0;
                                    if (UInt32.TryParse(((TextPayload)Payload).Text, out MaybeValue))
                                    {
                                        //Dalamud.Logging.PluginLog.Log($"Found value: {MaybeValue}");
                                        RollValue = MaybeValue;
                                    }
                                    else
                                    {
                                        //Dalamud.Logging.PluginLog.Log($"Didn't find value in: {((TextPayload)Payload).Text}");
                                    }
                                    CurrentStage = ExpectedRollStage.Done;
                                }
                                break;
                        }
                    }
                    //Finally, if we have a roll, add it.
                    if (RollValue != null)
                    {
                        //Dalamud.Logging.PluginLog.Log($"Roll added: {RollValue} : {Player}");
                        PlayerRolls.Add(new Roll { Player = Player, Value = (uint)RollValue });
                    }
                    //...phew
                }
            };
            //Run the task
            await Task.Run(async () => {
                try {
                    await MessageSource.WriteAsync(ChatTarget + ThisGame.Name, TaskCancellationToken);
                    await MessageSource.WriteAsync(ChatTarget + ThisGame.Tagline, TaskCancellationToken);
                    await MessageSource.WriteAsync(ChatTarget + $"Use {ChatCommand} to play!", TaskCancellationToken);
                    await MessageSource.WriteAsync(ChatTarget + SepText, TaskCancellationToken);

                    if (ThisGame.HighestAction.Active && ThisGame.HighestAction.Advertise)
                    {
                        await MessageSource.WriteAsync(ChatTarget + ThisGame.HighestAction.Advertisment, TaskCancellationToken);
                    }
                    if (ThisGame.LowestAction.Active && ThisGame.LowestAction.Advertise)
                    {
                        await MessageSource.WriteAsync(ChatTarget + ThisGame.LowestAction.Advertisment, TaskCancellationToken);
                    }
                    foreach (var SpecialRoll in ThisGame.AdditionalActions)
                    {
                        if (SpecialRoll.Action.Active && SpecialRoll.Action.Advertise)
                        {
                            await MessageSource.WriteAsync(ChatTarget + $"On {SpecialRoll.Value}: {SpecialRoll.Action.Advertisment}", TaskCancellationToken);
                        }
                    }
                    await MessageSource.WriteAsync(ChatTarget + StartText, TaskCancellationToken);
                    await MessageSource.WriteAsync(ChatTarget + ChatCommand, TaskCancellationToken);

                    ChatGui.ChatMessage += OnChatMessage;
                    try
                    {
                        await MessageSource.WriteAsync(ChatCommand, TaskCancellationToken); //Roll for yourself
                        await Task.Delay((int)ThisGame.Length * 1000, TaskCancellationToken);
                    }
                    finally
                    {
                        ChatGui.ChatMessage -= OnChatMessage;
                    }

                    await MessageSource.WriteAsync(ChatTarget + EndText, TaskCancellationToken);
                    if (PlayerRolls.Count == 0)
                    {
                        //Dalamud.Logging.PluginLog.Log("No Results!");
                    }
                    else
                    {
                        //Remove duplicate rolls for the same player, eventually I want to support a number of behaviours, hence the sub-optimal algorithm.
                        List<Roll> FilteredRolls = new List<Roll>();
                        foreach (Roll CandidateRoll in PlayerRolls)
                        {
                            if (FilteredRolls.FindIndex((Roll Existing) => { return Existing.Player == CandidateRoll.Player; }) == -1)
                            {
                                FilteredRolls.Add(CandidateRoll);
                            }
                        }
                        //Sort is unstable, may need changing.
                        FilteredRolls.Sort((Roll A, Roll B) => { return A.Value.CompareTo(B.Value); });
                        //Since we know we have at least one.
                        Roll Highest = FilteredRolls[FilteredRolls.Count - 1];
                        Roll Lowest = FilteredRolls[0];

                        if (ThisGame.HighestAction.Active)
                        {
                            await MessageSource.WriteAsync(ChatTarget + $"{Highest.Player} rolled Highest with {Highest.Value}: {ThisGame.HighestAction.Action}", TaskCancellationToken);
                        }
                        if (ThisGame.LowestAction.Active)
                        {
                            await MessageSource.WriteAsync(ChatTarget + $"{Lowest.Player} rolled Lowest with {Lowest.Value}: {ThisGame.LowestAction.Action}", TaskCancellationToken);
                        }

                        foreach (var SpecialRoll in ThisGame.AdditionalActions)
                        {
                            if (SpecialRoll.Action.Active)
                            {
                                uint Value = SpecialRoll.Value;
                                foreach (Roll FilteredRoll in FilteredRolls)
                                {
                                    if (FilteredRoll.Value == Value)
                                    {
                                        await MessageSource.WriteAsync(ChatTarget + $"{FilteredRoll.Player} rolled {Value}! {SpecialRoll.Action.Action}", TaskCancellationToken);
                                    }
                                    //No break, in case I decide not to sort the list and it messes things up!
                                }
                            }
                        }
                    }
                    await MessageSource.WriteAsync(ChatTarget + SepText, TaskCancellationToken);
                }
                catch(OperationCanceledException)
                {
                    //Ignore 
                }
            });
        }
    }
}

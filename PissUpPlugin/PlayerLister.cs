using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
/*
namespace PissUpPlugin
{
    using MessageAction = Func<String, CancellationToken, Task>;

    namespace Games
    {
        [Serializable]
        class PlayerLister : IGame
        {
            public string GetFriendlyName() { return "Player List"; }

            public string Name { get; set; } = "Game Name";
            public string Tagline { get; set; } = "Tagline";
            public uint EntryLength { get; set; }

            
            public List<string> Items { get; set; } = new List<string> { };

            public void DrawConfig(uint GameCount)
            {
                uint ActionCount = 0;

                {
                    string GameName = Name;
                    if (ImGui.InputText($"Game Name###gamename{GameCount}", ref GameName, PluginUI.TextLength))
                    {
                        Name = GameName;
                    }
                }
                {
                    string GameTagline = Tagline;
                    if(ImGui.InputText($"Tagline###tagline{GameCount}", ref GameTagline, PluginUI.TextLength))
                    {
                        Tagline = GameTagline;
                    }
                }

                {
                    int NewDiceValue = (int)DiceValue;
                    bool ValueUsed = NewDiceValue > 0;
                    if (ImGui.Checkbox($"Dice Value###dicevalueb{GameCount}", ref ValueUsed))
                    {
                        DiceValue = ValueUsed ? 500 : 0;
                    }
                    if (ValueUsed)
                    {
                        ImGui.SameLine();
                        if (ImGui.SliderInt($"Dice Value###dicevaluei{GameCount}", ref NewDiceValue, 2, 999))
                        {
                            DiceValue = (uint)NewDiceValue;
                        }
                    }
                }

                {
                    int GameLength = (int)Length;
                    if (ImGui.SliderInt($"Game Length (seconds)###len{GameCount}", ref GameLength, 10, 180))
                    {
                        Length = (uint)GameLength;
                    }
                }

                // public uint FinalCountdown { get; set; } = 5; //Not implemented yet
                // public uint RepeatReminder { get; set; } = 300; //Not implemented yet

                bool RenderAction(ref ActionInfo Action)
                {
                    bool ActionChanged = false;
                    {
                        bool Active = Action.Active;
                        if (ImGui.Checkbox($"Use Action##actb{ActionCount}", ref Active))
                        {
                            ActionChanged = true;
                            Action.Active = Active;
                        }
                        ImGui.SameLine();
                        string ActionText = Action.Action;
                        if (ImGui.InputText($"Text###acts{ActionCount}", ref ActionText, PluginUI.TextLength))
                        {
                            ActionChanged = true;
                            Action.Action = ActionText;
                        }
                    }
                    {
                        bool Advertise = Action.Advertise;
                        if (ImGui.Checkbox($"Advertisement###advb{ActionCount}", ref Advertise))
                        {
                            ActionChanged = true;
                            Action.Advertise = Advertise;
                        }
                        ImGui.SameLine();
                        string Advertisment = Action.Advertisment;
                        if (ImGui.InputText($"Text###advs{ActionCount}", ref Advertisment, PluginUI.TextLength))
                        {
                            ActionChanged = true;
                            Action.Advertisment = Advertisment;
                        }
                    }
                    ++ActionCount;
                    return ActionChanged;
                }
                ImGui.Separator();
                {
                    ImGui.Text("Highest Roll Action");
                    ActionInfo GameHighestAction = HighestAction;
                    if (RenderAction(ref GameHighestAction))
                    {
                        HighestAction = GameHighestAction;
                    }
                }
                {
                    ImGui.Text("Lowest Roll Action");
                    ActionInfo GameLowestAction = LowestAction;
                    if (RenderAction(ref GameLowestAction))
                    {
                        LowestAction = GameLowestAction;
                    }
                }
                ImGui.Separator();
                ImGui.Text("Specific Value Actions");
                ImGui.SameLine();
                if (ImGui.Button($"Add Action###addex{GameCount}"))
                {
                    AdditionalActions.Add(new ExtraActionInfo
                    {
                        Value = 0,
                        Action = new ActionInfo
                        {
                            Active = false,
                            Action = "You do X",
                            Advertise = false,
                            Advertisment = "On Y, you do X"
                        },
                    });
                }
                for (int i = 0; i < AdditionalActions.Count; ++i)
                {
                    bool ExtraActionChanged = false;
                    ExtraActionInfo ExtraAction = AdditionalActions[i];
                    int ExtraActionValue = (int)ExtraAction.Value;
                    if (ImGui.InputInt($"Dice Value###dvex{ActionCount}", ref ExtraActionValue))
                    {
                        ExtraActionChanged = true;
                        if (ExtraActionValue >= 0 && ExtraActionValue <= 999)
                        {
                            ExtraAction.Value = (uint)ExtraActionValue;
                        }
                    }
                    ImGui.SameLine();
                    if (ImGui.Button($"Remove###remex{ActionCount}"))
                    {
                        //ExtraActionChanged not needed.
                        AdditionalActions.RemoveAt(i);
                        break; //Just don't render any more extra actions this frame.
                    }
                    //Nothing later breaks, so we can set ValueCahnged based on ExtraActionChanged
                    ActionInfo ExtraActionAction = ExtraAction.Action;
                    if (RenderAction(ref ExtraActionAction))
                    {
                        ExtraActionChanged = true;
                        ExtraAction.Action = ExtraActionAction;
                    }

                    if (ExtraActionChanged)
                    {
                        AdditionalActions[i] = ExtraAction;
                    }
                }
                ++GameCount;
            }

            private struct Player
            {
                public string Name;
                public bool IsAFK;
                public uint LastTurn;
                public uint RollValue;
            }

            private List<Player> Players = new List<Player> { };
            private uint CurrentTurn = 0;

            //A roll is expected to and (optional) range rolled, a dice icon, and a value.
            private enum ExpectedRollStage
            {
                RollLimit,
                DiceIcon,
                Value,
                Done
            }

            const string SepText = "-----------------------------";
            const string StartText = "-----------New Round---------";
            const string EndText = "-----------Round Over--------";
            public async Task Run(Plugin GamePlugin, CancellationToken TaskCancellationToken, MessageAction SendMessage)
            {
                //Set up any variables we'll need in the task. 
                bool IsAlliance = GamePlugin.Configuration.IsAlliance;
                //PartyList.IsAlliance doesn't work, so we need to config this.
                string ChatTarget = IsAlliance ? "/a " : "/p "; //end in a space.
                string ChatCommand = IsAlliance ? "/dice alliance" : "/dice party";
                //Set up our chat message delegate ready for attaching.
                Players = new List<Player>();
                ExpectedRollStage InitialRollStage = ExpectedRollStage.RollLimit;
                Regex NumberRe = new Regex("^[\\D]+$" ); //Abuse this to check for a string with no values.
                ChatGui.OnMessageDelegate OnChatMessage = (XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
                =>
                {
                    //Assumption: all the chat types we're interested in are mutually exclusive.
                    if (type == XivChatType.Alliance || type == XivChatType.Party || type == XivChatType.CrossParty)
                    {
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
                            return;
                        }
                        //See if we've got a dice roll:
                        ExpectedRollStage CurrentStage = InitialRollStage;
                        uint? RollValue = null;
                        foreach (var Payload in message.Payloads)
                        {
                            if (CurrentStage == ExpectedRollStage.Done)
                            {
                                break;
                            }
                            switch (CurrentStage)
                            {
                                case ExpectedRollStage.RollLimit:
                                    if (Payload.Type == PayloadType.RawText)
                                    {
                                        if (NumberRe.IsMatch(((TextPayload)Payload).Text))
                                        {
                                            CurrentStage = ExpectedRollStage.DiceIcon;
                                        }
                                    }
                                    break;
                                case ExpectedRollStage.DiceIcon:
                                    if (Payload.Type == PayloadType.Icon)
                                    {
                                        if (((IconPayload)Payload).Icon == BitmapFontIcon.Dice)
                                        {
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
                                            RollValue = MaybeValue;
                                        }
                                        CurrentStage = ExpectedRollStage.Done;
                                    }
                                    break;
                            }
                        }
                        //Finally, if we have a roll, add it.
                        if (RollValue != null)
                        {
                            Players.Add(new Player { Name = Player, RollValue = (uint)RollValue, LastTurn = 0, IsAFK = false });
                        }
                        //...phew
                    }
                };
                //Run the task
                await Task.Run(async () =>
                {
                    try
                    {
                        await SendMessage(ChatTarget + Name, TaskCancellationToken);
                        await SendMessage(ChatTarget + Tagline, TaskCancellationToken);
                        await SendMessage(ChatTarget + $"Use {ChatCommand} to play!", TaskCancellationToken);
                        await SendMessage(ChatTarget + SepText, TaskCancellationToken);

                        if (HighestAction.Active && HighestAction.Advertise)
                        {
                            await SendMessage(ChatTarget + HighestAction.Advertisment, TaskCancellationToken);
                        }
                        if (LowestAction.Active && LowestAction.Advertise)
                        {
                            await SendMessage(ChatTarget + LowestAction.Advertisment, TaskCancellationToken);
                        }
                        foreach (var SpecialRoll in AdditionalActions)
                        {
                            if (SpecialRoll.Action.Active && SpecialRoll.Action.Advertise)
                            {
                                await SendMessage(ChatTarget + $"On {SpecialRoll.Value}: {SpecialRoll.Action.Advertisment}", TaskCancellationToken);
                            }
                        }
                        await SendMessage(ChatTarget + StartText, TaskCancellationToken);
                        await SendMessage(ChatTarget + ChatCommand, TaskCancellationToken);

                        Plugin.ChatGui.ChatMessage += OnChatMessage;
                        try
                        {
                            await SendMessage(ChatCommand, TaskCancellationToken); //Roll for yourself
                            await Task.Delay((int)Length * 1000, TaskCancellationToken);
                        }
                        finally
                        {
                            Plugin.ChatGui.ChatMessage -= OnChatMessage;
                        }

                        await SendMessage(ChatTarget + EndText, TaskCancellationToken);
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

                            if (HighestAction.Active)
                            {
                                await SendMessage(ChatTarget + $"{Highest.Player} rolled Highest with {Highest.Value}: {HighestAction.Action}", TaskCancellationToken);
                            }
                            if (LowestAction.Active)
                            {
                                await SendMessage(ChatTarget + $"{Lowest.Player} rolled Lowest with {Lowest.Value}: {LowestAction.Action}", TaskCancellationToken);
                            }

                            foreach (var SpecialRoll in AdditionalActions)
                            {
                                if (SpecialRoll.Action.Active)
                                {
                                    uint Value = SpecialRoll.Value;
                                    foreach (Roll FilteredRoll in FilteredRolls)
                                    {
                                        if (FilteredRoll.Value == Value)
                                        {
                                            await SendMessage(ChatTarget + $"{FilteredRoll.Player} rolled {Value}! {SpecialRoll.Action.Action}", TaskCancellationToken);
                                        }
                                        //No break, in case I decide not to sort the list and it messes things up!
                                    }
                                }
                            }
                        }
                        await SendMessage(ChatTarget + SepText, TaskCancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        //Ignore 
                    }
                });
            }

        }
    }
}
*/
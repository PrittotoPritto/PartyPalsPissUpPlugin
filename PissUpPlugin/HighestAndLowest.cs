using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PissUpPlugin
{
    using MessageAction = Func<String, CancellationToken, Task>;

    namespace Games
    {
        static class Common
        {
            public static string GetChatPrefix(SendTarget target, UInt16 number = 0)
            {
                //end in a space
                switch (target)
                {
                    case SendTarget.Party:
                        return "/p ";
                    case SendTarget.Alliance:
                        return "/a ";
                    case SendTarget.CWLS:
                        Debug.Assert(number != 0);
                        return "/cwlinkshell{0} ".Format(number);
                    case SendTarget.Say:
                        return "/say ";
                    case SendTarget.Yell:
                        return "/yell ";
                    default:
                        Debug.Assert(false);
                        goto case SendTarget.Party;
                }
            }

            public static string GetChatRollInstruction(SendTarget target)
            {
                switch (target)
                {
                    case SendTarget.Party:
                        return "/dice party";
                    case SendTarget.Alliance:
                        return "/dice alliance";
                    case SendTarget.CWLS:
                        return "/dice cwlinkshellX (replace X with the number)";
                    case SendTarget.Say:
                        return "/random";
                    case SendTarget.Yell:
                        return "/random"; //submit in public channels
                    default:
                        Debug.Assert(false);
                       goto case SendTarget.Party;
                }
            }
            public static string GetOwnChatRoll(SendTarget target, UInt16 number = 0 )
            {
                switch (target)
                {
                    case SendTarget.Party:
                        return "/dice party";
                    case SendTarget.Alliance:
                        return "/dice alliance";
                    case SendTarget.CWLS:
                        Debug.Assert(number > 0);
                        return "/dice cwlinkshell{0}".Format(number);
                    case SendTarget.Yell:
                        return "/random";
                    case SendTarget.Say:
                        return "/random";
                    default:
                        Debug.Assert(false);
                        goto case SendTarget.Party;
                }
            }
            public static Func<XivChatType, bool> GetMessageTypeFilter(SendTarget target, UInt16 number = 0)
            {
                switch (target)
                {
                    case SendTarget.CWLS:
                        XivChatType CWLSType = ((Func<XivChatType>)(() =>
                        {
                            switch (number)
                            {
                                case 1:
                                    return XivChatType.CrossLinkShell1;
                                case 2:
                                    return XivChatType.CrossLinkShell2;
                                case 3:
                                    return XivChatType.CrossLinkShell3;
                                case 4:
                                    return XivChatType.CrossLinkShell4;
                                case 5:
                                    return XivChatType.CrossLinkShell5;
                                case 6:
                                    return XivChatType.CrossLinkShell6;
                                case 7:
                                    return XivChatType.CrossLinkShell7;
                                case 8:
                                    return XivChatType.CrossLinkShell8;
                                default:
                                    Debug.Assert(false);
                                    goto case 1;
                            }
                        }))();
                        return (XivChatType type) => { return type == CWLSType; };
                    case SendTarget.Say:
                    case SendTarget.Yell:
                        return (XivChatType type) => { return type == XivChatType.Yell || type == XivChatType.Shout || type == XivChatType.Say; };
                    case SendTarget.Party:
                    case SendTarget.Alliance:
                        return (XivChatType type) => { return type == XivChatType.Alliance || type == XivChatType.Party || type == XivChatType.CrossParty; };
                    default:
                        Debug.Assert(false);
                        goto case SendTarget.Party;
                }
            }
        }

        [Serializable]
        class HighestAndLowest : IGame
        {
            public string GetFriendlyName() { return "Highest and Lowest"; }

            public string Name { get; set; } = "Game Name";
            public string Tagline { get; set; } = "Tagline";
            public string Outro { get; set; } = "";
            public uint DiceValue { get; set; }
            public uint Length { get; set; }
            public uint FinalCountdown { get; set; }
            public uint RepeatReminder { get; set; }

            [Serializable]
            public struct ActionInfo
            {
                public bool Active { get; set; }
                public string Action { get; set; }
                public bool Advertise { get; set; }
                public string Advertisment { get; set; }
            }
            public ActionInfo HighestAction { get; set; }
            public ActionInfo LowestAction { get; set; }

            [Serializable]
            public struct ExtraActionInfo
            {
                public uint Value { get; set; }
                public ActionInfo Action { get; set; }
            }
            public List<ExtraActionInfo> AdditionalActions { get; set; } = new List<ExtraActionInfo> { };

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
                    string GameOutro = Outro;
                    if (ImGui.InputText($"Outro###outro{GameCount}", ref GameOutro, PluginUI.TextLength))
                    {
                        Outro = GameOutro;
                    }
                }
                {
                    int NewDiceValue = (int)DiceValue;
                    bool ValueUsed = NewDiceValue > 0;
                    if (ImGui.Checkbox($"Dice Value###dicevalueb{GameCount}", ref ValueUsed))
                    {
                        DiceValue = ValueUsed ? 500u : 0u;
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


            private struct Roll
            {
                public string Player;
                public uint Value;
            }
            //A roll is expected to and (optional) range rolled, a dice icon, and a value.
            private enum ExpectedRollStage
            {
                //TranslateBegin,
                RollLimit,
                DiceIcon,
                Value,
                //TranslateEnd,
                Done
            }

            const string SepText = "-----------------------------";
            const string StartText = "-----------New Round---------";
            const string EndText = "-----------Round Over--------";
            public async Task Run(Plugin GamePlugin, CancellationToken TaskCancellationToken, MessageAction SendMessage)
            {
                //Set up any variables we'll need in the task. 
                SendTarget TargetChat = GamePlugin.Configuration.TargetChat;
                UInt16 CWLSNumber = GamePlugin.Configuration.CWLSNumber;
                //PartyList.IsAlliance doesn't work, so we need to config this.
                string ChatTarget = Common.GetChatPrefix(TargetChat, CWLSNumber);
                string NumberAddition = DiceValue > 0 ? $" {DiceValue}" : ""; //leading space if it exists
                string ChatInstruction = Common.GetChatRollInstruction(TargetChat) + NumberAddition;
                string ChatCommand = Common.GetOwnChatRoll(TargetChat, CWLSNumber) + NumberAddition;
                //Filter for incoming chat messages based on their type:
                Func<XivChatType, bool> TypeFilter = Common.GetMessageTypeFilter(TargetChat, CWLSNumber);
                //Set up our chat message delegate ready for attaching.
                List<Roll> PlayerRolls = new List<Roll>();
                ExpectedRollStage InitialRollStage = ExpectedRollStage.RollLimit;
                Regex NumberRe = new Regex(DiceValue > 0 ? $"\\D+{DiceValue}\\D+" : "^\\D*$");
                IChatGui.OnMessageDelegate OnChatMessage = (XivChatType type, int senderId, ref SeString sender, ref SeString message, ref bool isHandled)
                =>
                {
                    //Assumption: all the chat types we're interested in are mutually exclusive.
                    if (TypeFilter(type))
                    {
                        /*
                        Dalamud.Logging.PluginLog.Log($"Sender: {sender.ToString()}");
                        foreach (var payload in sender.Payloads)
                        {
                            Dalamud.Logging.PluginLog.Log($"Sender Payload: {payload.ToString()}");
                        }
                        Dalamud.Logging.PluginLog.Log($"Value: {message.ToString()}");
                        foreach (var payload in message.Payloads)
                        {
                            Dalamud.Logging.PluginLog.Log($"Payload: {payload.ToString()}");
                        }
                        Dalamud.Logging.PluginLog.Log($"Value: {message.ToString()}");
                        //*/
                        //Get the player name:
                        string? Player = null;
                        foreach (var PossiblePlayer in sender.Payloads)
                        {
                            if (PossiblePlayer.Type == PayloadType.Player)
                            {
                                Player = ((PlayerPayload)PossiblePlayer).PlayerName;
                                break;
                            }
                        }
                        if (Player == null)
                        {
                            string? PlayerText = null;
                            foreach (var PossiblePlayer in sender.Payloads)
                            {//Ok, this is dirty!
                                if (PossiblePlayer.Type == PayloadType.RawText)
                                {
                                    string? NewText = ((TextPayload)PossiblePlayer).Text;
                                    if (NewText != null && NewText.Contains(' ') && (PlayerText == null || NewText.Length > PlayerText.Length))
                                    {
                                        PlayerText = NewText;
                                    }
                                }
                            }
                            Player = PlayerText;
                        }
                        if (Player == null)
                        {
                            return;
                        }
                        // Dalamud.Logging.PluginLog.Log($"Player: {Player}");
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
                await Task.Run(async () =>
                {
                    try
                    {
                        await SendMessage(ChatTarget + Name, TaskCancellationToken);
                        await SendMessage(ChatTarget + Tagline, TaskCancellationToken);
                        await SendMessage(ChatTarget + $"Use '{ChatInstruction}' to play!", TaskCancellationToken);
                        await SendMessage(ChatTarget + SepText, TaskCancellationToken);

                        if (HighestAction.Active && HighestAction.Advertise)
                        {
                            await SendMessage(ChatTarget + "Highest Roll: "+ HighestAction.Advertisment, TaskCancellationToken);
                        }
                        if (LowestAction.Active && LowestAction.Advertise)
                        {
                            await SendMessage(ChatTarget + "Lowest Roll: " + LowestAction.Advertisment, TaskCancellationToken);
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
                        if (Outro.Length > 0)
                        {
                            await SendMessage(ChatTarget + Outro, TaskCancellationToken);
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
using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace PissUpPlugin
{
    public enum SendTarget
    {
        Party,
        Alliance,
        CWLS,
        Say,
        Yell,
    }

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        public SendTarget TargetChat { get; set; } = SendTarget.Party;
        public UInt16 CWLSNumber { get; set; } = 1;

        public IGame CurrentGame = (IGame)new Games.HighestAndLowest
        {
            Name = "THE BEST GAME EVER!",
            Tagline = "Roll and drink, with extras!",
            DiceValue = 0,
            Length = 60,
            FinalCountdown = 5,
            RepeatReminder = 300,
            HighestAction = new Games.HighestAndLowest.ActionInfo
            {
                Active = true,
                Action = "Ask the lowest roller a question!",
                Advertise = true,
                Advertisment = "Highest roll gets to ask the lowest roller a question!"
            },
            LowestAction = new Games.HighestAndLowest.ActionInfo
            {
                Active = true,
                Action = "You are the lowest roller!",
                Advertise = false,
                Advertisment = "Lowest roller gets asked!"
            },
            AdditionalActions = new List<Games.HighestAndLowest.ExtraActionInfo>()
        }; //So we can have more games later.

        public string SavePath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

        // the below exist just to make saving less cumbersome

        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }
    }
}

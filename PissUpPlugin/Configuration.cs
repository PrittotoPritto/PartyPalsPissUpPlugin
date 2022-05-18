using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace PissUpPlugin
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool IsAlliance { get; set; } = false;

        [Serializable]
        public struct Game
        {
            public string Name { get; set; }
            public string Tagline { get; set; }
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
            public List<ExtraActionInfo> AdditionalActions { get; set; }
        }
        public Game CurrentGame = new Game {
            Name = "THE BEST GAME EVER!",
            Tagline = "Roll and drink, with extras!",
            DiceValue = 0,
            Length = 60,
            FinalCountdown = 5,
            RepeatReminder = 300,
            HighestAction = new Game.ActionInfo
            {
                Active = true,
                Action = "Ask the lowest roller a question!",
                Advertise = true,
                Advertisment = "Highest roll gets to ask the lowest roller a question!"
            },
            LowestAction = new Game.ActionInfo
            {
                Active = true,
                Action = "You are the lowest roller!",
                Advertise = false,
                Advertisment = "Lowest roller gets asked!"
            },
            AdditionalActions = new List<Game.ExtraActionInfo>()
        }; //So we can have more games later.

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

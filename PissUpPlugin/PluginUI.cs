using ImGuiNET;
using System;
using System.Numerics;

namespace PissUpPlugin
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        private Configuration configuration;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        // passing in the image here just for simplicity
        public PluginUI(Configuration configuration)
        {
            this.configuration = configuration;
        }

        public void Dispose()
        {
        }

        public void Draw()
        {
            // This is our only draw handler attached to UIBuilder, so it needs to be
            // able to draw any windows we might have open.
            // Each method checks its own visibility/state to ensure it only draws when
            // it actually makes sense.
            // There are other ways to do this, but it is generally best to keep the number of
            // draw delegates as low as possible.

            DrawMainWindow();
        }

        public void DrawMainWindow()
        {
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(375, 400), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(375, 400), new Vector2(float.MaxValue, float.MaxValue));

            if (ImGui.Begin("Prittoto Pritto's Party Pals Pissup Plugin <3", ref this.visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                //ImGui.Text($"The random config bool is {this.configuration.SomePropertyToBeSavedAndWithADefault}");
                ImGui.Text("We've just got the one game for now, but having multiple and saving/loading would be nice.");
                ImGui.Spacing();

                bool IsAlliance = configuration.IsAlliance;
                ImGui.Checkbox("Use Alliance Chat", ref IsAlliance);
                if (IsAlliance != configuration.IsAlliance)
                {
                    configuration.IsAlliance = IsAlliance;
                }

                uint GameCount = 0;
                uint ActionCount = 0;
                bool RenderGame(ref Configuration.Game Game)
                {
                    bool ValueChanged = false;
                    {
                        string GameName = Game.Name;
                        if (ImGui.InputText($"Game Name###gamename{GameCount}", ref GameName, 50))
                        {
                            ValueChanged = true;
                            Game.Name = GameName;
                        }
                    }
                    {
                        string Tagline = Game.Tagline;
                        if (ImGui.InputText($"Tagline###tagline{GameCount}", ref Tagline, 50))
                        {
                            ValueChanged = true;
                            Game.Tagline = Tagline;
                        }
                    }

                    {
                        int DiceValue = (int)Game.DiceValue;
                        bool ValueUsed = DiceValue > 0;
                        if (ImGui.Checkbox($"Dice Value###dicevalueb{GameCount}", ref ValueUsed))
                        {
                            ValueChanged = true;
                            DiceValue = ValueUsed ? 500 : 0;
                            Game.DiceValue = (uint)DiceValue;
                        }
                        if (ValueUsed)
                        {
                            ImGui.SameLine();
                            if (ImGui.SliderInt($"Dice Value###dicevaluei{GameCount}", ref DiceValue, 2, 999))
                            {
                                ValueChanged = true;
                                Game.DiceValue = (uint)DiceValue;
                            }
                        }
                    }

                    {
                        int Length = (int)Game.Length;
                        if (ImGui.SliderInt($"Game Length (seconds)###len{GameCount}", ref Length, 10, 180))
                        {
                            ValueChanged = true;
                            Game.Length = (uint)Length;
                        }
                    }

                    // public uint FinalCountdown { get; set; } = 5; //Not implemented yet
                    // public uint RepeatReminder { get; set; } = 300; //Not implemented yet

                    bool RenderAction(ref Configuration.Game.ActionInfo Action)
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
                            if (ImGui.InputText($"Text###acts{ActionCount}", ref ActionText, 50))
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
                            if (ImGui.InputText($"Text###advs{ActionCount}", ref Advertisment, 50))
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
                        Configuration.Game.ActionInfo HighestAction = Game.HighestAction;
                        if (RenderAction(ref HighestAction))
                        {
                            ValueChanged = true;
                            Game.HighestAction = HighestAction; 
                        }
                    }
                    {
                        ImGui.Text("Lowest Roll Action");
                        Configuration.Game.ActionInfo LowestAction = Game.LowestAction;
                        if (RenderAction(ref LowestAction))
                        {
                            ValueChanged = true;
                            Game.HighestAction = LowestAction;
                        }
                    }
                    ImGui.Separator();
                    ImGui.Text("Specific Value Actions");
                    ImGui.SameLine();
                    if (ImGui.Button($"Add Action###addex{GameCount}"))
                    {
                        ValueChanged = true;
                        Game.AdditionalActions.Add(new Configuration.Game.ExtraActionInfo
                        {
                            Value = 0,
                            Action = new Configuration.Game.ActionInfo{
                                Active = false,
                                Action = "You do X",
                                Advertise = false,
                                Advertisment = "On Y, you do X"
                            },
                        }) ;
                    }
                    for (int i = 0; i < Game.AdditionalActions.Count; ++i)
                    {
                        bool ExtraActionChanged = false;
                        Configuration.Game.ExtraActionInfo ExtraAction = Game.AdditionalActions[i];
                        int ExtraActionValue = (int)ExtraAction.Value;
                        if (ImGui.InputInt($"Dice Value###dvex{ActionCount}", ref ExtraActionValue))
                        {
                            ValueChanged = true;
                            ExtraActionChanged = true;
                            if(ExtraActionValue >= 0 && ExtraActionValue <= 999)
                            {
                                ExtraAction.Value = (uint)ExtraActionValue;
                            }
                        }
                        ImGui.SameLine();
                        if (ImGui.Button($"Remove###remex{ActionCount}"))
                        {
                            ValueChanged = true;
                            //ExtraActionChanged not needed.
                            Game.AdditionalActions.RemoveAt(i);
                            break; //Just don't render any more extra actions this frame.
                        }
                        //Nothing later breaks, so we can set ValueCahnged based on ExtraActionChanged
                        Configuration.Game.ActionInfo ExtraActionAction = ExtraAction.Action;
                        if (RenderAction(ref ExtraActionAction))
                        {
                            ExtraActionChanged = true;
                            ExtraAction.Action = ExtraActionAction;
                        }

                        if (ExtraActionChanged)
                        {
                            ValueChanged = true;
                            Game.AdditionalActions[i] = ExtraAction;
                        }
                    }
                    ++GameCount;
                    return ValueChanged;
                }
                Configuration.Game Game = configuration.CurrentGame;
                if (RenderGame(ref Game))
                {
                    configuration.CurrentGame = Game;
                }
                ImGui.Separator();
                if (ImGui.Button("Save Plugin Config"))
                {
                    configuration.Save();
                }
            }
            ImGui.End();
        }
    }
}

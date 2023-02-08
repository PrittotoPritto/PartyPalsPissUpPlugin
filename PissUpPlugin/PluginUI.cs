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

        private string Error = "";
        private DateTime ErrorDisplay = DateTime.MinValue;

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

        public const int TextLength = 100;
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
                ImGui.Text("We've just got the one game for now, but having multiple would be nice, but at least I got save/load working below!");
                ImGui.Spacing();

                SendTarget TargetChat = configuration.TargetChat;
                ImGui.Text("Target Chat"); ImGui.SameLine();
                if (ImGui.RadioButton("Party", TargetChat == SendTarget.Party))
                {
                    TargetChat = SendTarget.Party;
                }
                ImGui.SameLine();
                if (ImGui.RadioButton("Alliance", TargetChat == SendTarget.Alliance))
                {
                    TargetChat = SendTarget.Alliance;
                }
                ImGui.SameLine();
                if (ImGui.RadioButton("CWLS", TargetChat == SendTarget.CWLS))
                {
                    TargetChat = SendTarget.CWLS;
                }

                ImGui.SameLine();
                int CWLSNumber = configuration.CWLSNumber;
                ImGui.PushItemWidth(80);
                ImGui.InputInt("CWLS Num", ref CWLSNumber, 1);
                if (CWLSNumber != configuration.CWLSNumber)
                {
                    //At  time of writing, the XivChatType only goes from CWLS1-CWLS8
                    if (CWLSNumber >= 0 && CWLSNumber <= 8)
                    {
                        configuration.CWLSNumber = (UInt16)CWLSNumber;
                    }
                }
                ImGui.PopItemWidth();

                /*
                ImGui.Text("Untested public chat options: ");
                ImGui.SameLine();
                if (ImGui.RadioButton("Yell", TargetChat == SendTarget.Yell))
                {
                    TargetChat = SendTarget.Yell;
                }
                ImGui.SameLine();
                if (ImGui.RadioButton("Say", TargetChat == SendTarget.Say))
                {
                    TargetChat = SendTarget.Say;
                }
                if (TargetChat != configuration.TargetChat)
                {
                    configuration.TargetChat = TargetChat;
                }
                */

                ImGui.Separator();
                uint GameCount = 0;
                configuration.CurrentGame.DrawConfig(GameCount);
                //++GameCount;

                ImGui.Separator();
                if (ErrorDisplay > DateTime.Now)
                {
                    ImGui.Text(Error);
                }
                else
                {
                    string Path = configuration.SavePath;
                    if (ImGui.InputText("File Path", ref Path, 300, ImGuiInputTextFlags.None))
                    {
                        configuration.SavePath = Path;
                    }
                    if (ImGui.Button("Save"))
                    {
                        try
                        {
                            JSONSerialisation.SaveFile(configuration.SavePath, configuration.CurrentGame);
                            throw new Exception("Save Successful");
                        }
                        catch (Exception e)
                        {
                            Error = e.Message;
                            ErrorDisplay = DateTime.Now + TimeSpan.FromSeconds(3);
                        }
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Load"))
                    {
                        try
                        {
                            IGame? NewGame = JSONSerialisation.LoadFile(configuration.SavePath);
                            if (NewGame != null)
                            {
                                configuration.CurrentGame = NewGame;
                                throw new Exception("Load Successful");
                            }
                            else
                            {
                                throw new Exception("Could not load game");
                            }
                        }
                        catch (Exception e)
                        {
                            Error = e.Message;
                            ErrorDisplay = DateTime.Now + TimeSpan.FromSeconds(3);
                        }
                    }
                }

            }

            ImGui.Separator();
            if (ImGui.Button("Save Plugin Config"))
            {
                configuration.Save();
            }

            ImGui.End();
        }
    }
}

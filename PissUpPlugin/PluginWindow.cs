using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace PissUpPlugin
{
    class PluginWindow : Window, IDisposable
    {
        private Configuration configuration;
        public FileDialogManager fileDialogManager = new();

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private string Error = "";
        private DateTime ErrorDisplay = DateTime.MinValue;

        // passing in the image here just for simplicity
        public PluginWindow(Configuration configuration)
            : base("Prittoto Pritto's Party Pals Pissup Plugin <3", ImGuiWindowFlags.None)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(375, 400),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
            this.configuration = configuration;
        }

        public void Dispose()
        {
        }

        public const int TextLength = 100;

        override public void Draw()
        {
            fileDialogManager.Draw();

            ImGui.Text("We've just got the one game type for now, maybe I'll get round to finishing other types in the future!");
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
            */
            if (TargetChat != configuration.TargetChat)
            {
                configuration.TargetChat = TargetChat;
            }

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
                    TrySave();
                }
                ImGui.SameLine();
                if (ImGuiComponents.IconButton("IconSave", FontAwesomeIcon.Download))
                {
                    fileDialogManager.SaveFileDialog("Save ruleset", ".json", configuration.SavePath, ".json", (bool chosen, string path) =>
                    {
                        if (chosen)
                        {
                            configuration.SavePath = path;
                            TrySave();
                        }
                    });
                }
                ImGui.SameLine();
                if (ImGui.Button("Load"))
                {
                    TryLoad();
                }
                ImGui.SameLine();
                if (ImGuiComponents.IconButton("IconLoad", FontAwesomeIcon.FileUpload))
                {
                    fileDialogManager.OpenFileDialog("Load ruleset", ".json", (bool chosen, string path) =>
                    {
                        if (chosen)
                        {
                            configuration.SavePath = path;
                            TryLoad();
                        }
                    });
                }
            }
            ImGui.Separator();
            if (ImGui.Button("Save Plugin Config"))
            {
                configuration.Save();
            }

            ImGui.End();
        } //Draw()

        public void TrySave()
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
        public void TryLoad()
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
    } //PluginWindow
} //namespace
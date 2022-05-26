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
                configuration.CurrentGame.DrawConfig(GameCount);
                //++GameCount;

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

using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;


namespace PissUpPlugin
{
    public sealed class Plugin : IDalamudPlugin 
    {
        public string Name => "PissUpPlugin";

        private const string commandName = "/pppp";

        [PluginService]
        public static IDalamudPluginInterface DalamudPluginInterface { get; private set; }
        [PluginService]
        public static IClientState ClientState { get; private set; }
        [PluginService]
        public static ICommandManager CommandManager { get; private set; }
        [PluginService]
        public static IChatGui ChatGui { get; private set; }
        [PluginService]
        public static IFramework Framework { get; private set; }
        [PluginService]
        public static IPartyList PartyList { get; private set; }

        public Configuration Configuration { get; init; }
        private PluginUI PluginUi { get; init; }

        public delegate void DrawGameUI();
        public event DrawGameUI GameUIDraw;

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
                CancellationToken TaskCancellationToken = TaskCancellationTokenSource.Token;
                this.GameSessionInProgress = this.Configuration.CurrentGame.Run(this, TaskCancellationToken, 
                    async (String Message, CancellationToken Token) => { await MessageSink.Writer.WriteAsync(Message, Token); }
                );
            }
            else
            {
                this.PluginUi.Visible = true;
            }
        }
        public void OnFrameworkUpdate(IFramework framework1)
        {
            if (!this.MessageSource.TryRead(out var Message) || !this.ChatAvailable)
            {
                return;
            }
            ChatSender.SendMessage(Message);
        }
        private void OnLogin()
        {
            this.ChatAvailable = true;
        }

        private void OnLogout(int type, int code)
        {
            this.ChatAvailable = false;
            ClearTask();
        }
        private void DrawUI()
        {
            this.PluginUi.Draw();
            GameUIDraw?.Invoke();
        }
        
        private void DrawConfigUI()
        {
            this.PluginUi.SettingsVisible = true;
        }
    }
}

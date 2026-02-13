using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using TimeMemoria.Services;  // ← ADDED


namespace TimeMemoria
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Time Memoria";

        public QuestData QuestData { get; set; } = new();

        [PluginService]
        public IDalamudPluginInterface PluginInterface { get; init; } = null!;
        [PluginService]
        public ICommandManager CommandManager { get; init; } = null!;
        [PluginService]
        public IDataManager DataManager { get; init; } = null!;
        [PluginService]
        public IGameGui GameGui { get; init; } = null!;
        [PluginService]
        public IPluginLog PluginLog { get; init; } = null!;

        private readonly Configuration configuration;
        private readonly MainWindow mainWindow;
        private readonly QuestDataManager questDataManager;
        private readonly WindowSystem windowSystem;
        private readonly IFramework framework;                          // ← ADDED
        private readonly PlaytimeStatsService playtimeStatsService;     // ← ADDED

        public Plugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager,
            IDataManager dataManager,
            IGameGui gameGui,
            IPluginLog pluginLog,
            IFramework framework,        // ← ADDED
            IClientState clientState,
            IPlayerState playerState,
            IChatGui chatGui)    // ← ADDED
        {
            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            DataManager = dataManager;
            GameGui = gameGui;
            PluginLog = pluginLog;

            configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(PluginInterface);

            questDataManager = new QuestDataManager(PluginInterface, PluginLog, this, configuration);
            
            // ← ADDED THESE LINES ↓
            this.framework = framework;
            playtimeStatsService = new PlaytimeStatsService(framework, clientState, playerState, chatGui, configuration);
            
            windowSystem = new WindowSystem("TimeMemoriaWindows");
            mainWindow = new MainWindow(this, questDataManager, configuration);
            windowSystem.AddWindow(mainWindow);

            CommandManager.AddHandler("/timememoria", new CommandInfo(OnCommand)
            {
                HelpMessage = "Toggle Time Memoria UI"
            });
            CommandManager.AddHandler("/tm", new CommandInfo(OnCommand)
            {
                HelpMessage = "Toggle Time Memoria UI"
            });

            PluginInterface.UiBuilder.Draw += DrawUi;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUi;
            PluginInterface.UiBuilder.OpenMainUi += () => mainWindow.IsOpen = true;
        }

        public void Dispose()
        {
            playtimeStatsService?.Dispose();  // ← ADDED THIS LINE
            mainWindow.Dispose();
            CommandManager.RemoveHandler("/timememoria");
            CommandManager.RemoveHandler("/tm");
        }

        private void OnCommand(string command, string args)
        {
            mainWindow.IsOpen = !mainWindow.IsOpen;
        }

        private void DrawUi()
        {
            windowSystem.Draw();
        }

        private void DrawConfigUi()
        {
            mainWindow.IsOpen = true;
        }
    }
}

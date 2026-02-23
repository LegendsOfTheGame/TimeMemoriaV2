using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using TimeMemoria.Services;


namespace TimeMemoria
{
    public class Plugin : IDalamudPlugin
    {
        public QuestData QuestData { get; set; } = new();

        public IDalamudPluginInterface PluginInterface { get; init; } = null!;
        public ICommandManager CommandManager { get; init; } = null!;
        public IDataManager DataManager { get; init; } = null!;
        public IGameGui GameGui { get; init; } = null!;
        public IPluginLog PluginLog { get; init; } = null!;

        private readonly Configuration configuration;
        private readonly MainWindow mainWindow;
        private readonly QuestDataManager questDataManager;
        private readonly WindowSystem windowSystem;
        private readonly IFramework framework;
        private readonly PlaytimeStatsService playtimeStatsService;
        private readonly NewsService newsService;
        private readonly TocService tocService;


        public Plugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager,
            IDataManager dataManager,
            IGameGui gameGui,
            IPluginLog pluginLog,
            IFramework framework,
            IClientState clientState,
            IPlayerState playerState,
            IChatGui chatGui)
        {
            PluginInterface = pluginInterface;
            CommandManager  = commandManager;
            DataManager     = dataManager;
            GameGui         = gameGui;
            PluginLog       = pluginLog;

            configuration = PluginInterface.GetPluginConfig() as Configuration
                            ?? new Configuration();
            configuration.Initialize(PluginInterface);

            this.framework       = framework;
            playtimeStatsService = new PlaytimeStatsService(
                                       framework, clientState, playerState,
                                       chatGui, configuration);
            newsService          = new NewsService(PluginLog);
            questDataManager     = new QuestDataManager(
                                       PluginInterface, PluginLog, this,
                                       configuration, playtimeStatsService);

            var pluginDir = PluginInterface.AssemblyLocation.DirectoryName!;
            var tocPath   = System.IO.Path.Combine(pluginDir, "Quests", "toc.json");
            tocService    = new TocService(PluginLog, tocPath);

            windowSystem = new WindowSystem("TimeMemoriaWindows");
            mainWindow   = new MainWindow(
                               this, questDataManager, configuration,
                               playtimeStatsService, newsService, tocService);
            windowSystem.AddWindow(mainWindow);

            CommandManager.AddHandler("/timememoria", new CommandInfo(OnCommand)
            {
                HelpMessage = "Toggle Time Memoria UI"
            });
            CommandManager.AddHandler("/tm", new CommandInfo(OnCommand)
            {
                HelpMessage = "Toggle Time Memoria UI"
            });

            PluginInterface.UiBuilder.Draw        += DrawUi;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUi;
            PluginInterface.UiBuilder.OpenMainUi  += OpenMainUi;
        }


        public void Dispose()
        {
            PluginInterface.UiBuilder.Draw        -= DrawUi;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUi;
            PluginInterface.UiBuilder.OpenMainUi  -= OpenMainUi;

            CommandManager.RemoveHandler("/timememoria");
            CommandManager.RemoveHandler("/tm");

            windowSystem.RemoveAllWindows();

            mainWindow.Dispose();
            playtimeStatsService?.Dispose();
            newsService?.Dispose();
        }


        private void OnCommand(string command, string args)
        {
            mainWindow.IsOpen = !mainWindow.IsOpen;
        }

        private void OpenMainUi()
        {
            mainWindow.IsOpen = true;
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

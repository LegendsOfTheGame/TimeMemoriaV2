using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;

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
        private readonly WindowSystem windowSystem;  // ADD THIS LINE

        public Plugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager,
            IDataManager dataManager,
            IGameGui gameGui,
            IPluginLog pluginLog)
        {
            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            DataManager = dataManager;
            GameGui = gameGui;
            PluginLog = pluginLog;

            configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(PluginInterface);

            questDataManager = new QuestDataManager(PluginInterface, PluginLog, this, configuration);
            
            // ADD THESE THREE LINES:
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
            windowSystem.Draw();  // CHANGE THIS LINE
        }

        private void DrawConfigUi()
        {
            mainWindow.IsOpen = true;
        }
    }
}

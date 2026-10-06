using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

namespace ChestDisplay
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    [SynchronizationMode(AdminOnlyStrictness.IfOnServer)]
    public class ChestDisplayPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "chestdisplay";
        public const string PluginName = "Chest Display";
        public const string PluginVersion = "1.1.0";

        private Harmony m_harmony;
        private ConfigFileWatcher m_configWatcher;

        private void Awake()
        {
            SignConfig.Bind(Config);
            // Правка .cfg во время игры применяется сразу (а на сервере — рассылается игрокам).
            m_configWatcher = new ConfigFileWatcher(Config);
            SignLocalization.Register();

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;
            PrefabManager.OnPrefabsRegistered += SignPiece.ApplyRecipe;
            SynchronizationManager.OnConfigurationSynchronized += (_, _) => SignPiece.ApplyRecipe();
            SignConfig.RecipeChanged += SignPiece.ApplyRecipe;
            SignConfig.LookChanged += ChestSign.RefreshAll;

            m_harmony = new Harmony(PluginGuid);
            m_harmony.PatchAll(typeof(Patches));

            Jotunn.Logger.LogInfo($"{PluginName} {PluginVersion} загружен");
        }

        private void OnVanillaPrefabsAvailable()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabsAvailable;
            SignPiece.Register();
        }

        private void OnDestroy()
        {
            m_harmony?.UnpatchSelf();
        }
    }
}

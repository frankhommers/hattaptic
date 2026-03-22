namespace Loupedeck.HaTTaPticPlugin
{
    using System;

    /// <summary>
    /// HaTTaPtic Plugin — HTTP-triggered haptic feedback for MX Master 4.
    /// Listens on http://127.72.80.84:8080/ for GET requests to trigger haptic waveforms.
    /// </summary>
    public class HaTTaPticPlugin : Plugin
    {
        private HapticEventRegistry _registry;
        private HttpHapticServer _httpServer;

        public override bool UsesApplicationApiOnly => true;
        public override bool HasNoApplication => true;

        public HaTTaPticPlugin()
        {
            PluginLog.Init(this.Log);
            PluginResources.Init(this.Assembly);
        }

        public override void Load()
        {
            try
            {
                PluginLog.Info("HaTTaPtic plugin loading...");

                _registry = new HapticEventRegistry(this);
                _registry.RegisterAll();

                _httpServer = new HttpHapticServer(_registry);
                _httpServer.Start();

                PluginLog.Info("HaTTaPtic plugin loaded — listening on http://127.0.0.1:18274/");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to load HaTTaPtic plugin");
            }
        }

        public override void Unload()
        {
            try
            {
                PluginLog.Info("HaTTaPtic plugin unloading...");

                _httpServer?.Stop();
                _httpServer?.Dispose();
                _httpServer = null;

                _registry = null;

                PluginLog.Info("HaTTaPtic plugin unloaded");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Error unloading HaTTaPtic plugin");
            }
        }
    }
}

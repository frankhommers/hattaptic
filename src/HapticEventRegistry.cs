namespace Loupedeck.HaTTaPticPlugin
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Registers haptic waveforms as plugin events and provides triggering.
    /// </summary>
    public class HapticEventRegistry
    {
        private readonly Plugin _plugin;
        private readonly HashSet<string> _knownWaveforms;

        public static readonly string[] Waveforms = new[]
        {
            "sharp_collision",
            "damp_collision",
            "subtle_collision",
            "sharp_state_change",
            "damp_state_change",
            "completed",
            "angry_alert",
            "happy_alert",
            "knock",
            "ringing"
        };

        public HapticEventRegistry(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _knownWaveforms = new HashSet<string>(Waveforms, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Registers all waveforms as plugin events.
        /// </summary>
        public void RegisterAll()
        {
            foreach (var waveform in Waveforms)
            {
                _plugin.PluginEvents.AddEvent(waveform, waveform, $"Haptic waveform: {waveform}");
                PluginLog.Verbose($"Registered haptic event: {waveform}");
            }

            PluginLog.Info($"Registered {Waveforms.Length} haptic events");
        }

        /// <summary>
        /// Triggers a haptic waveform by name.
        /// Returns true if the waveform was found and triggered.
        /// </summary>
        public bool Trigger(string waveformName)
        {
            if (string.IsNullOrEmpty(waveformName))
            {
                return false;
            }

            if (!_knownWaveforms.Contains(waveformName))
            {
                PluginLog.Info($"Unknown waveform requested: {waveformName}");
                return false;
            }

            _plugin.PluginEvents.RaiseEvent(waveformName);
            PluginLog.Info($"Haptic triggered: {waveformName}");
            return true;
        }

        /// <summary>
        /// Returns true if the waveform name is known.
        /// </summary>
        public bool IsKnown(string waveformName) =>
            !string.IsNullOrEmpty(waveformName) && _knownWaveforms.Contains(waveformName);
    }
}

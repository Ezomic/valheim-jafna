using UnityEngine;

namespace Jafna
{
    /// <summary>
    /// The player's own switch for the stone fill, and the one place that flips it.
    ///
    /// The switch is the StoneFill config entry, so the cfg is what remembers it between
    /// sessions: BepInEx writes an entry the moment it is set. It is declared personal with
    /// Suite.Local in the plugin, because Core syncs the whole config by default and a synced
    /// bool is one the host's value is put back over in the frame it changes.
    ///
    /// It sits under the host's AutoRaise and never above it (see <see cref="Fill.Allowed"/>).
    /// Flipping it while the host has AutoRaise off is allowed and changes nothing yet, and the
    /// message and the panel both say so, because a switch that silently does nothing is the
    /// complaint this mod keeps being built to answer.
    /// </summary>
    internal static class StoneSwitch
    {
        internal static bool On
        {
            get { return JafnaConfig.StoneFill.Value; }
        }

        internal static void Set(bool on)
        {
            JafnaConfig.StoneFill.Value = on;
        }

        /// <summary>Flips the switch from the key, and says so where the eye already is.</summary>
        internal static void Flip(Player player)
        {
            Set(!On);

            if (player == null) return;

            string text = On ? "Stone fill on" : "Stone fill off";
            if (!JafnaConfig.AutoRaise.Value) text += ", but this server has it off";

            // Top left rather than the middle: the middle of the screen is where a swing's own
            // messages land, and a scenario reads that line to see what the swing said.
            player.Message(MessageHud.MessageType.TopLeft, text);
        }
    }
}

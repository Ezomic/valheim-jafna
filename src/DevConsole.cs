using HarmonyLib;

namespace Jafna
{
    /// <summary>
    /// `jafna stone on|off` in the console: the stone fill switch, the way the key flips it.
    ///
    /// It exists for a Devkit scenario, which cannot press a key and needs the off state to test
    /// that a swing far below the held height takes no stone. With no word it only reports.
    ///
    /// isCheat false, and honestly: it flips the same preference the key flips, which any player
    /// can do by hand, and changes no rule, no skill and nothing in the world. A cheat command
    /// would need devcommands first and would mark the character as having cheated.
    /// </summary>
    internal static class DevConsole
    {
        /// <summary>
        /// Process-wide, not per world. Terminal's command table is a private static that nothing
        /// clears, so a second registration would leave a duplicate behind.
        /// </summary>
        private static bool _registered;

        [HarmonyPatch(typeof(Terminal), "InitTerminal")]
        internal static class Hook
        {
            private static void Postfix()
            {
                if (_registered) return;
                _registered = true;

                new Terminal.ConsoleCommand("jafna",
                    "[stone on|off] - your own switch for paying stone to raise low ground, the way "
                    + "its key flips it. With no word it says what the switch is",
                    OnCommand, isCheat: false);
            }
        }

        private static void OnCommand(Terminal.ConsoleEventArgs args)
        {
            Terminal term = args.Context;
            if (term == null) return;

            string verb = args.Length > 1 ? args[1].ToLowerInvariant() : "";

            if (verb.Length > 0 && verb != "stone")
            {
                term.AddString("jafna: unknown '" + args[1] + "'. Try jafna stone on|off.");
                return;
            }

            string state = args.Length > 2 ? args[2].ToLowerInvariant() : "";

            if (state == "on") StoneSwitch.Set(true);
            else if (state == "off") StoneSwitch.Set(false);
            else if (state.Length > 0)
            {
                term.AddString("jafna stone: on or off, not '" + args[2] + "'");
                return;
            }

            // name=value with no spaces inside, so Devkit's `printed` step can match it.
            term.AddString("jafna stone=" + (StoneSwitch.On ? "on" : "off")
                + " raise=" + (JafnaConfig.AutoRaise.Value ? "on" : "off")
                + "   (the key is " + JafnaConfig.StoneFillKey.Value + ", pressed with a levelling tool out)");
        }
    }
}

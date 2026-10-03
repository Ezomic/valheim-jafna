using System.Globalization;
using HarmonyLib;
using TMPro;

namespace Jafna
{
    /// <summary>
    /// `jafnareadout` in the console: what the build panel's description actually drew, as one
    /// line a Devkit scenario can read numbers out of.
    ///
    /// The readout's whole job since LHM-46 is to leave the box the same size in every state, and
    /// that is a claim about the rendered label, not about the string Jafna wrote. A line longer
    /// than the box wraps and adds a row, and that is invisible from the string. So this asks the
    /// label: how many lines it laid out against how many the text has, how tall it wants to be,
    /// and how wide its widest line is against the room it has. A scenario runs it in each state
    /// and holds the line count and the height equal with `went ... same`.
    ///
    /// Not named `jafna`, which the stone switch's console command takes on its own branch.
    /// isCheat false: it only reads.
    /// </summary>
    internal static class ReadoutProbe
    {
        private static bool _registered;

        [HarmonyPatch(typeof(Terminal), "InitTerminal")]
        internal static class Hook
        {
            private static void Postfix()
            {
                if (_registered) return;
                _registered = true;

                new Terminal.ConsoleCommand("jafnareadout",
                    "- how many lines the build panel's description drew, how tall it is and whether "
                    + "a line wrapped, for a scenario to hold equal from state to state",
                    OnCommand, isCheat: false);
            }
        }

        private static void OnCommand(Terminal.ConsoleEventArgs args)
        {
            Terminal term = args.Context;
            if (term == null) return;

            Hud hud = Hud.instance;
            TMP_Text label = hud == null ? null : hud.m_pieceDescription;

            if (label == null || !label.gameObject.activeInHierarchy)
            {
                term.AddString("jafnareadout: the build panel is not up, take the hoe out first");
                return;
            }

            // Regenerated now rather than trusted from the last canvas update, so the numbers
            // describe the string in the label this frame.
            label.ForceMeshUpdate();

            int written = label.text.Split('\n').Length;
            int drawn = label.textInfo.lineCount;

            float widest = 0f;
            for (int i = 0; i < drawn; i++)
            {
                if (label.textInfo.lineInfo[i].width > widest) widest = label.textInfo.lineInfo[i].width;
            }

            float room = label.rectTransform.rect.width - label.margin.x - label.margin.z;

            // name=value with no spaces inside, so Devkit's `note` and `printed` steps can match it.
            term.AddString("jafnareadout lines=" + drawn
                + " written=" + written
                + " wrapped=" + (drawn == written ? "no" : "yes")
                + " tall=" + label.preferredHeight.ToString("0.0", CultureInfo.InvariantCulture)
                + " widest=" + widest.ToString("0.0", CultureInfo.InvariantCulture)
                + " room=" + room.ToString("0.0", CultureInfo.InvariantCulture)
                + " clipped=" + (widest > room + 0.02f ? "yes" : "no"));
        }
    }
}

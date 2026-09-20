using HarmonyLib;
using UnityEngine;

namespace Jafna
{
    /// <summary>
    /// The numbers, on screen, while a levelling tool is out.
    ///
    /// This is half the mod rather than a nicety. Continuing a flat means the tool sometimes
    /// uses a height that is not the one under your crosshair, and a tool that silently
    /// overrules your aim is indistinguishable from a tool that has started misbehaving. The
    /// readout is what makes the difference between the two visible, so it says not only which
    /// height is going to be used but where that height came from.
    ///
    /// It goes in the build panel and nowhere else. No window, no keybind, no ring: the panel
    /// is where a player already looks to find out what the selected entry does, and
    /// <c>Hud.SetupPieceInfo</c> re-reads <c>m_description</c> off the piece and re-localises it
    /// on every single frame the build HUD is up. So writing the string onto the prefab is
    /// enough to get a live number, with no UI of our own to build, skin or keep in sync.
    ///
    /// The description is restored the moment the selection changes or the feature is switched
    /// off, because the thing being written to is a prefab and it lives as long as the process.
    /// Leaving a line on it would strand a stale reach on an entry nothing is updating any
    /// more, which Core's config sync can cause mid-session by pushing a host's Enabled.
    /// </summary>
    internal static class Readout
    {
        private static Piece _described;
        private static string _original;
        private static string _written;

        private static string _lines;

        /// <summary>
        /// Recomputed by the placement-ghost postfix while a level op is selected. Kept as a
        /// finished string rather than as its parts so that the restore path below has exactly
        /// one thing to compare and exactly one thing to undo.
        /// </summary>
        internal static void Update(
            Player player, TerrainOp.Settings settings, Vector3 point, float radius, bool wardClear)
        {
            if (!JafnaConfig.ShowReadout.Value)
            {
                _lines = null;
                return;
            }

            // The compiler for this zone exists on this machine only once something has been
            // levelled here before, and that is exactly the case where there is a flat to
            // continue - so a null compiler is not a failure, it is the answer.
            TerrainComp comp = TerrainComp.FindTerrainCompiler(point);

            float offset = settings.m_levelOffset;
            Vector3 probe = point + Vector3.up * offset;

            float target = Flat.Target(comp, probe, radius, settings.m_square, out Flat.Source source);

            string reason;
            switch (source)
            {
                case Flat.Source.ContinuedFlat:
                    reason = "continuing the flat it touches";
                    break;
                case Flat.Source.Disagreed:
                    reason = "crosshair - two heights meet here";
                    break;
                case Flat.Source.TooLittle:
                    reason = "crosshair - too little flat to follow";
                    break;
                default:
                    reason = "crosshair - new ground";
                    break;
            }

            string text =
                "Reach <color=orange>" + radius.ToString("0.0") + "m</color>"
                + " at Crafting " + JafnaPatches.CraftingLevel(player).ToString("0")
                + "\nLevel to <color=orange>" + target.ToString("0.00") + "m</color>"
                + " (" + reason + ")";

            if (!wardClear)
            {
                text += "\n<color=#ff6060>A ward you cannot use is inside this swing.</color>";
            }

            _lines = text;
        }

        internal static void Clear()
        {
            _lines = null;
        }

        /// <summary>
        /// Writes the lines onto the selected piece and takes them off again when the selection
        /// moves.
        ///
        /// Patched on UpdatePlacement rather than on the ghost update, because the ghost update
        /// stops being called the moment the tool goes away and the restore has to happen after
        /// that, not before. GetSelectedPiece is null-safe on the same field InPlaceMode tests,
        /// so putting the hoe away arrives here as a null selection and restores rather than
        /// returning early and leaving the write behind.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "UpdatePlacement", new[] { typeof(bool), typeof(float) })]
        private static void UpdatePlacement(Player __instance)
        {
            if (__instance == null || __instance != Player.m_localPlayer) return;

            Piece selected = __instance.GetSelectedPiece();
            if (selected != _described) Restore();

            if (!JafnaConfig.Enabled.Value || _lines == null || selected == null)
            {
                Restore();
                return;
            }

            if (_described == null)
            {
                _described = selected;
                _original = selected.m_description;
            }

            string wanted = string.IsNullOrEmpty(_original) ? _lines : _original + "\n\n" + _lines;

            // Only on change. SetupPieceInfo runs every frame and would happily re-localise a
            // fresh string sixty times a second; there is no reason to hand it one.
            if (wanted == _written) return;

            selected.m_description = wanted;
            _written = wanted;
        }

        private static void Restore()
        {
            if (_described == null) return;

            // Unity's == is overloaded so a destroyed Piece compares equal to null, and this is
            // the one place that matters: a prefab whose description we changed can be gone by
            // the time the selection moves. Plain == rather than ?. for exactly that reason -
            // the null-propagating operators bypass the overload and would hand back a
            // destroyed object that throws somewhere else entirely.
            if (_described != null) _described.m_description = _original;

            _described = null;
            _original = null;
            _written = null;
        }
    }
}

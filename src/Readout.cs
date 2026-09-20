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

            float target;
            Flat.Source source;

            if (Held.Active)
            {
                target = Held.Height;
                source = Flat.Source.Held;
            }
            else
            {
                target = Flat.Target(comp, probe, radius, Reach.IsSquare(settings), out source);
            }

            // Vanilla's own radius, so the line can say what the skill actually bought rather
            // than only what the swing covers.
            float vanilla = Reach.VanillaRadius(settings);

            // Each of these ends with the height, so the one value that changes as you look
            // around is the last thing on the line and moves nothing when it does.
            string reason;
            switch (source)
            {
                case Flat.Source.Held:
                    // Worded as an instruction rather than a state, because the one failure
                    // this feature can produce is forgetting it is on: you walk somewhere else,
                    // swing, and the ground moves toward a number you set five minutes ago.
                    // A line that says how to stop is a line that cannot be misread as scenery.
                    reason = "HOLDING height, press " + JafnaConfig.HoldKey.Value + " to release. Levelling to ";
                    break;
                case Flat.Source.ContinuedFlat:
                    reason = "Continuing ground you already flattened, to ";
                    break;
                case Flat.Source.Disagreed:
                    reason = "Two heights meet here, so using your crosshair at ";
                    break;
                case Flat.Source.TooLittle:
                    reason = "Too little flat ground to follow, so using your crosshair at ";
                    break;
                default:
                    reason = "Fresh ground, levelling to your crosshair at ";
                    break;
            }

            // Written as width rather than radius, and as sentences rather than labels.
            //
            // The first version of this line read "Reach 5,4m at Crafting 52 (tool: 3,0m)" and
            // was not clear, which is a fair complaint about all three of its parts. "Reach" is
            // a radius, but what a player watches is how wide the patch under them goes, so the
            // number on screen disagreed with the number in their eyes by a factor of two.
            // "tool:" named a thing without saying what about it. And two bare figures side by
            // side leave you to work out which is the mod and which is the game.
            string text;

            string crafting = JafnaPatches.CraftingLevel(player).ToString("0");

            // Nothing on this line changes while the tool is out - the reach only moves when
            // Crafting does - so it is written plainly with no padding at all.
            if (radius > vanilla + 0.01f)
            {
                text = "Flattens " + Num((radius * 2f).ToString("0.0") + "m") + " across,"
                       + " up from the hoe's own " + (vanilla * 2f).ToString("0.0") + "m"
                       + " (Crafting " + crafting + ")";
            }
            else
            {
                // Below about Crafting 25 the curve has not caught the hoe up yet. Saying so is
                // better than showing two identical numbers, which reads as the mod being broken
                // rather than as the skill not being high enough.
                text = "Flattens " + Num((radius * 2f).ToString("0.0") + "m") + " across,"
                       + " the hoe's own reach (Crafting " + crafting + " adds nothing yet)";
            }

            text += "\n" + reason + Num(target.ToString("0.00") + "m");

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
        /// A number in the panel's highlight colour.
        ///
        /// There is deliberately no padding and no fixed advance here, and both were tried. The
        /// height under the crosshair changes every frame and the panel is rewritten every
        /// frame, so an ordinary number pushes whatever follows it sideways as you look around,
        /// which breaks the suite's rule that nothing reflows. Padding to a fixed character
        /// count and forcing one advance per character with TextMeshPro's mspace does hold the
        /// line still - and puts the padding on screen as visible gaps, "the hoe's own  6,0m"
        /// and "(Crafting  52)", which is a worse fault than the one it fixed.
        ///
        /// What actually fixes it is arrangement, not formatting: a number that changes every
        /// frame goes at the END of its line, where there is nothing after it to move. See the
        /// lines below. Nothing here needs to be padded once they are ordered that way.
        /// </summary>
        private static string Num(string text)
        {
            return "<color=orange>" + text + "</color>";
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

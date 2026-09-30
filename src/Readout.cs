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
            Player player, TerrainOp.Settings settings, Vector3 point, float radius, bool wardClear, bool lands)
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
            else if (lands)
            {
                // Only while the swing would actually land. A refused swing raises nothing and
                // costs nothing, and a price on screen beside a refusal reads as the price of
                // being refused.
                string fill = FillLine(player, settings, point, radius);
                if (fill != null) text += "\n" + fill;
            }

            _lines = text;
        }

        /// <summary>
        /// What raising the low ground under this swing will take out of the pack, or null when
        /// nothing under it needs raising and none of it is too far below to raise.
        ///
        /// Silent when there is nothing to raise, which is most of the time. A line that said
        /// "costs 0" on every flat swing would teach a player to stop reading it before the swing
        /// where it matters.
        ///
        /// "That height" is the height at the end of the line above, which is the one the swing
        /// is filling toward. The cost is the number that moves as you look around, so it goes
        /// last, by the same rule as every other line here.
        ///
        /// When the pack is short, the line says what this swing will actually do, because a
        /// short swing fills the middle of the patch and leaves the rest (see Fill's class
        /// comment), and a price for the whole patch is not what it charges. What the pack holds
        /// goes first and stays still, then what this swing fills and takes, then what all of it
        /// would cost. That puts two moving numbers on one line, which the rule above is against,
        /// and it is accepted: this swing's price moves least, because a short swing spends about
        /// everything you carry, so the price of the whole patch, which moves with every step of
        /// the aim, is the one that goes last. When even the middle is more than the pack pays for,
        /// the line says the swing fills nothing, since that swing is left to the hoe's own easing
        /// and takes nothing.
        ///
        /// Out of range of the workbench Raise ground needs, the line says that instead of a price
        /// and nothing else, because that swing takes no stone whatever the pack holds, and a price
        /// beside it would read as what it is about to charge. Only where a swing would need stone,
        /// by the same silence rule as the price: a reminder on every flat swing away from a
        /// bench would be scenery within the hour.
        ///
        /// When the entry itself costs some of the same item, the short lines say so after what
        /// you carry. That stone is taken by vanilla after the swing and the fill leaves it alone,
        /// so without the clause "you carry 5, filling costs 5" would read as enough and the
        /// swing would then say it ran short. The other lines leave it out, because vanilla's
        /// own requirement list in the same panel already shows it.
        ///
        /// Ground too far below the height for the game to ever allow gets a line of its own
        /// under the price, and on its own when that is all there is. Without it the panel shows a
        /// height and the ground stops short of it with nothing to say why, which is the complaint
        /// the fill was built to answer. Under the price rather than above it, so the price line
        /// does not jump up and down as that line comes and goes with the aim.
        ///
        /// Item names are written as the game's own $ tokens and come out in the player's
        /// language, because the build panel localises the description on every frame it draws.
        /// </summary>
        private static string FillLine(Player player, TerrainOp.Settings settings, Vector3 point, float radius)
        {
            Fill.Terms terms = Fill.Quote(
                player, settings, point, radius,
                out string cost, out string all, out string carried, out string own, out bool far,
                out string station);

            string pack = "You carry " + carried + (string.IsNullOrEmpty(own) ? "" : ", the swing itself takes " + own);
            string line;

            switch (terms)
            {
                case Fill.Terms.Free:
                    line = "Filling up to that height is free in this world";
                    break;
                case Fill.Terms.Covered:
                    line = "Filling up to that height is already paid for";
                    break;
                case Fill.Terms.Paid:
                    line = "Filling up to that height costs " + Num(cost);
                    break;
                case Fill.Terms.Short:
                    line = pack + ", this swing fills the middle for " + Num(cost) + ", all of it costs " + Num(all);
                    break;
                case Fill.Terms.ShortCovered:
                    line = pack + ", the middle is already paid for, all of it costs " + Num(all);
                    break;
                case Fill.Terms.Unaffordable:
                    line = pack + ", not enough to fill any of it, all of it costs " + Num(all);
                    break;
                case Fill.Terms.NoStation:
                    line = "Filling up to that height needs " + station + " nearby";
                    break;
                default:
                    line = null;
                    break;
            }

            if (!far) return line;

            if (line == null)
            {
                return "The ground here cannot be filled that high, the game keeps ground within 8m of "
                       + "where the world made it";
            }

            return line + "\nPart of the ground here cannot be filled that high, the game keeps ground within "
                   + "8m of where the world made it";
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

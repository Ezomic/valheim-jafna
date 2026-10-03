using System.Collections.Generic;
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
        /// How many lines the box holds, and the reason the count is fixed.
        ///
        /// The panel is the game's own tooltip, which sizes itself to its text, so a readout that
        /// grew a warning line over a bad swing and lost its price line over a good one resized the
        /// box as the crosshair moved, and at working speed that is unreadable (LHM-46). The cure is
        /// not shorter text, it is text that never changes shape: the same lines in the same
        /// places in every state, a dash where a line has nothing to say, and one warning slot that
        /// is replaced in place instead of added to. Robbin picked this layout from a mockup, with
        /// the Ground group above Height because what the swing does to the ground is the part he
        /// wants first.
        ///
        ///   warning slot            All clear, or the one thing that is wrong, in red
        ///   GROUND                  what the swing does to the ground
        ///     Raise up to / Lower up to / Flattens
        ///   HEIGHT                  which height, and where it came from
        ///     Crosshair / This swing / Taken from
        ///   COST                    stone and the workbench
        ///     Stone / Workbench
        ///
        /// Nothing may be added to or removed from that list in a branch of the code below. A new
        /// fact either takes a row of its own that is always written, or goes into the warning slot.
        /// </summary>
        internal const int Rows = 12;

        /// <summary>
        /// The longest a line may be, in visible characters after the game has put its own names in.
        /// A line longer than the box wraps, and a wrapped line is a line more. The longest row
        /// written here is 38. Devkit's `jafnareadout` checks the real box for a wrapped line.
        /// </summary>
        private const int MaxChars = 40;

        private const string Dim = "#a79d86";
        private const string Red = "#ff6060";

        private static readonly System.Text.RegularExpressions.Regex Tags =
            new System.Text.RegularExpressions.Regex("<[^>]*>");

        private static readonly List<Heightmap> Maps = new List<Heightmap>();

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

            Fill.Terms terms = Fill.Terms.None;
            string cost = null, all = null, carried = null, own = null, station = null;
            bool far = false;

            // Only while the swing would actually land. A refused swing raises nothing and costs
            // nothing, and a price on screen beside a refusal reads as the price of being refused.
            if (wardClear && lands)
            {
                terms = Fill.Quote(
                    player, settings, point, radius,
                    out cost, out all, out carried, out own, out far, out station);
            }

            Gap(point, radius, target, out float raise, out float lower);

            string crafting = JafnaPatches.CraftingLevel(player).ToString("0");

            // Each row ends with whatever changes as you look around, so the one value that moves
            // every frame is the last thing on its line and shifts nothing when it does.
            var rows = new List<string>(Rows)
            {
                Slot(wardClear, far, terms, own, station),

                Header("GROUND"),
                Row("Raise up to: ", Metres(raise)),
                Row("Lower up to: ", Metres(lower)),
                Row("Flattens ", Num((radius * 2f).ToString("0.0") + "m"), " across (Crafting ", crafting, ")"),

                Header("HEIGHT"),
                Row("Crosshair: ", Num(probe.y.ToString("0.00") + "m")),
                Row("This swing: ", Num(target.ToString("0.00") + "m")),
                Row("Taken from: ", Num(From(source))),

                Header("COST"),
                StoneRow(terms, cost, all, carried),
                WorkbenchRow(terms)

                // LHM-42's stone switch belongs here as the last row of the Cost group, for
                // example "Stone fill is on, J turns it off". It is a row that is always written,
                // so merging it makes Rows 13 and moves nothing else in the box.
            };

            _lines = string.Join("\n", rows.ToArray());
        }

        /// <summary>
        /// The warning slot: always one line, "All clear" or the single most important thing that
        /// is wrong. In order: a ward that blocks the swing, ground the game will never let it
        /// reach, a swing that cannot be paid for because no workbench is near, then running short
        /// of stone. Only the first is shown and the rows below say the rest, so a second problem
        /// never makes the slot taller.
        /// </summary>
        private static string Slot(bool wardClear, bool far, Fill.Terms terms, string own, string station)
        {
            if (!wardClear) return Warn("A ward you cannot use is in the swing");
            if (far) return Warn("Past 8m of the original ground");

            switch (terms)
            {
                case Fill.Terms.NoStation:
                    return Warn("Filling needs " + station + " nearby");
                case Fill.Terms.Short:
                case Fill.Terms.ShortCovered:
                    // The entry's own cost is taken by vanilla after the fill leaves it alone, so
                    // "you carry 5, needs 31" would read as five to spend when some of it is not.
                    return Warn(string.IsNullOrEmpty(own)
                        ? "Short of stone, fills only the middle"
                        : "Short, the swing itself takes " + own);
                case Fill.Terms.Unaffordable:
                    return Warn("Not enough stone to fill any of it");
                default:
                    return Fit("<color=" + Dim + ">All clear</color>");
            }
        }

        private static string StoneRow(Fill.Terms terms, string cost, string all, string carried)
        {
            switch (terms)
            {
                case Fill.Terms.Free:
                    return Row("Stone: ", Num("free in this world"));
                case Fill.Terms.Covered:
                    return Row("Stone: ", Num("already paid for"));
                case Fill.Terms.Paid:
                    return Row("Stone: you carry ", Num(carried), ", needs ", Num(cost));
                case Fill.Terms.Short:
                case Fill.Terms.ShortCovered:
                case Fill.Terms.Unaffordable:
                    return Row("Stone: you carry ", Num(carried), ", needs ", Bad(all));
                default:
                    return Row("Stone: ", Num("-"));
            }
        }

        private static string WorkbenchRow(Fill.Terms terms)
        {
            switch (terms)
            {
                case Fill.Terms.None:
                    return Row("Workbench: ", Num("-"));
                case Fill.Terms.NoStation:
                    return Row("Workbench: ", Bad("none near"));
                default:
                    return Row("Workbench: ", Num("in range"));
            }
        }

        private static string From(Flat.Source source)
        {
            switch (source)
            {
                case Flat.Source.Held:
                    // Worded as an instruction rather than a state, because the one failure this
                    // feature can produce is forgetting it is on: you walk somewhere else, swing,
                    // and the ground moves toward a number you set five minutes ago. The word
                    // HOLDING stays because the scenarios assert its absence while nothing is held,
                    // and a check for a word that no longer exists would pass for nothing.
                    return "HOLDING, " + JafnaConfig.HoldKey.Value + " releases";
                case Flat.Source.ContinuedFlat:
                    return "flat ground";
                case Flat.Source.Disagreed:
                    return "crosshair, heights disagree";
                case Flat.Source.TooLittle:
                    return "crosshair, too little flat";
                default:
                    return "your crosshair";
            }
        }

        /// <summary>
        /// How far the ground under the swing sits below and above the height it will use, as the
        /// largest gap in each direction, read off the grid points the fill walks. A swing does not
        /// move every point the whole gap (the hoe eases the edge less than the middle), so these
        /// are the most the ground will change, not what it changes everywhere.
        /// </summary>
        private static void Gap(Vector3 point, float radius, float target, out float raise, out float lower)
        {
            raise = 0f;
            lower = 0f;

            Maps.Clear();
            Heightmap.FindHeightmap(point, radius, Maps);

            for (int i = 0; i < Maps.Count; i++)
            {
                Heightmap hmap = Maps[i];
                if (hmap == null) continue;

                float reach = radius / hmap.m_scale;
                if (reach <= 0f) continue;

                hmap.WorldToVertex(point, out int cx, out int cy);

                int span = Mathf.CeilToInt(reach);
                int pitch = hmap.m_width + 1;
                float baseY = hmap.transform.position.y;
                Vector2 middle = new Vector2(cx, cy);

                for (int iy = cy - span; iy <= cy + span; iy++)
                {
                    for (int ix = cx - span; ix <= cx + span; ix++)
                    {
                        if (ix < 0 || iy < 0 || ix >= pitch || iy >= pitch) continue;
                        if (Vector2.Distance(middle, new Vector2(ix, iy)) > reach) continue;

                        float gap = target - (hmap.GetHeight(ix, iy) + baseY);
                        if (gap > raise) raise = gap;
                        if (-gap > lower) lower = -gap;
                    }
                }
            }
        }

        private static string Metres(float value)
        {
            return Num(value < 0.01f ? "-" : value.ToString("0.00") + "m");
        }

        private static string Header(string name)
        {
            return "<color=" + Dim + ">" + name + "</color>";
        }

        private static string Warn(string text)
        {
            return Fit("<color=" + Red + ">" + text + "</color>");
        }

        private static string Bad(string text)
        {
            return "<color=" + Red + ">" + text + "</color>";
        }

        private static string Row(params string[] parts)
        {
            return Fit(string.Concat(parts));
        }

        /// <summary>
        /// Holds a line to <see cref="MaxChars"/>. Measured after the game has put its own names in
        /// (item names are $ tokens that come out longer or shorter in the player's language), and
        /// cut as plain text with ".." if it is still too long, which loses the colours on that line
        /// and nothing else. That is the rare case, a price of several kinds of item, and a plain
        /// line is a better failure than a wrapped one.
        /// </summary>
        private static string Fit(string line)
        {
            string shown = Localization.instance != null ? Localization.instance.Localize(line) : line;
            string plain = Tags.Replace(shown, "");

            return plain.Length <= MaxChars ? shown : plain.Substring(0, MaxChars - 2) + "..";
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

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Jafna
{
    /// <summary>
    /// Raising the ground a flattening swing cannot lift on its own, and paying for it in stone.
    ///
    /// The ceiling this removes is vanilla's, not the mod's. The hoe's Level ground is a
    /// <c>SmoothTerrain</c> op, and SmoothTerrain adds what it moves into <c>m_smoothDelta</c> and
    /// clamps that to one metre either way per point. Only a level or raise op ever banks it into
    /// <c>m_levelDelta</c> and frees the room again. So a point more than a metre below the height
    /// a swing is aiming at comes up a metre and stops, and every swing after that moves it
    /// nothing. Holding a height makes it easy to aim past that, and the ground then visibly stops
    /// short of the number the panel is showing.
    ///
    /// On 2026-09-20 the ceiling was kept on purpose, because a flattening swing that could set
    /// ground to any height would be a free Raise ground. On 2026-09-26 Robbin asked for the
    /// ground to be raised anyway, with stone as the price. That keeps the reason and removes the
    /// ceiling: the part a swing cannot reach is raised, and it costs what raising it by hand costs.
    ///
    /// The price is vanilla's rather than a number of ours. The hoe's own Raise ground entry is
    /// found in the tool's piece table at runtime, and its <c>m_resources</c> are the price of one
    /// Raise ground swing. That swing's volume on flat ground follows from its own settings and
    /// <c>RaiseTerrain</c>'s falloff (see <see cref="RaiseVolume"/>), so the rate is the entry's
    /// cost divided by that volume, per item. Both halves are asset data, which is why neither is
    /// written down here and why the resolved price is logged once a session.
    ///
    /// Whether a swing needs stone at all is the hoe's question, not ours. It does when the hoe's
    /// own swing would run into its one metre clamp at a point it is allowed to raise, which is
    /// exactly the ground the ceiling above stops short. A swing that does not is left to vanilla
    /// untouched, easing and all, and costs nothing, however far below the height its edge sits:
    /// the hoe has always left the edge of a swing short, and charging for that would turn every
    /// flattening swing into a paid one. The flat top below is a property of paying, not a new hoe.
    ///
    /// A swing that needs stone lifts its whole circle to the height at once. On 2026-09-27 Robbin
    /// picked this from three side views, as "Flat top, hard edge": "One paid swing lifts its whole
    /// circle straight to your height at once. It's faster, but it leaves a step at the edge of the
    /// circle until you swing next to it." Until then the fill followed the hoe's easing, and that
    /// is what the pick replaced. SmoothTerrain eases each point toward the height by
    /// <c>1 - (d/r)^power</c> rather than setting it, the hoe's power is 1, and the fill paid only
    /// for what the clamp cut off, so one swing finished only its middle and it took several more
    /// to bring the ground round it up. Now every point inside the circle that is below the height
    /// ends on it, and a point above it is eased down by the hoe for free as it always was. The
    /// cost of that is the rim: a point near the edge, which the hoe barely moves, is now paid for
    /// nearly all the way, so a paid swing takes many times the stone the eased one did. The
    /// step at the edge is as tall as the ground was short there, and the next paid swing beside
    /// it continues the flat and takes it away.
    ///
    /// Only what the hoe would not have done for free is charged, and that is where the two arrays
    /// matter. The fill runs before SmoothTerrain, which then reads the heights as they stood
    /// before the swing, because the heightmap is only rebuilt in its LateUpdate, and moves each
    /// point by its eased amount through <c>m_smoothDelta</c>, clamped to a metre. The fill never
    /// writes that array. It works out what the smooth is about to leave in it and sets
    /// <c>m_levelDelta</c> so that the two add up to the height (see <see cref="Measure"/>). So
    /// every point still gets exactly the free movement a vanilla swing would have given it, uses
    /// up exactly the same share of its one metre, and everything past that is billed: nothing
    /// reaches the level delta that was not paid for. The alternative matters more than it looks.
    /// Vanilla's raise op folds the smooth delta into the level delta and so hands the next
    /// smoothing swing a fresh free metre. Doing that here would make every swing's first metre
    /// free again, and flattening a hole a metre at a time would cost nothing, which is the free
    /// Raise ground this exists not to be.
    ///
    /// A point comes all the way up or not at all. Also on 2026-09-27, before the flat top, Robbin
    /// asked: "if it can't raise a part to the level that is set to flatten, it doesnt raise that
    /// part up and doesnt use stone for that part". The first version did the opposite in both of
    /// the places a fill can fall short. Short of stone, it raised every point by the same fraction
    /// of what that point needed, so the whole patch came up part of the way, the stone was gone
    /// and not even the middle was at the height. And at vanilla's eight metre limit it raised a
    /// point as far as the limit and charged for it, although no swing could ever take that point
    /// the rest of the way. Now the stone is spent from the middle of the swing outward, each point
    /// it reaches lifted onto the height, and a point whose level the game will never allow is left
    /// out whatever the pack holds (see <see cref="Measure"/>). A point left out still gets the
    /// hoe's own free, eased movement, because that is the hoe without this mod, and it costs
    /// nothing. So a swing that runs short leaves a flat patch in its middle, a step round it,
    /// and the hoe's usual slope beyond that.
    ///
    /// Both ways of leaving a point out are said, before the swing and at it. The build panel
    /// prices only what the swing will fill, says when that is the middle and not all of it, and
    /// says when part of the swing is past the eight metre limit (see <see cref="Quote"/>). The
    /// swing itself puts one message in the middle of the screen (see <see cref="Plan"/>). A
    /// point left out in silence would bring back the complaint this feature began with, ground
    /// stopping short of the height on the panel with nothing to say why, this time by design.
    ///
    /// The middle first because the middle is where the player aimed, and a patch that grows
    /// outward from it leaves one clean edge. Cheapest first would raise more points for the same
    /// stone and leave the deepest part, right under the crosshair, as the one hole in the swing.
    ///
    /// Where the work happens is the part that took the most thought.
    ///
    ///  - The swinging client decides whether the swing needs stone and how much of it it can pay
    ///    for, because the stone is in its pack and nowhere else. It measures the swing from its
    ///    own copy of the zone, which is the same data the owner holds, works out how far from the
    ///    middle of the swing its stone reaches, and sends that distance behind the reach in the
    ///    package Reach.cs already appends.
    ///  - The client that owns the zone lifts every point inside that distance that is below the
    ///    height onto it, from what it measures itself. A distance rather than a volume so that a
    ///    swing across a zone line stops at the same place on both sides of it: two zones each
    ///    handed a volume would each spend it from their own half, and the line between them is a
    ///    row of vertices both of them draw. How far a vertex is from the middle is a fact both
    ///    zones agree on. For the same reason the owner does not ask again whether the swing
    ///    needs stone. A distance above zero is the swinger's answer for the whole swing, and a
    ///    zone asking for itself could find nothing clamped in its own half, ease that half the
    ///    hoe's way and flat top the other, with a step running down the zone line.
    ///  - The owner then tells the swinger what it actually raised, and the swinger pays for that.
    ///    Paying after rather than before is what keeps a mixed server honest in the right
    ///    direction: an owner without this mod never reads the distance, raises nothing and sends
    ///    no bill, so the swing costs nothing. Paying up front would have taken stone for a raise
    ///    that never happened.
    ///
    /// When the swinger owns the zone, which is singleplayer and most of the time on a server,
    /// every step of that is one synchronous call and the bill is exact. When somebody else owns
    /// it, two things run a round trip behind.
    ///
    ///  - The bill. A second quick swing is planned against stone the first one's bill is about to
    ///    take, so the two bills together can come to more than the pack held. The difference is
    ///    carried as a debt against the next fill rather than forgiven, so rapid swings cannot be
    ///    used to raise ground for nothing.
    ///  - The ground. The swinger's copy can still show ground the last swing already raised, so
    ///    it overestimates what is left, the distance comes out short of the whole swing with stone
    ///    to spare, the rim is left for later and the swinger is told it ran short when it did not.
    ///    The same stale copy can make a swing a paid one that the owner, seeing the raised ground,
    ///    would have left to the hoe; the owner then lifts only what it finds below the height and
    ///    bills only that. Both are left alone on purpose. The bill is always for ground the owner
    ///    really raised, the next swing finishes the job, and every fix tried on paper either let
    ///    the two sides of a zone line stop at different places or needed the owner to know about
    ///    zones it does not own. It is in the README's multiplayer section.
    /// </summary>
    internal static class Fill
    {
        /// <summary>The method the zone owner calls on the swinger with what a fill used.</summary>
        internal const string BillRpc = "Jafna_FillBill";

        /// <summary>
        /// Clamped shortfalls adding up to less than this are rounding, and a swing with no more
        /// than this does not need stone, in cubic metres. Ten litres. Without a floor a swing on
        /// ground that is already flat can find a few millimetres of float noise at one rim point,
        /// become a paid swing and take a whole stone for it.
        /// </summary>
        private const float MinVolume = 0.01f;

        /// <summary>
        /// The smallest paid lift one point is given, in metres, and the smallest clamped shortfall
        /// at one point that counts toward a swing needing stone. A point that would be lifted less
        /// than this is left to the hoe and not charged for.
        ///
        /// MinVolume alone was not enough, and the reason is ground that is nearly finished. The
        /// hoe's free room at a point is used up by every swing that moves it, filled or not, so on
        /// worked ground most points have little or none left and any upward centimetre there is a
        /// shortfall the clamp cuts off. Worked through on paper in review (not yet seen in a game),
        /// a touch-up pass over a finished terrace finds a few millimetres at dozens of points,
        /// which sums to several times MinVolume. Without this floor that touch-up would be a paid
        /// swing, and since a paid swing lifts its whole circle, it would charge for every point
        /// under it that sits a hair low: each swing takes part of a stone, the panel flips between
        /// a price and "already paid for", and with an empty pack every swing over your own flat
        /// yard says you are out of stone. Ground hoed before this version carries used-up room
        /// too, so it is not only filled ground.
        ///
        /// Per point rather than per swing, because the complaint is per point: a dip you cannot
        /// see is not worth stone however many of them one swing covers. Leaving it to the hoe
        /// rather than raising it free is what stops the floor becoming a free raise in small
        /// steps. Three centimetres is under what the terrain mesh shows as a bump, and a point
        /// short by more than that is lifted by the next paid swing that covers it.
        /// </summary>
        private const float MinLift = 0.03f;

        /// <summary>
        /// How long after asking for a fill a bill is still accepted, in seconds. A bill with no
        /// request behind it is either very late or not ours, and neither should take stone.
        /// </summary>
        private const float BillWindow = 10f;

        /// <summary>Whole items out of float arithmetic need a little slack in both directions.</summary>
        private const float Slack = 0.0001f;

        /// <summary>
        /// The distance a swing asks for when the pack pays for all of it. Infinite rather than
        /// the swing's own radius, so it means "no limit" without the owner having to agree with
        /// the swinger on what that radius was.
        /// </summary>
        internal const float Everywhere = float.PositiveInfinity;

        /// <summary>
        /// Two lifted points this close in distance from the middle are the same ring, in metres.
        /// Grid points at the same distance come out of the same float arithmetic bit for bit, and
        /// neighbouring rings are centimetres apart even at a thirty metre reach, so any small
        /// number will do; this one is well clear of both.
        /// </summary>
        private const float SameRing = 0.001f;

        // Private on TerrainComp, and bound lazily inside a try/catch for the reason Flat.cs spells
        // out: a FieldRefAccess that throws in a static initialiser poisons every Harmony patch the
        // class carries, and the symptom is unrelated vanilla features breaking.
        private static AccessTools.FieldRef<TerrainComp, bool[]> _modifiedHeight;
        private static AccessTools.FieldRef<TerrainComp, float[]> _levelDelta;
        private static AccessTools.FieldRef<TerrainComp, float[]> _smoothDelta;

        private static bool _bound;
        private static bool _bindFailed;

        private static bool Bind()
        {
            if (_bound) return !_bindFailed;
            _bound = true;

            try
            {
                _modifiedHeight = AccessTools.FieldRefAccess<TerrainComp, bool[]>("m_modifiedHeight");
                _levelDelta = AccessTools.FieldRefAccess<TerrainComp, float[]>("m_levelDelta");
                _smoothDelta = AccessTools.FieldRefAccess<TerrainComp, float[]>("m_smoothDelta");
            }
            catch (Exception e)
            {
                _bindFailed = true;
                JafnaPlugin.Log.LogWarning(
                    "Could not reach TerrainComp's height arrays, so raising ground with stone is off "
                    + "for this session and flattening keeps vanilla's one metre ceiling. " + e.Message);
            }

            return !_bindFailed;
        }

        /// <summary>
        /// Whether this op is one a fill applies to: a smooth that is not also a level.
        ///
        /// A level op has no ceiling to fill. <c>LevelTerrain</c> sets each point to its target
        /// outright and banks it into the level delta, so it already reaches any height up to
        /// vanilla's eight metre limit, and charging for it would be charging for something the
        /// game gives away.
        /// </summary>
        internal static bool Applies(TerrainOp.Settings settings)
        {
            return settings != null && settings.m_smooth && !settings.m_level;
        }

        // -- The price -------------------------------------------------------------------------

        /// <summary>What a cubic metre of raised ground costs, read off a tool's Raise ground entry.</summary>
        internal sealed class Price
        {
            internal PieceTable Table;
            internal Piece Piece;
            internal float Scale;

            /// <summary>Cubic metres one Raise ground swing adds to flat ground.</summary>
            internal float Volume;

            /// <summary>Shared item names, $item_stone and the like.</summary>
            internal string[] Items;

            /// <summary>How many of each item one cubic metre costs.</summary>
            internal float[] PerCubicMetre;

            /// <summary>
            /// The world key that makes the entry free, taken when the price is read so a bill that
            /// arrives after a logout never has to touch a Piece that may have been unloaded.
            /// </summary>
            internal GlobalKeys FreeKey;

            internal bool IsFree()
            {
                return Items.Length == 0
                       || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(FreeKey));
            }
        }

        private static Price _price;

        /// <summary>A table already searched and found to carry no raise entry, so it is not searched every frame.</summary>
        private static PieceTable _noRaiseOn;

        /// <summary>
        /// The price of raising ground with the tool in the player's hands, or null when that tool
        /// has no Raise ground entry to take a price from.
        ///
        /// The first entry in the table that raises is the one used. Vanilla fills its tables
        /// before any mod adds to them, so on the hoe that is Raise ground itself, and choosing by
        /// position rather than by the cheapest keeps a mod's discount entry from quietly becoming
        /// the rate. The entry and what it resolved to are logged once, because both are asset
        /// data: nothing on disk says what Raise ground costs or how far it reaches.
        /// </summary>
        internal static Price Resolve(Player player, float scale)
        {
            if (player == null) return null;

            PieceTable table = player.GetBuildTool();
            if (table == null) return null;

            if (_price != null && _price.Table == table && _price.Piece != null && _price.Scale == scale)
            {
                return _price;
            }

            if (_noRaiseOn == table) return null;

            for (int i = 0; i < table.m_pieces.Count; i++)
            {
                GameObject prefab = table.m_pieces[i];
                if (prefab == null) continue;
                if (!prefab.TryGetComponent(out TerrainOp op) || op.m_settings == null) continue;

                TerrainOp.Settings s = op.m_settings;
                if (!s.m_raise || s.m_raiseDelta <= 0f || s.m_raiseRadius <= 0f) continue;
                if (!prefab.TryGetComponent(out Piece piece)) continue;

                float volume = RaiseVolume(s, scale);
                if (volume <= MinVolume) continue;

                List<string> items = new List<string>();
                List<float> rates = new List<float>();
                string cost = "";

                // The same filter vanilla's ConsumeResources applies with no station in use, which
                // is the case for anything built with a hoe: upgrade-only requirements are skipped,
                // and so is anything with no item or no amount.
                foreach (Piece.Requirement req in piece.m_resources)
                {
                    if (req == null || req.m_resItem == null || req.m_upgraderResource) continue;

                    int amount = req.GetAmount(0);
                    if (amount <= 0) continue;

                    items.Add(req.m_resItem.m_itemData.m_shared.m_name);
                    rates.Add(amount / volume);
                    cost += (cost.Length > 0 ? ", " : "") + req.m_resItem.name + " x" + amount;
                }

                _price = new Price
                {
                    Table = table,
                    Piece = piece,
                    Scale = scale,
                    Volume = volume,
                    Items = items.ToArray(),
                    PerCubicMetre = rates.ToArray(),
                    FreeKey = piece.FreeBuildKey()
                };

                string rate = "";
                for (int k = 0; k < _price.Items.Length; k++)
                {
                    rate += (rate.Length > 0 ? ", " : "")
                            + _price.PerCubicMetre[k].ToString("0.000") + " " + _price.Items[k];
                }

                JafnaPlugin.Log.LogInfo(
                    "Raising ground is priced off '" + prefab.name + "' on " + table.name + ": raise radius "
                    + s.m_raiseRadius.ToString("0.00") + ", delta " + s.m_raiseDelta.ToString("0.00")
                    + ", power " + s.m_raisePower.ToString("0.00") + ", square " + s.m_square
                    + ", so one swing adds " + volume.ToString("0.00") + " cubic metres to flat ground for "
                    + (cost.Length > 0 ? cost : "nothing") + ". That is "
                    + (rate.Length > 0 ? rate : "free") + " per cubic metre.");

                return _price;
            }

            _noRaiseOn = table;
            JafnaPlugin.Log.LogInfo(
                "'" + table.name + "' has no Raise ground entry to take a price from, so flattening "
                + "with it keeps vanilla's one metre ceiling.");
            return null;
        }

        /// <summary>
        /// Cubic metres one swing of this raise op adds to flat ground, at this heightmap scale.
        ///
        /// Walked point by point with <c>RaiseTerrain</c>'s own footprint and falloff rather than
        /// integrated as a cone, because the game does it on a grid and the grid is what the stone
        /// was priced against. A square op lifts every point in its square by the full delta; a
        /// round one lifts each point within the radius by <c>delta * (1 - d/r)^power</c>, or by
        /// the full delta when power is zero.
        ///
        /// Flat ground is the full-value swing. RaiseTerrain lifts a point toward the ghost's height
        /// plus that amount and never by more than that amount, so on flat ground under the ghost
        /// every point gets all of it. On a slope a vanilla swing adds less for the same stone, so
        /// there filling is the cheaper way up.
        ///
        /// It is not the cheapest way up overall, and the README says so. RaiseTerrain also zeroes
        /// the smooth delta at every point it lifts, which hands the next Level ground swing its
        /// free metre back, so a player alternating Raise ground and Level ground by hand gets that
        /// metre again on every pair of swings. A fill deliberately does not give it back (see the
        /// class comment), so hand alternation can beat this rate. That is a known gap, written
        /// down, rather than an oversight.
        /// </summary>
        internal static float RaiseVolume(TerrainOp.Settings s, float scale)
        {
            if (s == null || scale <= 0f) return 0f;

            float reach = s.m_raiseRadius / scale;
            if (reach <= 0f) return 0f;

            int span = Mathf.CeilToInt(reach);
            float sum = 0f;

            for (int dy = -span; dy <= span; dy++)
            {
                for (int dx = -span; dx <= span; dx++)
                {
                    float weight = 1f;

                    if (!s.m_square)
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > reach) continue;

                        if (s.m_raisePower > 0f)
                        {
                            weight = 1f - d / reach;
                            if (s.m_raisePower != 1f) weight = Mathf.Pow(weight, s.m_raisePower);
                        }
                    }

                    sum += s.m_raiseDelta * weight;
                }
            }

            return sum * scale * scale;
        }

        internal static float ScaleAt(Vector3 point)
        {
            Heightmap hmap = Heightmap.FindHeightmap(point);
            return hmap == null ? 1f : hmap.m_scale;
        }

        // -- The ledger ------------------------------------------------------------------------

        /// <summary>
        /// Items already paid for and not yet used, by shared name. Negative is a debt.
        ///
        /// This is the "neither free nor a whole stone each" part. A fill costing 0.3 of a stone
        /// takes a whole one and keeps 0.7 toward the next, so small raises are paid for at the
        /// same rate as large ones and nothing rounds away in either direction.
        ///
        /// Held for one character in one session, like the held height. It is never more than
        /// one of each item, and saving it would mean a value of ours in the player profile for
        /// the sake of less than a stone.
        /// </summary>
        private static readonly Dictionary<string, float> Credit = new Dictionary<string, float>();

        private static long _ledgerFor;
        private static ZNet _ledgerIn;

        /// <summary>
        /// Clears the ledger when the character or the session changes.
        ///
        /// The character alone is not enough, and the first version keyed on it alone.
        /// <c>GetPlayerID</c> is the id saved in the profile, the same after every logout and in
        /// every world, so credit and debt followed a character out of a server and into a
        /// singleplayer world while the README, the cfg and the scenario all said they ended at
        /// logout. The session is the <c>ZNet</c> instance: the game builds one when you enter a
        /// world and destroys it when you leave, and Unity's <c>==</c> reads a destroyed one as
        /// null, so a new world never compares equal to the last. A respawn keeps both, which is
        /// right, because dying is not a way to clear a debt.
        /// </summary>
        private static void Ledger(Player player)
        {
            long id = player.GetPlayerID();
            ZNet net = ZNet.instance;
            if (id == _ledgerFor && net == _ledgerIn) return;

            _ledgerFor = id;
            _ledgerIn = net;
            Credit.Clear();
        }

        private static float CreditOf(string item)
        {
            return Credit.TryGetValue(item, out float credit) ? credit : 0f;
        }

        /// <summary>
        /// How many of <paramref name="item"/> the swing's own entry costs, which vanilla takes
        /// after the swing and which the fill must therefore leave in the pack.
        ///
        /// The order is the whole reason. When this machine owns the zone the bill is paid inside
        /// <c>TerrainOp.Awake</c>, which runs inside <c>PlacePiece</c>, and vanilla only calls
        /// <c>ConsumeResources</c> for the entry after that returns. <c>Inventory.RemoveItem</c>
        /// takes what is there and says nothing when it is short, and the "can you afford it"
        /// check ran before the swing. So a fill allowed to count every stone could spend the
        /// entry's own price, and vanilla would then collect it short or not at all, in silence.
        /// The hoe's Level ground costs nothing as far as anyone has seen, but whether any
        /// flattening entry costs stone is asset data, and Paved road may well; the selection
        /// line in the log says, with Verbose on.
        ///
        /// The same filter as ConsumeResources with no station, and nothing when the entry's
        /// free-build key is set, because vanilla skips ConsumeResources then.
        /// </summary>
        private static int OwnCost(Piece own, string item)
        {
            if (own == null || own.m_resources == null) return 0;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(own.FreeBuildKey())) return 0;

            int total = 0;

            foreach (Piece.Requirement req in own.m_resources)
            {
                if (req == null || req.m_resItem == null || req.m_upgraderResource) continue;
                if (req.m_resItem.m_itemData.m_shared.m_name != item) continue;

                int amount = req.GetAmount(0);
                if (amount > 0) total += amount;
            }

            return total;
        }

        /// <summary>
        /// Cubic metres of fill the player can pay for right now, counting what is already paid
        /// and leaving what the swing's own entry costs. Infinity when nothing is limiting: a free
        /// entry, a NoBuildCost world, or the nocost cheat, which vanilla also lets past its
        /// requirement check.
        /// </summary>
        private static float Affordable(Player player, Price price, Piece own, out string limiting)
        {
            limiting = null;

            if (price.IsFree() || player.NoCostCheat()) return float.PositiveInfinity;

            Ledger(player);
            Inventory pack = player.GetInventory();

            float volume = float.PositiveInfinity;

            for (int k = 0; k < price.Items.Length; k++)
            {
                // CountItems with vanilla's defaults, so an item vanilla's own requirement check
                // would not count is not counted here either.
                float have = CreditOf(price.Items[k])
                             + (pack == null ? 0 : pack.CountItems(price.Items[k]))
                             - OwnCost(own, price.Items[k]);
                float covers = Mathf.Max(0f, have) / price.PerCubicMetre[k];

                if (covers < volume)
                {
                    volume = covers;
                    limiting = price.Items[k];
                }
            }

            return volume;
        }

        // -- Measuring ---------------------------------------------------------------------------

        /// <summary>
        /// One point a paid swing lifts onto the height, by how many metres past what the hoe
        /// moves it for free, and where it stands.
        /// </summary>
        private struct Lift
        {
            public int Index;
            public float Metres;

            /// <summary>
            /// Metres from the middle of the swing, grid point to grid point the way the smooth
            /// itself measures, so the swinger and the owner, and the two zones either side of a
            /// line, all get the same number for the same vertex.
            /// </summary>
            public float Distance;

            /// <summary>
            /// Cubic metres this point adds to the bill. Zero on the row a neighbouring heightmap
            /// counts, which is raised here as well and paid for there (see <see cref="Measure"/>).
            /// </summary>
            public float Volume;
        }

        private static readonly List<Lift> Lifts = new List<Lift>();
        private static readonly List<Lift> Planned = new List<Lift>();
        private static readonly List<Heightmap> Maps = new List<Heightmap>();

        private static readonly Comparison<Lift> Nearer = (a, b) => a.Distance.CompareTo(b.Distance);

        /// <summary>
        /// Cubic metres a paid swing centred on <paramref name="centre"/> lifts onto the height, on
        /// one heightmap. <paramref name="clamped"/> gets the part of that which the hoe's own swing
        /// wants and its one metre clamp cuts off, which is what decides whether a swing needs
        /// stone at all (see the class comment). Records each point it would lift when
        /// <paramref name="record"/> is given.
        ///
        /// Deliberately <c>SmoothTerrain</c>'s own arithmetic, point for point: the same footprint
        /// (always round, whatever <c>m_square</c> says), the same falloff with the same fast path
        /// for a power of three, the same Lerp toward the same target, and the same clamp on the
        /// sum. That is how the fill knows what the smooth, running after it, will leave in
        /// <c>m_smoothDelta</c> at each point, called settled here, and so what
        /// <c>m_levelDelta</c> must hold for the two to land the point on the height. The finished
        /// height is the ground plus both deltas (<c>ApplyToHeightmap</c>), so the level delta
        /// wanted is the height less the ground less the settled smooth delta, and the lift billed
        /// is that less what is banked there already. The free part is then whatever the smooth
        /// moves the point by, which is vanilla's own number by construction. If this walked a
        /// different set of points, or eased them differently, the fill would lift ground the swing
        /// never touches, or land the points it does touch off the height by the difference.
        ///
        /// A point at or above the height is left alone: the hoe eases it down for free, and a flat
        /// top is only ever built upward.
        ///
        /// Three limits are respected on the way:
        ///
        ///  - Vanilla clamps the finished height to eight metres either side of the ground the
        ///    heights are kept against, and the level delta to eight metres. That ground is the
        ///    height less both deltas, their sum clamped to eight metres the way
        ///    <c>ApplyToHeightmap</c> clamps it. A point whose level is further above that ground
        ///    than eight metres can never be brought to it, by any number of swings or any amount
        ///    of stone, so it is not lifted at all: not raised, not charged, and not in the price
        ///    the panel quotes. Nor is a point whose level would need more than eight metres in the
        ///    level delta, which only happens where the hoe dug before and the smooth delta is still
        ///    below zero after this swing. The first version filled such points as far as the limit
        ///    and charged for that, which is the half-raised, paid-for ground Robbin's rule in the
        ///    class comment is against. <see cref="MinLift"/> of slack, so a level a centimetre past
        ///    the limit is still filled to it rather than left a metre short. Every point left out
        ///    this way is counted in <paramref name="beyond"/>, because leaving it out in silence
        ///    was the bug this whole feature exists to fix: the panel shows a height and the ground
        ///    stops short of it with nothing on screen to say why. Plan says so in the middle of the
        ///    screen and the panel says so under the price. Only points that would otherwise have
        ///    been lifted count, which is why the <see cref="MinLift"/> test comes first.
        ///  - A point needing less than <see cref="MinLift"/> is left to the hoe, and a clamped
        ///    shortfall smaller than it does not count toward the swing needing stone. Why is on
        ///    the constant.
        ///  - A heightmap's last row and column are the same vertices as its neighbour's first, and
        ///    a swing that reaches them reaches the neighbour too. Both lift them, from the same
        ///    numbers, so the seam stays closed, but only the neighbour counts them, so the line is
        ///    not paid for twice.
        /// </summary>
        private static float Measure(
            Heightmap hmap, float[] level, float[] smooth, Vector3 centre, float radius, float power,
            List<Lift> record, ref int beyond, ref float clamped)
        {
            if (hmap == null || radius <= 0f) return 0f;

            float scale = hmap.m_scale;
            float reach = radius / scale;
            if (reach <= 0f) return 0f;

            hmap.WorldToVertex(centre, out int cx, out int cy);

            float target = centre.y - hmap.transform.position.y;
            int span = Mathf.CeilToInt(reach);
            int width = hmap.m_width;
            int pitch = width + 1;
            Vector2 middle = new Vector2(cx, cy);

            float smoothClamp = Heightmap.c_SmoothMaxDelta;
            float levelClamp = Heightmap.c_LevelMaxDelta;

            float volume = 0f;

            for (int iy = cy - span; iy <= cy + span; iy++)
            {
                for (int ix = cx - span; ix <= cx + span; ix++)
                {
                    float d = Vector2.Distance(middle, new Vector2(ix, iy));
                    if (d > reach || ix < 0 || iy < 0 || ix >= pitch || iy >= pitch) continue;

                    float height = hmap.GetHeight(ix, iy);
                    if (height >= target) continue;

                    float u = d / reach;
                    u = power != 3f ? Mathf.Pow(u, power) : u * u * u;

                    int n = iy * pitch + ix;
                    float smoothed = smooth != null && n < smooth.Length ? smooth[n] : 0f;
                    float levelled = level != null && n < level.Length ? level[n] : 0f;

                    // What the smooth is about to do here, in its own order: ease toward the
                    // target, add that to the smooth delta, clamp the sum to a metre. What the
                    // clamp throws away is the part of the hoe's own swing it cannot do.
                    float eased = Mathf.Lerp(height, target, 1f - u) - height;
                    float settled = Mathf.Clamp(smoothed + eased, -smoothClamp, smoothClamp);
                    float cut = smoothed + eased - settled;

                    float ground = height - Mathf.Clamp(levelled + smoothed, -levelClamp, levelClamp);
                    float top = Mathf.Min(target, ground + levelClamp);
                    float banked = top - ground - settled;
                    float lift = banked - levelled;
                    if (lift < MinLift) continue;

                    if (target - ground > levelClamp + MinLift || banked > levelClamp + MinLift)
                    {
                        beyond++;
                        continue;
                    }

                    // Inside the slack the level lands on the limit rather than a hair past it.
                    // After the cap, so a point one centimetre short of the eight metre limit is
                    // skipped too rather than billed for a lift nobody can see.
                    if (banked > levelClamp) lift = levelClamp - levelled;
                    if (lift < MinLift) continue;

                    bool counted = ix < width && iy < width;
                    float paid = counted ? lift * scale * scale : 0f;

                    if (counted && cut >= MinLift) clamped += Mathf.Min(cut, lift) * scale * scale;

                    if (record != null)
                    {
                        record.Add(new Lift { Index = n, Metres = lift, Distance = d * scale, Volume = paid });
                    }

                    volume += paid;
                }
            }

            return volume;
        }

        /// <summary>
        /// Cubic metres a paid flattening swing here would lift onto the height, read from this
        /// client's copy of the ground, and in <paramref name="clamped"/> how much of it the hoe's
        /// clamp cuts off, which says whether the swing is a paid one at all (see
        /// <see cref="Measure"/>). Used for the readout every frame and once per swing to decide
        /// how much of the fill the pack pays for.
        ///
        /// The target is worked out per heightmap exactly as the owner will work it out: the held
        /// height when there is one, otherwise whatever <see cref="Flat.Target"/> says for that
        /// zone. A zone with no compiler yet has never been shaped, so its deltas are all zero and
        /// its target is the crosshair, which is also what the owner will find.
        ///
        /// <paramref name="fresh"/> is for the one call per swing that decides how far the fill
        /// reaches: it brings any heightmap this machine shaped earlier in the frame up to date
        /// first, so that when this machine also owns the zone it measures the same ground the
        /// owner half is about to (see <see cref="Freshen(Heightmap, bool)"/>). The readout passes
        /// false, since a stale frame there costs one frame of a number and a rebuild costs a mesh.
        /// Both hand in <paramref name="record"/>, to find how far from the middle the pack
        /// reaches: the swing to ask for that distance, the readout to price it.
        /// <paramref name="beyond"/> is how many points were left out for the eight metre limit
        /// (see <see cref="Measure"/>), so both can say so.
        /// </summary>
        private static float Estimate(
            TerrainOp.Settings settings, Vector3 point, float radius, bool fresh, List<Lift> record,
            out int beyond, out float clamped)
        {
            beyond = 0;
            clamped = 0f;
            if (settings == null || !Bind()) return 0f;

            Vector3 probe = point + Vector3.up * settings.m_levelOffset;
            bool square = Reach.IsSquare(settings);

            Maps.Clear();
            Heightmap.FindHeightmap(point, radius, Maps);

            float need = 0f;

            for (int i = 0; i < Maps.Count; i++)
            {
                Heightmap hmap = Maps[i];
                if (hmap == null) continue;

                if (fresh) Freshen(hmap, true);

                TerrainComp comp = TerrainComp.FindTerrainCompiler(hmap.transform.position);

                float target;
                if (Held.Active) target = Held.Height;
                else if (comp != null) target = Flat.Target(comp, probe, radius, square, out Flat.Source _);
                else target = probe.y;

                float[] level = comp != null ? _levelDelta(comp) : null;
                float[] smooth = comp != null ? _smoothDelta(comp) : null;

                need += Measure(
                    hmap, level, smooth, new Vector3(probe.x, target, probe.z), radius, settings.m_smoothPower, record,
                    ref beyond, ref clamped);
            }

            return need;
        }

        // -- The swinging client ---------------------------------------------------------------

        private static TerrainOp _plannedOp;
        private static float _plannedReach = -1f;

        /// <summary>The price the last request was made at, so its bill is paid at the same rate.</summary>
        private static Price _billedAt;

        private static float _askedAt = float.NegativeInfinity;

        /// <summary>
        /// The frame the last request was made in, and what the swing's own entry costs of each
        /// priced item. A bill paid in that same frame is being paid inside the swing, before
        /// vanilla's ConsumeResources, so it leaves the entry's cost in the pack. A bill arriving
        /// in any later frame came over the network after vanilla had already taken it.
        /// </summary>
        private static int _askedFrame = -1;

        private static int[] _reserved = new int[0];

        /// <summary>
        /// How far from the middle of this swing the pack pays to raise the ground, in metres:
        /// every point below the height nearer than this is lifted onto it and every one further
        /// out is left to the hoe's own easing. <see cref="Everywhere"/> when the pack pays for all
        /// of it, and -1 when the swing is not asking for a fill at all: when it does not need stone,
        /// and when the pack cannot pay for even the middle.
        ///
        /// Decided once per swing and then handed to every zone the swing reaches, because
        /// ApplyOperation is called once per zone and the distance has to be the same in all of
        /// them (see the class comment). The op object is the key: TerrainOp.Awake hands the same
        /// one to every zone and then destroys it, so the next swing is always a different object.
        ///
        /// This is also where running short is said. Once per swing, at the moment of the swing,
        /// rather than when a bill comes back, so it lands on the click that caused it. Ground
        /// too far below the height for the game to ever allow is said here too, and for the same
        /// reason; the build panel says it before the swing as well. Only one of the two, because
        /// a centre message replaces the one before it outright, and running short goes first:
        /// it is the one more stone fixes, and the other is still on the panel.
        /// </summary>
        internal static float Plan(TerrainOp op, Player player, float radius)
        {
            if (op == null) return -1f;
            if (op == _plannedOp) return _plannedReach;

            _plannedOp = op;
            _plannedReach = -1f;

            if (!JafnaConfig.AutoRaise.Value || player == null) return -1f;

            TerrainOp.Settings settings = op.m_settings;
            if (!Applies(settings) || !IsSwingOf(player, op)) return -1f;

            Vector3 point = op.transform.position;

            // Inside a dungeon the heightmaps under the swing are the surface above it, because
            // Heightmap.IsPointInside compares only X and Z. The hoe's Level ground cannot land
            // there anyway - it is a ground piece and the placement ray finds no terrain indoors,
            // so the ghost is switched off - but a flattening entry on another tool might, and
            // the fill would then lift the surface over the entrance by up to eight metres.
            if (Character.InInterior(point)) return -1f;

            Price price = Resolve(player, ScaleAt(point));
            if (price == null) return -1f;

            Planned.Clear();
            float need = Estimate(settings, point, radius, true, Planned, out int beyond, out float clamped);

            // Whether the swing needs stone is decided on what the hoe's clamp cuts off, and what
            // it costs on the whole flat top. A swing the hoe can do alone stays vanilla.
            if (clamped <= MinVolume)
            {
                if (beyond > 0) player.Message(MessageHud.MessageType.Center, "Too far below to raise");
                return -1f;
            }

            Piece own = player.GetSelectedPiece();
            float affordable = Affordable(player, price, own, out string limiting);
            float reach = affordable >= need ? Everywhere : Within(Planned, affordable);

            // On the reach rather than on the two volumes, so the message and the fill cannot
            // disagree over a rounding difference between two ways of adding the same numbers.
            if (!float.IsPositiveInfinity(reach))
            {
                // Localised by MessageHud, so the item's own $ token comes out as its name.
                player.Message(MessageHud.MessageType.Center,
                    "Not enough " + (limiting ?? "$item_stone") + " to raise all of it");
            }
            else if (beyond > 0)
            {
                player.Message(MessageHud.MessageType.Center, "Too far below to raise all of it");
            }

            if (JafnaConfig.Verbose.Value)
            {
                JafnaPlugin.Log.LogInfo(
                    "Fill wanted " + need.ToString("0.00") + " cubic metres, the pack covers "
                    + (float.IsPositiveInfinity(affordable) ? "all of it" : affordable.ToString("0.00"))
                    + ", so it raises " + Describe(reach)
                    + (beyond > 0 ? ", and leaves " + beyond + " points past the eight metre limit alone" : "")
                    + ".");
            }

            if (reach <= 0f) return -1f;

            if (_reserved.Length != price.Items.Length) _reserved = new int[price.Items.Length];
            for (int k = 0; k < price.Items.Length; k++) _reserved[k] = OwnCost(own, price.Items[k]);

            _billedAt = price;
            _askedAt = Time.time;
            _askedFrame = Time.frameCount;
            _plannedReach = reach;
            return reach;
        }

        /// <summary>
        /// How far from the middle every point below the height can be lifted onto it on
        /// <paramref name="affordable"/> cubic metres, or 0 when not even the middle can.
        ///
        /// A ring at a time. Points the same distance from the middle go in together or not at
        /// all, because raising one of four equally placed points and not the others would choose
        /// between them by the order a loop visits them, and two zones meeting across that ring
        /// would choose differently.
        ///
        /// The answer is half way between the last ring paid for and the first one that is not,
        /// rather than the last ring's own distance. The owner compares its own distances against
        /// it, and a line drawn exactly through a ring would leave that ring to float rounding.
        /// </summary>
        private static float Within(List<Lift> lifts, float affordable)
        {
            lifts.Sort(Nearer);

            float spent = 0f;
            float reached = -1f;
            int i = 0;

            while (i < lifts.Count)
            {
                float distance = lifts[i].Distance;
                float ring = 0f;
                int j = i;

                while (j < lifts.Count && lifts[j].Distance - distance < SameRing)
                {
                    ring += lifts[j].Volume;
                    j++;
                }

                if (spent + ring > affordable) return reached < 0f ? 0f : (reached + distance) * 0.5f;

                spent += ring;
                reached = distance;
                i = j;
            }

            return Everywhere;
        }

        /// <summary>A reach in words for the Verbose log, which would otherwise print infinity.</summary>
        private static string Describe(float reach)
        {
            if (float.IsPositiveInfinity(reach)) return "the whole swing";
            if (reach <= 0f) return "nothing";
            return "every low point within " + reach.ToString("0.00") + "m of the middle";
        }

        /// <summary>
        /// Whether this op is the local player swinging the tool in their hands.
        ///
        /// Any TerrainOp instantiated on this machine goes through ApplyOperation - a pickaxe's dig,
        /// a location's own shaping, Devkit's flatten - and none of them should ever take stone out
        /// of a pack. The selected build piece being the op's own prefab is the test that only a
        /// swing passes.
        /// </summary>
        private static bool IsSwingOf(Player player, TerrainOp op)
        {
            if (!player.InPlaceMode()) return false;

            PieceTable table = player.GetBuildTool();
            if (table == null) return false;

            GameObject selected = table.GetSelectedPrefab();
            if (selected == null) return false;

            return Utils.GetPrefabName(op.gameObject.name) == selected.name;
        }

        /// <summary>Listens for bills on a zone's compiler. Called from TerrainComp.Awake on every client.</summary>
        internal static void Listen(TerrainComp comp)
        {
            ZNetView nview = Reach.View(comp);
            if (nview == null) return;

            try
            {
                nview.Register<float>(BillRpc, OnBill);
            }
            catch (ArgumentException)
            {
                // Already registered. Awake runs once per instance, so this is not expected; it is
                // caught because a duplicate registration is a Dictionary.Add and would otherwise
                // throw out of TerrainComp.Awake and take the zone's terrain with it.
            }
        }

        /// <summary>
        /// A zone owner reporting what it raised for our swing, in cubic metres. Paid from the pack.
        ///
        /// Paid regardless of whether the mod is still enabled. The ground has already been raised,
        /// and a host switching the mod off mid-swing should not make that swing free.
        /// </summary>
        private static void OnBill(long sender, float used)
        {
            if (used <= 0f) return;

            if (Time.time - _askedAt > BillWindow)
            {
                if (JafnaConfig.Verbose.Value)
                {
                    JafnaPlugin.Log.LogInfo(
                        "Ignored a bill for " + used.ToString("0.00") + " cubic metres that no swing of ours asked for.");
                }
                return;
            }

            Pay(used);
        }

        private static void Pay(float used)
        {
            Player player = Player.m_localPlayer;
            Price price = _billedAt;
            if (player == null || price == null) return;

            if (price.IsFree()) return;

            Ledger(player);

            Inventory pack = player.GetInventory();
            bool cheat = player.NoCostCheat();

            // Still inside the swing, so vanilla has not yet taken the entry's own price.
            bool inSwing = Time.frameCount == _askedFrame && _reserved.Length == price.Items.Length;

            for (int k = 0; k < price.Items.Length; k++)
            {
                string item = price.Items[k];
                float credit = CreditOf(item) - used * price.PerCubicMetre[k];
                int taken = 0;

                if (credit < 0f && pack != null)
                {
                    int want = Mathf.CeilToInt(-credit - Slack);
                    int spare = pack.CountItems(item) - (inSwing ? _reserved[k] : 0);
                    taken = Mathf.Min(want, Mathf.Max(0, spare));

                    if (taken > 0)
                    {
                        // RemoveItem with vanilla's defaults, the call ConsumeResources makes.
                        pack.RemoveItem(item, taken);
                        credit += taken;
                    }
                }

                // Under the nocost cheat vanilla takes what you carry and waives the rest, and so
                // does this. Otherwise what could not be taken stays owed, which only happens when
                // somebody else owns the zone and two swings were in flight at once.
                if (credit < 0f && (cheat || credit > -Slack)) credit = 0f;

                Credit[item] = credit;

                if (JafnaConfig.Verbose.Value)
                {
                    JafnaPlugin.Log.LogInfo(
                        "Paid " + taken + " " + item + " for " + used.ToString("0.00") + " cubic metres of fill, "
                        + (credit >= 0f ? "carrying " + credit.ToString("0.00") + " toward the next"
                                        : "still owing " + (-credit).ToString("0.00")) + ".");
                }
            }
        }

        // -- The zone owner -----------------------------------------------------------------------

        /// <summary>Whether this op, arriving with this reach, is one <see cref="Apply"/> will raise ground for.</summary>
        internal static bool Wants(TerrainOp.Settings modifier, float reach)
        {
            return reach > 0f && JafnaConfig.AutoRaise.Value && Applies(modifier);
        }

        // Heightmaps an op on this machine changed the heights of this frame, and the subset of
        // those a fill raised. Keyed by frame rather than cleared from a LateUpdate of ours,
        // because the heightmap's own LateUpdate is what makes an entry stale and there is no
        // ordering between the two to rely on.
        private static int _frame = -1;
        private static readonly HashSet<Heightmap> Shaped = new HashSet<Heightmap>();
        private static readonly HashSet<Heightmap> Filled = new HashSet<Heightmap>();

        private static void Today()
        {
            if (_frame == Time.frameCount) return;

            _frame = Time.frameCount;
            Shaped.Clear();
            Filled.Clear();
        }

        /// <summary>
        /// Notes that an op on this machine changed the heights on this compiler's heightmap.
        /// Called from a DoOperation postfix for every op, fill or not.
        ///
        /// Only level, raise and smooth ops count. A paint-only op, and the paint that spreads
        /// into a neighbouring zone when a swing reaches the edge of its own, pokes a heightmap
        /// for a rebuild without moving a single height, and treating those as stale was what
        /// made the first version rebuild the next zone's collision and render meshes twice on
        /// every fill swing that crossed a zone line.
        /// </summary>
        internal static void Touched(TerrainComp comp, TerrainOp.Settings modifier)
        {
            if (comp == null || modifier == null) return;
            if (!modifier.m_level && !modifier.m_raise && !modifier.m_smooth) return;
            if (!JafnaConfig.AutoRaise.Value) return;

            Heightmap hmap = Heightmap.FindHeightmap(comp.transform.position);
            if (hmap == null) return;

            Today();
            Shaped.Add(hmap);
        }

        /// <summary>
        /// Brings a compiler's heightmap up to date before an op reads it, when that matters.
        /// Called from the DoOperation prefix for every op, before anything reads a height.
        /// </summary>
        internal static void Freshen(TerrainComp comp, bool filling)
        {
            Today();
            if (Filled.Count == 0 && (!filling || Shaped.Count == 0)) return;
            if (comp == null) return;

            Freshen(Heightmap.FindHeightmap(comp.transform.position), filling);
        }

        /// <summary>
        /// Rebuilds a heightmap now rather than at LateUpdate, in exactly two cases.
        ///
        /// An op reads heights off the heightmap, and DoOperation only asks for a rebuild at the
        /// end of the frame, so a second op on the same heightmap in the same frame reads the
        /// ground as it was before the first. Vanilla lives with that between its own ops. A fill
        /// moves ground by up to eight metres in one op where the hoe's flattening moves it by
        /// one, so the same overlap costs far more after a fill, and:
        ///
        ///  - A fill about to measure (<paramref name="filling"/>) rebuilds first if any op has
        ///    moved heights here this frame. Otherwise two fills landing together - somebody's
        ///    swings arriving in one packet - would each raise the same ground.
        ///  - Any op at all rebuilds first if a fill landed here this frame. Otherwise, say, a
        ///    level op arriving in the same frame would set its points relative to the unfilled
        ///    ground and leave the whole fill stacked on top of the height it aimed for.
        ///
        /// Vanilla op after vanilla op is left exactly as vanilla has it. It is the rebuild the
        /// heightmap's LateUpdate was going to do anyway, only sooner, and <c>Regenerate</c>
        /// clears the pending flag so it is not done twice unless something pokes it again. When
        /// nothing is pending the heights are already current - a zone synced from another
        /// machine rebuilds on the spot - and nothing is done.
        /// </summary>
        private static void Freshen(Heightmap hmap, bool filling)
        {
            if (hmap == null) return;

            Today();
            if (!Filled.Contains(hmap) && !(filling && Shaped.Contains(hmap))) return;

            Shaped.Remove(hmap);
            Filled.Remove(hmap);

            if (hmap.m_doLateUpdate == 0) return;

            hmap.Regenerate();
        }

        /// <summary>
        /// Lifts every point on this zone that lies nearer the middle than <paramref name="reach"/>
        /// metres and below the height onto the height, and sends the bill. Runs on the client that
        /// owns the zone, from the DoOperation prefix, before vanilla's SmoothTerrain.
        ///
        /// Each point all the way or not at all, which is Robbin's rule, and the whole circle when
        /// the pack pays for it, which is his flat top (see the class comment). A reach above zero
        /// is the swinger saying the swing needs stone, and it is taken as said rather than asked
        /// again here. The reach is the swinger's, and the points are this machine's own measure of
        /// the ground, so the bill is for what was raised here rather than what the swinger
        /// expected.
        ///
        /// Before, because the smooth then does its own part on top: this banks into the level
        /// delta whatever the smooth's own move will not cover, the smooth adds its eased amount
        /// to the smooth delta and clamps it, and together they put each point on the height. Both
        /// read the heights as they stood before the swing, since the heightmap is not rebuilt
        /// until the frame's LateUpdate, which is exactly why the two amounts add up rather than
        /// overlap, and why Measure can know the smooth's part before the smooth has run.
        ///
        /// That same delay is the one trap here, and <see cref="Freshen(Heightmap, bool)"/> is
        /// where it is handled. By the time this runs the DoOperation prefix has already called
        /// it, before the height was chosen; it is called again here only so this method stays
        /// correct if it is ever reached another way.
        /// </summary>
        internal static void Apply(TerrainComp comp, Vector3 centre, TerrainOp.Settings modifier, float reach, long sender)
        {
            if (!Wants(modifier, reach)) return;
            if (comp == null || !Bind()) return;

            // The owner's half of the interior test in Plan. The owner decides what is raised, so
            // it does not rely on every swinger having asked the question.
            if (Character.InInterior(centre)) return;

            Heightmap hmap = Heightmap.FindHeightmap(comp.transform.position);
            if (hmap == null) return;

            bool[] modified = _modifiedHeight(comp);
            float[] level = _levelDelta(comp);
            float[] smooth = _smoothDelta(comp);
            if (modified == null || level == null || smooth == null) return;

            Freshen(hmap, true);

            Lifts.Clear();
            // The swinger has already said whether any of it is too far below, and whether the
            // swing needs stone; the owner only leaves the too-far points out.
            int beyond = 0;
            float clamped = 0f;
            float need = Measure(
                hmap, level, smooth, centre, modifier.m_smoothRadius, modifier.m_smoothPower, Lifts, ref beyond,
                ref clamped);
            if (Lifts.Count == 0) return;

            float levelClamp = Heightmap.c_LevelMaxDelta;
            float used = 0f;
            int raised = 0;

            for (int i = 0; i < Lifts.Count; i++)
            {
                if (Lifts[i].Distance >= reach) continue;

                int n = Lifts[i].Index;
                level[n] = Mathf.Clamp(level[n] + Lifts[i].Metres, -levelClamp, levelClamp);
                modified[n] = true;

                used += Lifts[i].Volume;
                raised++;
            }

            if (raised == 0) return;

            Today();
            Filled.Add(hmap);

            if (JafnaConfig.Verbose.Value)
            {
                JafnaPlugin.Log.LogInfo(
                    "Filled " + used.ToString("0.00") + " of " + need.ToString("0.00") + " cubic metres at "
                    + centre.y.ToString("0.00") + "m, " + raised + " of " + Lifts.Count + " points, asked for "
                    + Describe(reach) + ", billing peer " + sender + ".");
            }

            if (used <= 0f) return;

            ZNetView nview = Reach.View(comp);
            if (nview == null || !nview.IsValid()) return;

            // To the swinger, on this zone's own view. When the swinger is this machine the routed
            // call is handled on the spot, inside this very swing, so the pack is settled before
            // the swing's next zone is even measured.
            nview.InvokeRPC(sender, BillRpc, used);
        }

        // -- The readout -----------------------------------------------------------------------

        /// <summary>What the readout should say about raising ground for the swing under the crosshair.</summary>
        internal enum Terms
        {
            None,
            Free,
            Paid,
            Covered,

            /// <summary>Short of stone: this swing fills the middle and takes stone for it.</summary>
            Short,

            /// <summary>Short of stone: this swing fills the middle on what is already paid for.</summary>
            ShortCovered,

            /// <summary>Short of stone: this swing cannot pay for even the middle, so it fills nothing.</summary>
            Unaffordable
        }

        /// <summary>Points recorded for the readout, so pricing a swing never disturbs a swing's own list.</summary>
        private static readonly List<Lift> Quoted = new List<Lift>();

        /// <summary>
        /// What a swing here would cost, for the build panel. <paramref name="cost"/> is what this
        /// swing takes out of the pack in whole items, after what is already paid, and
        /// <paramref name="all"/> what filling the whole of it would take; they differ only when
        /// the pack is short. <paramref name="carried"/> is what the pack holds and
        /// <paramref name="own"/> what the entry itself takes of the same items, which the short
        /// lines need and the others do not. <paramref name="far"/> is whether any of the swing is
        /// too far below the height for the game to ever allow (see <see cref="Measure"/>), which
        /// can be true whatever the terms, including None.
        ///
        /// Priced exactly as the swing will be billed: the same test for whether it needs stone,
        /// the same flat top, the same cutoff <see cref="Plan"/> will send, worked out by the same
        /// <see cref="Within"/>, and only the points inside it. The first version priced the whole
        /// fill whatever the pack held, and after the fill became middle first that was a price no
        /// swing would ever charge.
        ///
        /// Whole items, because that is what a player watches leave the pack. A fraction on screen
        /// would disagree with the pack on every swing and be right only on average.
        /// </summary>
        internal static Terms Quote(
            Player player, TerrainOp.Settings settings, Vector3 point, float radius,
            out string cost, out string all, out string carried, out string own, out bool far)
        {
            cost = null;
            all = null;
            carried = null;
            own = null;
            far = false;

            if (!JafnaConfig.AutoRaise.Value || player == null || !Applies(settings)) return Terms.None;

            // Plan's interior test, so the panel never prices a swing that would not be filled.
            if (Character.InInterior(point)) return Terms.None;

            Price price = Resolve(player, ScaleAt(point));
            if (price == null) return Terms.None;

            Quoted.Clear();
            float need = Estimate(settings, point, radius, false, Quoted, out int beyond, out float clamped);
            far = beyond > 0;

            // Plan's test for a swing that needs stone, so the panel is silent over exactly the
            // swings the hoe does alone, and prices the flat top over the rest.
            if (clamped <= MinVolume) return Terms.None;

            if (price.IsFree()) return Terms.Free;

            Ledger(player);
            Inventory pack = player.GetInventory();
            Piece selected = player.GetSelectedPiece();

            // The cheat lifts the limit and still takes what is carried, so it is never short but
            // is still quoted.
            float affordable = Affordable(player, price, selected, out string _);
            float reach = affordable >= need ? Everywhere : Within(Quoted, affordable);
            bool everything = float.IsPositiveInfinity(reach);
            float spent = everything ? need : Inside(Quoted, reach);

            bool anything = false;
            cost = "";
            all = "";
            carried = "";
            own = "";

            for (int k = 0; k < price.Items.Length; k++)
            {
                string item = price.Items[k];
                int now = WholeItems(spent, price, k);
                if (now > 0) anything = true;

                cost += (k > 0 ? ", " : "") + now + " " + item;
                all += (k > 0 ? ", " : "") + WholeItems(need, price, k) + " " + item;
                carried += (k > 0 ? ", " : "") + (pack == null ? 0 : pack.CountItems(item)) + " " + item;

                int entry = OwnCost(selected, item);
                if (entry > 0) own += (own.Length > 0 ? ", " : "") + entry + " " + item;
            }

            if (everything) return anything ? Terms.Paid : Terms.Covered;
            if (reach <= 0f) return Terms.Unaffordable;
            return anything ? Terms.Short : Terms.ShortCovered;
        }

        /// <summary>Cubic metres of fill inside a cutoff, counted the way the owner bills them in Apply.</summary>
        private static float Inside(List<Lift> lifts, float reach)
        {
            float spent = 0f;

            for (int i = 0; i < lifts.Count; i++)
            {
                if (lifts[i].Distance < reach) spent += lifts[i].Volume;
            }

            return spent;
        }

        /// <summary>
        /// Whole items of one kind a fill of this many cubic metres takes from the pack, after what
        /// is already paid. The arithmetic of <see cref="Pay"/>, which takes a whole item only
        /// when the ledger would otherwise go below nothing.
        /// </summary>
        private static int WholeItems(float volume, Price price, int k)
        {
            return Mathf.Max(0, Mathf.CeilToInt(volume * price.PerCubicMetre[k] - CreditOf(price.Items[k]) - Slack));
        }
    }
}

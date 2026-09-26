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
    /// Only the part the smooth could not do is charged. The first metre of movement at every
    /// point stays free, as it is in vanilla, and nothing is charged at all when nothing needs
    /// raising. The raise is banked into <c>m_levelDelta</c> and <c>m_smoothDelta</c> is left
    /// alone, which matters more than it looks: vanilla's raise op folds the smooth delta into the
    /// level delta and so hands the next smoothing swing a fresh free metre. Doing that here would
    /// make every swing's first metre free again, and flattening a hole a metre at a time would
    /// cost nothing, which is the free Raise ground this exists not to be.
    ///
    /// Where the work happens is the part that took the most thought.
    ///
    ///  - The swinging client decides how much of the fill it can pay for, because the stone is
    ///    in its pack and nowhere else. It measures the shortfall from its own copy of the zone,
    ///    which is the same data the owner holds, and sends a share between 0 and 1 behind the
    ///    reach in the package Reach.cs already appends.
    ///  - The client that owns the zone raises that share of what it measures itself. A share
    ///    rather than a volume so that a swing across a zone line raises both halves by the same
    ///    fraction: two zones each capped by a volume would stop at different heights along the
    ///    line they share, and that line is a row of vertices both of them draw.
    ///  - The owner then tells the swinger what it actually raised, and the swinger pays for that.
    ///    Paying after rather than before is what keeps a mixed server honest in the right
    ///    direction: an owner without this mod never reads the share, raises nothing and sends no
    ///    bill, so the swing costs nothing. Paying up front would have taken stone for a raise that
    ///    never happened.
    ///
    /// When the swinger owns the zone, which is singleplayer and most of the time on a server,
    /// every step of that is one synchronous call and the bill is exact. When somebody else owns
    /// it, the swinger's copy of the ground can be a swing behind, so a bill can come to more than
    /// the pack held. The difference is carried as a debt against the next fill rather than
    /// forgiven, so rapid swings cannot be used to raise ground for nothing.
    /// </summary>
    internal static class Fill
    {
        /// <summary>The method the zone owner calls on the swinger with what a fill used.</summary>
        internal const string BillRpc = "Jafna_FillBill";

        /// <summary>
        /// Shortfalls smaller than this are rounding and are ignored, in cubic metres. Ten litres.
        /// Without a floor a swing on ground that is already flat can find a few millimetres of
        /// float noise at one rim point and take a whole stone for it.
        /// </summary>
        private const float MinVolume = 0.01f;

        /// <summary>
        /// How long after asking for a fill a bill is still accepted, in seconds. A bill with no
        /// request behind it is either very late or not ours, and neither should take stone.
        /// </summary>
        private const float BillWindow = 10f;

        /// <summary>Whole items out of float arithmetic need a little slack in both directions.</summary>
        private const float Slack = 0.0001f;

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
        /// pricing off flat ground is the cheapest honest reading of what Raise ground charges.
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
        /// Held for the session and for one character, like the held height. It is never more
        /// than one of each item, and saving it would mean a value of ours in the player profile
        /// for the sake of less than a stone.
        /// </summary>
        private static readonly Dictionary<string, float> Credit = new Dictionary<string, float>();

        private static long _ledgerFor;

        private static void Ledger(Player player)
        {
            long id = player.GetPlayerID();
            if (id == _ledgerFor) return;

            _ledgerFor = id;
            Credit.Clear();
        }

        private static float CreditOf(string item)
        {
            return Credit.TryGetValue(item, out float credit) ? credit : 0f;
        }

        /// <summary>
        /// Cubic metres of fill the player can pay for right now, counting what is already paid.
        /// Infinity when nothing is limiting: a free entry, a NoBuildCost world, or the nocost
        /// cheat, which vanilla also lets past its requirement check.
        /// </summary>
        private static float Affordable(Player player, Price price, out string limiting)
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
                float have = CreditOf(price.Items[k]) + (pack == null ? 0 : pack.CountItems(price.Items[k]));
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

        /// <summary>One point the smooth will leave short, and by how many metres.</summary>
        private struct Lift
        {
            public int Index;
            public float Metres;
        }

        private static readonly List<Lift> Lifts = new List<Lift>();
        private static readonly List<Heightmap> Maps = new List<Heightmap>();

        /// <summary>
        /// Cubic metres a smooth centred on <paramref name="centre"/> wants to raise and cannot, on
        /// one heightmap. Records each short point when <paramref name="record"/> is given.
        ///
        /// Deliberately <c>SmoothTerrain</c>'s own arithmetic, point for point: the same footprint
        /// (always round, whatever <c>m_square</c> says), the same falloff with the same fast path
        /// for a power of three, and the same Lerp toward the same target. The shortfall at a point
        /// is what the smooth wants to move it minus the room its one metre clamp has left. If this
        /// walked a different set of points, or eased them differently, the fill would lift ground
        /// the swing never touches and be right most of the time.
        ///
        /// Two limits are respected on the way:
        ///
        ///  - Vanilla clamps the level delta to eight metres and the finished height to eight
        ///    metres either side of the generated ground. After a fill the smooth delta at a short
        ///    point sits at its one metre clamp, so the fill can bank at most eight minus one minus
        ///    what is already banked. Past that the game throws the height away, and charging for
        ///    it would be charging for nothing.
        ///  - A heightmap's last row and column are the same vertices as its neighbour's first, and
        ///    a swing that reaches them reaches the neighbour too. Both raise them, so the seam stays
        ///    closed, but only the neighbour counts them, so the line is not paid for twice.
        /// </summary>
        private static float Measure(
            Heightmap hmap, float[] level, float[] smooth, Vector3 centre, float radius, float power,
            List<Lift> record)
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

            float metres = 0f;

            for (int iy = cy - span; iy <= cy + span; iy++)
            {
                for (int ix = cx - span; ix <= cx + span; ix++)
                {
                    float d = Vector2.Distance(middle, new Vector2(ix, iy));
                    if (d > reach || ix < 0 || iy < 0 || ix >= pitch || iy >= pitch) continue;

                    float u = d / reach;
                    u = power != 3f ? Mathf.Pow(u, power) : u * u * u;

                    float height = hmap.GetHeight(ix, iy);
                    float wanted = Mathf.Lerp(height, target, 1f - u) - height;
                    if (wanted <= 0f) continue;

                    int n = iy * pitch + ix;
                    float smoothed = smooth != null && n < smooth.Length ? smooth[n] : 0f;
                    float levelled = level != null && n < level.Length ? level[n] : 0f;

                    float room = Mathf.Max(0f, smoothClamp - smoothed);
                    float excess = wanted - room;
                    if (excess <= 0f) continue;

                    float headroom = levelClamp - levelled - smoothClamp;
                    if (headroom <= 0f) continue;
                    if (excess > headroom) excess = headroom;

                    if (record != null) record.Add(new Lift { Index = n, Metres = excess });

                    if (ix < width && iy < width) metres += excess;
                }
            }

            return metres * scale * scale;
        }

        /// <summary>
        /// Cubic metres a flattening swing here would want raised and cannot raise itself, read
        /// from this client's copy of the ground. Used for the readout every frame and once per
        /// swing to decide how much of the fill the pack pays for.
        ///
        /// The target is worked out per heightmap exactly as the owner will work it out: the held
        /// height when there is one, otherwise whatever <see cref="Flat.Target"/> says for that
        /// zone. A zone with no compiler yet has never been shaped, so its deltas are all zero and
        /// its target is the crosshair, which is also what the owner will find.
        /// </summary>
        internal static float Estimate(TerrainOp.Settings settings, Vector3 point, float radius)
        {
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

                TerrainComp comp = TerrainComp.FindTerrainCompiler(hmap.transform.position);

                float target;
                if (Held.Active) target = Held.Height;
                else if (comp != null) target = Flat.Target(comp, probe, radius, square, out Flat.Source _);
                else target = probe.y;

                float[] level = comp != null ? _levelDelta(comp) : null;
                float[] smooth = comp != null ? _smoothDelta(comp) : null;

                need += Measure(
                    hmap, level, smooth, new Vector3(probe.x, target, probe.z), radius, settings.m_smoothPower, null);
            }

            return need;
        }

        // -- The swinging client ---------------------------------------------------------------

        private static TerrainOp _plannedOp;
        private static float _plannedShare = -1f;

        /// <summary>The price the last request was made at, so its bill is paid at the same rate.</summary>
        private static Price _billedAt;

        private static float _askedAt = float.NegativeInfinity;

        /// <summary>
        /// The share of this swing's shortfall the pack pays for, from 0 to 1, or -1 when the swing
        /// is not asking for a fill at all.
        ///
        /// Decided once per swing and then handed to every zone the swing reaches, because
        /// ApplyOperation is called once per zone and the share has to be the same in all of them
        /// (see the class comment). The op object is the key: TerrainOp.Awake hands the same one to
        /// every zone and then destroys it, so the next swing is always a different object.
        ///
        /// This is also where running short is said. Once per swing, at the moment of the swing,
        /// rather than when a bill comes back, so it lands on the click that caused it.
        /// </summary>
        internal static float Plan(TerrainOp op, Player player, float radius)
        {
            if (op == null) return -1f;
            if (op == _plannedOp) return _plannedShare;

            _plannedOp = op;
            _plannedShare = -1f;

            if (!JafnaConfig.AutoRaise.Value || player == null) return -1f;

            TerrainOp.Settings settings = op.m_settings;
            if (!Applies(settings) || !IsSwingOf(player, op)) return -1f;

            Vector3 point = op.transform.position;

            Price price = Resolve(player, ScaleAt(point));
            if (price == null) return -1f;

            float need = Estimate(settings, point, radius);
            if (need <= MinVolume) return -1f;

            float affordable = Affordable(player, price, out string limiting);
            float share = affordable >= need ? 1f : Mathf.Clamp01(affordable / need);

            if (share < 1f)
            {
                // Localised by MessageHud, so the item's own $ token comes out as its name.
                player.Message(MessageHud.MessageType.Center,
                    "Not enough " + (limiting ?? "$item_stone") + " to raise all of it");
            }

            if (JafnaConfig.Verbose.Value)
            {
                JafnaPlugin.Log.LogInfo(
                    "Fill wanted " + need.ToString("0.00") + " cubic metres, the pack covers "
                    + (float.IsPositiveInfinity(affordable) ? "all of it" : affordable.ToString("0.00"))
                    + ", asking for " + (share * 100f).ToString("0") + "%.");
            }

            if (share <= 0f) return -1f;

            _billedAt = price;
            _askedAt = Time.time;
            _plannedShare = share;
            return share;
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

            for (int k = 0; k < price.Items.Length; k++)
            {
                string item = price.Items[k];
                float credit = CreditOf(item) - used * price.PerCubicMetre[k];
                int taken = 0;

                if (credit < 0f && pack != null)
                {
                    int want = Mathf.CeilToInt(-credit - Slack);
                    taken = Mathf.Min(want, pack.CountItems(item));

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

        /// <summary>
        /// Raises the given share of what this smooth cannot reach on this zone, and sends the bill.
        /// Runs on the client that owns the zone, from the DoOperation prefix, before vanilla's
        /// SmoothTerrain.
        ///
        /// Before, because the smooth then does its own part on top: this banks the shortfall into
        /// the level delta, the smooth adds whatever room its clamp has left, and together they put
        /// each point where the smooth alone wanted it. Both read the heights as they stood before
        /// the swing, since the heightmap is not rebuilt until the frame's LateUpdate, which is
        /// exactly why the two amounts add up rather than overlap.
        ///
        /// That same delay is the one trap here. Two ops landing on one heightmap in one frame -
        /// which happens when somebody else's swings arrive together over the network - would have
        /// the second measure ground the first already raised and raise it again. A pending rebuild
        /// is therefore done first. It is the rebuild LateUpdate was about to do anyway, only sooner.
        /// </summary>
        internal static void Apply(TerrainComp comp, Vector3 centre, TerrainOp.Settings modifier, float share, long sender)
        {
            if (share <= 0f) return;
            if (!JafnaConfig.AutoRaise.Value || !Applies(modifier)) return;
            if (comp == null || !Bind()) return;

            Heightmap hmap = Heightmap.FindHeightmap(comp.transform.position);
            if (hmap == null) return;

            bool[] modified = _modifiedHeight(comp);
            float[] level = _levelDelta(comp);
            float[] smooth = _smoothDelta(comp);
            if (modified == null || level == null || smooth == null) return;

            if (hmap.m_doLateUpdate != 0) hmap.Regenerate();

            Lifts.Clear();
            float need = Measure(hmap, level, smooth, centre, modifier.m_smoothRadius, modifier.m_smoothPower, Lifts);
            if (Lifts.Count == 0) return;

            float f = Mathf.Min(1f, share);
            float levelClamp = Heightmap.c_LevelMaxDelta;

            for (int i = 0; i < Lifts.Count; i++)
            {
                int n = Lifts[i].Index;
                level[n] = Mathf.Clamp(level[n] + Lifts[i].Metres * f, -levelClamp, levelClamp);
                modified[n] = true;
            }

            float used = need * f;

            if (JafnaConfig.Verbose.Value)
            {
                JafnaPlugin.Log.LogInfo(
                    "Filled " + used.ToString("0.00") + " of " + need.ToString("0.00") + " cubic metres over "
                    + Lifts.Count + " points at " + centre.y.ToString("0.00") + "m, billing peer " + sender + ".");
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
            Short
        }

        /// <summary>
        /// What a swing here would cost, for the build panel. <paramref name="cost"/> is what the
        /// whole fill takes out of the pack in whole items, after what is already paid;
        /// <paramref name="carried"/> is what the pack holds, filled only when it is short.
        ///
        /// Whole items, because that is what a player watches leave the pack. A fraction on screen
        /// would disagree with the pack on every swing and be right only on average.
        /// </summary>
        internal static Terms Quote(
            Player player, TerrainOp.Settings settings, Vector3 point, float radius, out string cost, out string carried)
        {
            cost = null;
            carried = null;

            if (!JafnaConfig.AutoRaise.Value || player == null || !Applies(settings)) return Terms.None;

            Price price = Resolve(player, ScaleAt(point));
            if (price == null) return Terms.None;

            float need = Estimate(settings, point, radius);
            if (need <= MinVolume) return Terms.None;

            if (price.IsFree()) return Terms.Free;

            Ledger(player);
            Inventory pack = player.GetInventory();

            bool anything = false;
            cost = "";
            carried = "";

            for (int k = 0; k < price.Items.Length; k++)
            {
                string item = price.Items[k];
                int whole = Mathf.Max(0, Mathf.CeilToInt(need * price.PerCubicMetre[k] - CreditOf(item) - Slack));
                if (whole > 0) anything = true;

                cost += (k > 0 ? ", " : "") + whole + " " + item;
                carried += (k > 0 ? ", " : "") + (pack == null ? 0 : pack.CountItems(item)) + " " + item;
            }

            // The cheat lifts the limit and still takes what is carried, so it is never short but
            // is still quoted.
            float affordable = Affordable(player, price, out string _);
            if (affordable < need) return Terms.Short;

            return anything ? Terms.Paid : Terms.Covered;
        }
    }
}

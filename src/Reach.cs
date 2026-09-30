using System;
using System.Reflection;
using Ezomic.Shared;
using HarmonyLib;
using UnityEngine;

namespace Jafna
{
    /// <summary>
    /// How wide a level swing reaches, how that number gets to the machine that applies it,
    /// and the ward rule that stops the extra width being a way over somebody's fence.
    ///
    /// The reach itself is Skaft's rule applied to a second tool: Crafting buys reach and
    /// never buys a discount. The arithmetic lives in <c>core\shared\CraftingReach.cs</c> so
    /// that "8 metres at 60" cannot come to mean two different things in two mods.
    ///
    /// Getting it across a network is the interesting part, and vanilla gives you almost
    /// nothing to work with. A terrain op is applied by whichever client owns that zone's
    /// TerrainComp, which is frequently not the player who swung, and
    /// <c>TerrainOp.Settings.Serialize</c> writes exactly one thing: the prefab's name hash.
    /// The far end then answers it out of its OWN ObjectDB and uses its OWN copy of the
    /// settings. So a radius simply does not travel, and mutating the prefab's settings object
    /// locally would not help - it is the shared asset every op of that type reads, on a
    /// machine that may not be the one deciding anything.
    ///
    /// What does work is that nothing ever reads to the end of that package.
    /// <c>RPC_ApplyOperation</c> reads a position, a flag, sometimes a forward vector and one
    /// int, and stops. Bytes after that are invisible to it. So the radius is appended behind
    /// a magic number: an owner running Jafna finds it and uses it, an owner not running Jafna
    /// never looks and applies a perfectly ordinary vanilla op. The failure mode of the thing
    /// that is missing is the behaviour of the game without it, which is the only acceptable
    /// shape for a change to a protocol that is not yours.
    ///
    /// The package is also the only thing that tells the owner an op is a Jafna swing at all.
    /// DoOperation is handed a position and a settings object, and a location shaping its own
    /// ground, another mod's op, Devkit's flatten and a swing from a player without Jafna all
    /// look exactly like a hoe swing there. So the swinger appends the package to its own swing
    /// every time, at the hoe's own width with nothing held included, and to nothing else (see
    /// <see cref="IsSwing"/>), and the owner reshapes only what arrives carrying it. Until
    /// 2026-09-29 the swinger sent it only when it had a wider radius, a held height or a fill to
    /// say, and the owner continued the flat under every flattening op whatever sent it. A
    /// scenario log caught Devkit's flatten levelled 0.2 m below its own target, to a flat it
    /// happened to touch.
    /// </summary>
    internal static class Reach
    {
        /// <summary>
        /// "JAFN". Not a version number and not a length - just a witness that the four bytes
        /// after it were written by this mod. It is checked at a known offset rather than
        /// searched for, so a vanilla package cannot collide with it by accident.
        /// </summary>
        internal const int Magic = 0x4A41464E;

        private static AccessTools.FieldRef<TerrainComp, ZNetView> _nview;
        private static bool _bound;
        private static bool _bindFailed;

        private static FieldInfo[] _settingsFields;

        /// <summary>
        /// Set by the RPC prefix for the op it is about to let through, read and cleared by the
        /// DoOperation prefix. A static is safe here only because both run inside one
        /// synchronous call on the main thread with nothing between them - RPC_ApplyOperation
        /// calls DoOperation directly. Anything less immediate would need the ZDO to carry it.
        /// </summary>
        private static float _incoming = -1f;
        private static bool _incomingHeld;
        private static float _incomingHeight;
        private static float _incomingFill = -1f;
        private static long _incomingSender;

        private static bool Bind()
        {
            if (_bound) return !_bindFailed;
            _bound = true;

            try
            {
                _nview = AccessTools.FieldRefAccess<TerrainComp, ZNetView>("m_nview");
            }
            catch (Exception e)
            {
                _bindFailed = true;
                // Every swing's package is sent through this view, and the package is how a zone
                // owner knows an op is a swing at all, so without it nothing is reshaped anywhere,
                // this machine's own zones included.
                JafnaPlugin.Log.LogWarning(
                    "Could not reach TerrainComp.m_nview, so no swing can carry Jafna's note to the "
                    + "zone that applies it, and levelling is vanilla this session, singleplayer "
                    + "included. " + e.Message);
            }

            return !_bindFailed;
        }

        internal static ZNetView View(TerrainComp comp)
        {
            return Bind() ? _nview(comp) : null;
        }

        /// <summary>
        /// Whether this op is one that flattens ground, and so one this mod has any business
        /// touching.
        ///
        /// In Valheim 1.0 the hoe's Level ground entry is <c>m_smooth</c>, not <c>m_level</c> -
        /// read off the running game on 2026-09-20, because a piece table is asset data that no
        /// decompiler or prefab rip can reach. <c>m_level</c> is used by neither of the hoe's
        /// flattening entries. Both are accepted here anyway: <c>m_level</c> exists, other tools
        /// and other mods use it, and the two differ only in how hard they pull.
        ///
        /// <c>m_raise</c> is deliberately not on this list. It builds ground up rather than
        /// flattening it, which is a different job with a different answer to "what height
        /// should this be", and widening it was not what was asked for.
        /// </summary>
        internal static bool IsFlatten(TerrainOp.Settings settings)
        {
            return settings != null && (settings.m_smooth || settings.m_level);
        }

        /// <summary>
        /// Whether this op is the local player swinging the tool in their hands, which is the only
        /// op Jafna ever reshapes where it starts.
        ///
        /// Any TerrainOp instantiated on this machine goes through GetRadius and ApplyOperation: a
        /// pickaxe's dig, a location's own shaping, another mod's op, Devkit's flatten. None of them
        /// should be widened, pulled to a held height, carry a fill or take stone out of a pack.
        /// The selected build piece being the op's own prefab is the test that only a swing passes.
        /// It began as the fill's test alone, and the rest of the mod took it on 2026-09-29 (see
        /// the class comment). A mod that instantiates the hoe's own entry while the player has it
        /// selected would pass it too. Nothing here can tell that op from the click, so that is
        /// accepted.
        ///
        /// By name, because an instantiated op is called after its prefab with "(Clone)" on the end,
        /// and GetPrefabName takes that off. Player.PlacePiece instantiates the op while the player
        /// is still in place mode with the entry selected, so the test holds for the whole of
        /// TerrainOp.Awake, which is where GetRadius and ApplyOperation are both called.
        /// </summary>
        internal static bool IsSwing(TerrainOp op)
        {
            Player player = Player.m_localPlayer;
            if (player == null || op == null) return false;
            if (!player.InPlaceMode()) return false;

            PieceTable table = player.GetBuildTool();
            if (table == null) return false;

            GameObject selected = table.GetSelectedPrefab();
            if (selected == null) return false;

            return Utils.GetPrefabName(op.gameObject.name) == selected.name;
        }

        /// <summary>
        /// The radius the tool itself uses for whichever flattening operation it performs.
        /// Smooth first, because that is the one the hoe actually carries.
        /// </summary>
        internal static float VanillaRadius(TerrainOp.Settings settings)
        {
            if (settings == null) return 0f;

            return settings.m_smooth ? settings.m_smoothRadius : settings.m_levelRadius;
        }

        /// <summary>
        /// Whether the op's footprint is really a square, which is not the same question as
        /// what <c>m_square</c> says.
        ///
        /// <c>LevelTerrain</c> honours the flag. <c>SmoothTerrain</c> does not: it runs
        /// <c>Vector2.Distance</c> against the radius on every point and never looks at
        /// <c>m_square</c> at all, so a smooth op is round however the prefab is authored.
        /// mud_road_v2 has the flag false anyway, but relying on that would be relying on an
        /// asset rather than on the code, and the code is the thing that cannot change under
        /// us without a game update we would rebuild for.
        ///
        /// It matters twice. The footprint this mod walks to find flat ground has to be the
        /// same set of points the op will write to, or it reads heights from ground the swing
        /// never touches and is right most of the time. And the ward test pads a square by root
        /// two, which on a circle refuses swings that could not reach the ward at all.
        /// </summary>
        internal static bool IsSquare(TerrainOp.Settings settings)
        {
            return settings != null && !settings.m_smooth && settings.m_square;
        }

        /// <summary>
        /// The radius a flattening op should use for the player at this keyboard, in metres.
        ///
        /// Always at least the tool's own radius. The vanilla number is asset data and cannot
        /// be read off disk by any decompiler, so the mod deliberately never states it and
        /// never subtracts from it - it takes whichever is larger. That also means MinRadius
        /// can sit at 0 and mean "leave a new character exactly the hoe the game shipped"
        /// without anybody having to know what that is.
        /// </summary>
        internal static float Earned(TerrainOp.Settings settings, Player player)
        {
            float vanilla = VanillaRadius(settings);

            if (!JafnaConfig.Enabled.Value || player == null || !IsFlatten(settings)) return vanilla;

            float earned = CraftingReach.Radius(
                CraftingReach.Level(player),
                JafnaConfig.MinRadius.Value,
                JafnaConfig.MaxRadius.Value,
                JafnaConfig.FullLevel.Value,
                JafnaConfig.Curve.Value);

            return Mathf.Max(vanilla, earned);
        }

        /// <summary>
        /// A copy of a settings object with one radius changed.
        ///
        /// A copy and never the original, because the original is the prefab asset out of
        /// ObjectDB, shared by every op of that type on this machine and by whatever the next
        /// op happens to be. This is the same rule as never writing to a shared material, and
        /// it fails the same way: quietly, later, somewhere else.
        ///
        /// Copied by reflection over the public fields rather than field by field. A game
        /// update that adds a setting then carries it across on its own; a hand-written copy
        /// would drop it and the op would come out subtly wrong with nothing logged.
        /// </summary>
        internal static TerrainOp.Settings With(TerrainOp.Settings source, float radius)
        {
            if (source == null) return null;

            if (_settingsFields == null)
            {
                _settingsFields = typeof(TerrainOp.Settings)
                    .GetFields(BindingFlags.Public | BindingFlags.Instance);
            }

            TerrainOp.Settings copy = new TerrainOp.Settings();

            for (int i = 0; i < _settingsFields.Length; i++)
            {
                _settingsFields[i].SetValue(copy, _settingsFields[i].GetValue(source));
            }

            float before = VanillaRadius(source);

            if (source.m_smooth) copy.m_smoothRadius = radius;
            else copy.m_levelRadius = radius;

            // The paint circle is dragged along by the same factor rather than left where it
            // was. The hoe's Level ground paints dirt over what it flattens, and a mod that
            // widened the flattening alone would leave a wide patch of smoothed ground with a
            // small circle of dirt in the middle of it - which reads as the texture failing to
            // keep up rather than as a deliberate reach, and would have been blamed on the
            // paint mask. Scaled by ratio because the vanilla radii are asset data: their
            // relationship is known here, their values are not.
            if (source.m_paintCleared && before > 0.01f && source.m_paintRadius > 0f)
            {
                copy.m_paintRadius = source.m_paintRadius * (radius / before);
            }

            return copy;
        }

        // -- The wire ------------------------------------------------------------------

        /// <summary>
        /// Writes the mod's four fields and then the fill's reach, how far from the middle the
        /// swinger's stone pays to raise the ground (see Fill.Plan). It goes last and is read only
        /// when it is there, because the first four are what Jafna 1.0.0 wrote and still reads: an
        /// owner on that version finds its magic, reads its thirteen bytes, never looks further,
        /// and applies the swing without raising anything or sending a bill. The swing then costs
        /// nothing, which is the right way to fail for a price nobody collected.
        /// </summary>
        internal static void Append(ZPackage pkg, float radius, bool held, float height, float fill)
        {
            pkg.Write(Magic);
            pkg.Write(radius);
            pkg.Write(held);
            pkg.Write(height);
            pkg.Write(fill);
        }

        /// <summary>
        /// Reads the appended radius without disturbing the reader, by walking exactly the
        /// fields vanilla walks and then putting the position back. Reading the last eight
        /// bytes of the array instead would have been shorter and would guess: a vanilla
        /// package's final int is a prefab hash, and the four bytes in front of it are a float
        /// that could in principle equal the magic.
        /// </summary>
        internal static bool Peek(ZPackage pkg, out float radius, out bool held, out float height, out float fill)
        {
            radius = -1f;
            held = false;
            height = 0f;
            fill = -1f;

            if (pkg == null) return false;

            int start = pkg.GetPos();

            try
            {
                pkg.ReadVector3();
                if (pkg.ReadBool()) pkg.ReadVector3();
                pkg.ReadInt();

                // Magic, radius, the held flag and the held height: 4 + 4 + 1 + 4.
                if (pkg.GetPos() + 13 > pkg.Size()) return false;
                if (pkg.ReadInt() != Magic) return false;

                radius = pkg.ReadSingle();
                held = pkg.ReadBool();
                height = pkg.ReadSingle();

                // The fill's reach, when the swinger's version writes one. A package from 1.0.0
                // ends here, and no reach means no fill was asked for.
                if (pkg.GetPos() + 4 <= pkg.Size()) fill = pkg.ReadSingle();

                return radius > 0f;
            }
            catch
            {
                // A short or malformed package is not this mod's business to report. Vanilla is
                // about to read the same bytes and will say so in its own words.
                return false;
            }
            finally
            {
                pkg.SetPos(start);
            }
        }

        /// <summary>
        /// <paramref name="sender"/> is the peer that swung, which is where a fill's bill goes. It
        /// rides along with the rest because RPC_ApplyOperation is the only place that knows it:
        /// DoOperation is handed a position and a settings object and nothing about who asked.
        /// </summary>
        internal static void SetIncoming(float radius, bool held, float height, float fill, long sender)
        {
            _incoming = radius;
            _incomingHeld = held;
            _incomingHeight = height;
            _incomingFill = fill;
            _incomingSender = sender;
        }

        internal static float TakeIncoming(out bool held, out float height, out float fill, out long sender)
        {
            float r = _incoming;
            held = _incomingHeld;
            height = _incomingHeight;
            fill = _incomingFill;
            sender = _incomingSender;

            _incoming = -1f;
            _incomingHeld = false;
            _incomingHeight = 0f;
            _incomingFill = -1f;
            _incomingSender = 0L;

            return r;
        }

        // -- Wards ---------------------------------------------------------------------

        /// <summary>
        /// Whether a level op of this radius centred here clears every ward the player cannot
        /// use.
        ///
        /// <c>PrivateArea.IsInside</c> is <c>DistanceXZ(ward, point) &lt; m_radius + radius</c>,
        /// so handing <c>CheckAccess</c> a radius turns its point test into a circle-against-
        /// circle one for free. Vanilla passes 0 from <c>Player.UpdatePlacementGhost</c> for
        /// anything that is not itself a ward, which is why the stock hoe can already cut into
        /// a neighbour's ground from just outside their fence.
        ///
        /// A square op reaches root two further at its corners than its radius suggests, so it
        /// is tested with the circumscribed radius. The hoe's own op is round - m_square is
        /// false on mud_road_v2, read off the running game - and padding a circle by root two
        /// would refuse swings that cannot touch the ward at all, which is a worse failure than
        /// the hole being closed slightly loosely.
        ///
        /// Flashing is off. The vanilla call flashes the ward to tell you why you are being
        /// refused, but this runs from a placement ghost update, which is every frame, and a
        /// ward strobing at sixty hertz reads as a bug rather than an explanation. The readout
        /// says it in words instead.
        /// </summary>
        internal static bool FootprintClear(Vector3 point, float radius, bool square)
        {
            if (!JafnaConfig.RespectWardFootprint.Value) return true;

            return PrivateArea.CheckAccess(point, square ? radius * 1.4142136f : radius, false, false);
        }
    }
}

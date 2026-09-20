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
                JafnaPlugin.Log.LogWarning(
                    "Could not reach TerrainComp.m_nview, so the reach will not travel to other "
                    + "players this session and levelling stays vanilla width online. "
                    + "Singleplayer is unaffected. " + e.Message);
            }

            return !_bindFailed;
        }

        internal static ZNetView View(TerrainComp comp)
        {
            return Bind() ? _nview(comp) : null;
        }

        /// <summary>
        /// The radius a level op should use for the player at this keyboard, in metres.
        ///
        /// Always at least the tool's own radius. The vanilla number is asset data and cannot
        /// be read off disk by any decompiler, so the mod deliberately never states it and
        /// never subtracts from it - it takes whichever is larger. That also means MinRadius
        /// can sit at 0 and mean "leave a new character exactly the hoe the game shipped"
        /// without anybody having to know what that is.
        /// </summary>
        internal static float Earned(TerrainOp.Settings settings, Player player)
        {
            float vanilla = settings == null ? 0f : settings.m_levelRadius;

            if (!JafnaConfig.Enabled.Value || player == null) return vanilla;

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
        internal static TerrainOp.Settings With(TerrainOp.Settings source, float levelRadius)
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

            copy.m_levelRadius = levelRadius;
            return copy;
        }

        // -- The wire ------------------------------------------------------------------

        internal static void Append(ZPackage pkg, float radius)
        {
            pkg.Write(Magic);
            pkg.Write(radius);
        }

        /// <summary>
        /// Reads the appended radius without disturbing the reader, by walking exactly the
        /// fields vanilla walks and then putting the position back. Reading the last eight
        /// bytes of the array instead would have been shorter and would guess: a vanilla
        /// package's final int is a prefab hash, and the four bytes in front of it are a float
        /// that could in principle equal the magic.
        /// </summary>
        internal static bool Peek(ZPackage pkg, out float radius)
        {
            radius = -1f;
            if (pkg == null) return false;

            int start = pkg.GetPos();

            try
            {
                pkg.ReadVector3();
                if (pkg.ReadBool()) pkg.ReadVector3();
                pkg.ReadInt();

                if (pkg.GetPos() + 8 > pkg.Size()) return false;
                if (pkg.ReadInt() != Magic) return false;

                radius = pkg.ReadSingle();
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

        internal static void SetIncoming(float radius)
        {
            _incoming = radius;
        }

        internal static float TakeIncoming()
        {
            float r = _incoming;
            _incoming = -1f;
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
        /// The op levels a square, so the corner is radius * root two from the middle and that
        /// is the number to test with. Testing the inscribed radius would leave four triangles
        /// of ground reachable and would be harder to explain than either extreme.
        ///
        /// Flashing is off. The vanilla call flashes the ward to tell you why you are being
        /// refused, but this runs from a placement ghost update, which is every frame, and a
        /// ward strobing at sixty hertz reads as a bug rather than an explanation. The readout
        /// says it in words instead.
        /// </summary>
        internal static bool FootprintClear(Vector3 point, float radius)
        {
            if (!JafnaConfig.RespectWardFootprint.Value) return true;

            return PrivateArea.CheckAccess(point, radius * 1.4142136f, false, false);
        }
    }
}

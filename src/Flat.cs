using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Jafna
{
    /// <summary>
    /// Deciding what height a level swing should actually use.
    ///
    /// The whole mod is one observation about <c>TerrainComp.LevelTerrain</c>: it sets every
    /// grid point under the tool to <c>worldPos.y</c>, and <c>worldPos</c> is the placement
    /// ghost, which sits wherever your crosshair last touched the ground. So the reference
    /// height follows the camera. Two swings taken a step apart level their shared overlap
    /// to two different heights, and the difference is permanent. No amount of care fixes
    /// that, because the thing moving is not the player's aim, it is the target.
    ///
    /// The fix needs somewhere to keep "the height this area is supposed to be", and the game
    /// is already keeping it. <c>TerrainComp</c> carries a <c>m_modifiedHeight</c> flag per
    /// grid point, saved into the zone's ZDO and synced like everything else, meaning "a
    /// terrain op has touched this point". So a swing can look at the ground it is about to
    /// cover, find the parts of it that were levelled before, and use their height instead of
    /// the crosshair's. Flat then grows outward from wherever it started, at one height,
    /// across sessions and across players, with nothing new saved anywhere and nothing new
    /// for a player to place, carry or press.
    ///
    /// A swing that touches no levelled ground falls through to vanilla untouched, which is
    /// what makes starting a new platform at a new height still work.
    /// </summary>
    internal static class Flat
    {
        /// <summary>
        /// Why a swing used the height it used. The readout says this out loud, because a mod
        /// that silently overrules your crosshair is indistinguishable from a hoe that has
        /// started misbehaving - which is the complaint this exists to answer, not to join.
        /// </summary>
        internal enum Source
        {
            Crosshair,
            ContinuedFlat,
            Disagreed,
            TooLittle
        }

        // TerrainComp keeps all three of these private. They are bound lazily and inside a
        // try/catch rather than in a static initialiser, and that is not defensive habit: a
        // FieldRefAccess bound in a static field throws at type-init when a name is wrong, and
        // from then on EVERY Harmony patch this assembly carries throws
        // TypeInitializationException instead of running. That presents as unrelated vanilla
        // features breaking - "I cannot equip a tool any more" - and sends you a very long way
        // from the one misspelled string that caused it. Bound this way, a bad name costs the
        // feature and nothing else.
        private static AccessTools.FieldRef<TerrainComp, bool[]> _modifiedHeight;
        private static AccessTools.FieldRef<TerrainComp, float[]> _levelDelta;
        private static AccessTools.FieldRef<TerrainComp, float[]> _smoothDelta;

        private static bool _bound;
        private static bool _bindFailed;

        /// <summary>Scratch list, reused. One swing per click, but it is still a loop over the footprint.</summary>
        private static readonly List<float> Heights = new List<float>();

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
                    "Could not reach TerrainComp's height arrays, so continuing a flat is off for "
                    + "this session and levelling is vanilla. The reach and ward parts are "
                    + "unaffected. " + e.Message);
            }

            return !_bindFailed;
        }

        /// <summary>
        /// The height a level op centred on <paramref name="worldPos"/> should use, and why.
        ///
        /// Returns the world Y to level to. When the answer is <see cref="Source.Crosshair"/>
        /// that is simply <c>worldPos.y</c> back again and the caller should change nothing.
        ///
        /// <paramref name="comp"/> is the terrain compiler for the zone, <paramref name="radius"/>
        /// the level radius in metres and <paramref name="square"/> the op's own square flag, so
        /// the footprint walked here is the same set of points <c>LevelTerrain</c> will write to.
        /// Walking a different set would be the worst kind of wrong: it would read heights from
        /// ground the swing never touches and be right most of the time.
        /// </summary>
        internal static float Target(TerrainComp comp, Vector3 worldPos, float radius, bool square, out Source source)
        {
            source = Source.Crosshair;

            if (!JafnaConfig.ContinueFlat.Value) return worldPos.y;
            if (comp == null || !Bind()) return worldPos.y;

            Heightmap hmap = Heightmap.FindHeightmap(comp.transform.position);
            if (hmap == null) return worldPos.y;

            bool[] modified = _modifiedHeight(comp);
            float[] levelDelta = _levelDelta(comp);
            float[] smoothDelta = _smoothDelta(comp);

            if (modified == null || levelDelta == null || smoothDelta == null) return worldPos.y;

            hmap.WorldToVertex(worldPos, out int cx, out int cy);

            // Deliberately the same arithmetic as LevelTerrain rather than something tidier.
            // m_scale is 1 in every world the game ships, but reading it costs nothing and
            // hardcoding 1 is the kind of assumption that survives until it does not.
            float reach = radius / hmap.m_scale;
            int span = Mathf.CeilToInt(reach);
            int pitch = hmap.m_width + 1;

            Vector2 centre = new Vector2(cx, cy);

            Heights.Clear();

            for (int iy = cy - span; iy <= cy + span; iy++)
            {
                for (int ix = cx - span; ix <= cx + span; ix++)
                {
                    if (!square && Vector2.Distance(centre, new Vector2(ix, iy)) > reach) continue;
                    if (ix < 0 || iy < 0 || ix >= pitch || iy >= pitch) continue;

                    int n = iy * pitch + ix;
                    if (n < 0 || n >= modified.Length) continue;
                    if (!modified[n]) continue;

                    // m_modifiedHeight is set by levelling, by raising AND by smoothing, so the
                    // flag alone is not "this ground was made flat". A smoothed point sits on a
                    // deliberate slope, and adopting its height would quietly drag a platform
                    // toward the curve somebody rounded off at its edge. Take only points that
                    // a level or raise op moved and a smooth op did not.
                    if (Mathf.Abs(smoothDelta[n]) > 0.0001f) continue;
                    if (Mathf.Abs(levelDelta[n]) < 0.0001f) continue;

                    Heights.Add(hmap.transform.position.y + hmap.GetHeight(ix, iy));
                }
            }

            if (Heights.Count < Mathf.Max(1, JafnaConfig.ContinueMinVertices.Value))
            {
                source = Heights.Count > 0 ? Source.TooLittle : Source.Crosshair;
                return worldPos.y;
            }

            float lowest = float.MaxValue;
            float highest = float.MinValue;
            float total = 0f;

            for (int i = 0; i < Heights.Count; i++)
            {
                float h = Heights[i];
                if (h < lowest) lowest = h;
                if (h > highest) highest = h;
                total += h;
            }

            // Two platforms at different heights meeting under one swing. Averaging them would
            // put a ramp through the middle of both and there would be no way to tell it had
            // happened until the wall would not sit flush. Hand it back to the crosshair, which
            // is at least a height the player can see and move.
            if (highest - lowest > Mathf.Max(0f, JafnaConfig.ContinueTolerance.Value))
            {
                source = Source.Disagreed;
                return worldPos.y;
            }

            source = Source.ContinuedFlat;
            return total / Heights.Count;
        }
    }
}

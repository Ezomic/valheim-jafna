using System;
using HarmonyLib;
using UnityEngine;

namespace Jafna
{
    /// <summary>
    /// The mod's Harmony patches, in the order a single swing travels through them.
    ///
    ///   1. TerrainOp.GetRadius   - on the swinging client, so the op knows how many zones to
    ///                              reach into before it asks any of them to do anything.
    ///   2. TerrainComp.ApplyOperation
    ///                            - still on the swinging client. Sends the radius.
    ///   3. TerrainComp.RPC_ApplyOperation
    ///                            - on the client that owns the zone, which is often somebody
    ///                              else. Receives the radius.
    ///   4. TerrainComp.DoOperation
    ///                            - same machine. Applies the radius and decides the height.
    ///   5. Player.UpdatePlacementGhost
    ///                            - every frame, on the swinging client. The ward footprint.
    ///
    /// Three and four are separate patches rather than one because the radius has to be read
    /// off the package before vanilla reads it and used after vanilla has parsed it, and the
    /// two are different methods. Everything between them is one synchronous call.
    /// </summary>
    internal static class JafnaPatches
    {
        private static AccessTools.FieldRef<Player, GameObject> _placementGhost;
        private static AccessTools.FieldRef<Player, Player.PlacementStatus> _placementStatus;
        private static AccessTools.FieldRef<Player, PieceTable> _buildPieces;

        private static bool _bound;
        private static bool _bindFailed;
        private static bool _skillReported;

        /// <summary>
        /// Bound lazily and inside a try/catch for the reason spelled out in Flat.cs: a
        /// FieldRefAccess that throws at type-init poisons every patch in the class, and it
        /// surfaces as unrelated vanilla features breaking rather than as this mod failing.
        /// </summary>
        private static bool Bind()
        {
            if (_bound) return !_bindFailed;
            _bound = true;

            try
            {
                _placementGhost = AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");
                _placementStatus = AccessTools.FieldRefAccess<Player, Player.PlacementStatus>("m_placementStatus");
                _buildPieces = AccessTools.FieldRefAccess<Player, PieceTable>("m_buildPieces");
            }
            catch (Exception e)
            {
                _bindFailed = true;
                JafnaPlugin.Log.LogWarning(
                    "Could not reach the placement ghost fields, so the ward footprint check and "
                    + "the readout are off for this session. Levelling itself is unaffected. "
                    + e.Message);
            }

            return !_bindFailed;
        }

        /// <summary>The level op on the currently selected build piece, or null if it is not one.</summary>
        private static TerrainOp.Settings SelectedLevelOp(Player player)
        {
            if (!Bind()) return null;

            PieceTable table = _buildPieces(player);
            if (table == null) return null;

            GameObject prefab = table.GetSelectedPrefab();
            if (prefab == null) return null;

            ReportSelection(prefab);

            if (!prefab.TryGetComponent(out TerrainOp op)) return null;

            return Reach.IsFlatten(op.m_settings) ? op.m_settings : null;
        }

        /// <summary>Build-menu prefabs already described in the log, so each is reported once.</summary>
        private static readonly System.Collections.Generic.HashSet<string> _describedPrefabs =
            new System.Collections.Generic.HashSet<string>();

        /// <summary>
        /// Says what the selected build-menu prefab actually is, once per prefab, behind Verbose.
        ///
        /// This exists because the game carries TWO terrain systems and nothing on disk says
        /// which one a given piece uses. TerrainOp is the one that goes through
        /// TerrainComp.ApplyOperation and an RPC; TerrainModifier is an older arrangement that
        /// persists as its own ZDO and is read back when a Heightmap regenerates, optionally
        /// routing into the same compiler via m_useTerrainCompiler. Player.UpdatePlacementGhost
        /// checks for both, so both are live in 1.0, and a piece table's contents are asset data
        /// that no decompiler reaches and that a Devkit rip resolves only through ZNetScene and
        /// ObjectDB - where a hoe piece may well not be registered at all.
        ///
        /// The route that works is the one Skaft ended up on for the hammer: make the mod log
        /// what it is actually holding. A mod that patches the wrong one of two systems does not
        /// fail, it does nothing, and the log stays clean while you check the config.
        /// </summary>
        private static void ReportSelection(GameObject prefab)
        {
            if (!JafnaConfig.Verbose.Value) return;
            if (!_describedPrefabs.Add(prefab.name)) return;

            string line = "Build menu selection '" + prefab.name + "': ";

            if (prefab.TryGetComponent(out TerrainOp op))
            {
                TerrainOp.Settings s = op.m_settings;
                line += s == null
                    ? "TerrainOp with no settings. "
                    : "TerrainOp level=" + s.m_level
                      + " radius=" + s.m_levelRadius.ToString("0.00")
                      + " square=" + s.m_square
                      + " offset=" + s.m_levelOffset.ToString("0.00")
                      + " raise=" + s.m_raise
                      + " smooth=" + s.m_smooth
                      + " smoothRadius=" + s.m_smoothRadius.ToString("0.00")
                      + " smoothPower=" + s.m_smoothPower.ToString("0.00")
                      + " paint=" + s.m_paintCleared
                      + " paintRadius=" + s.m_paintRadius.ToString("0.00") + ". ";
            }
            else
            {
                line += "no TerrainOp. ";
            }

            if (prefab.TryGetComponent(out TerrainModifier mod))
            {
                line += "TerrainModifier level=" + mod.m_level
                        + " radius=" + mod.m_levelRadius.ToString("0.00")
                        + " square=" + mod.m_square
                        + " offset=" + mod.m_levelOffset.ToString("0.00")
                        + " useCompiler=" + mod.m_useTerrainCompiler
                        + " smooth=" + mod.m_smooth + ".";
            }
            else
            {
                line += "no TerrainModifier.";
            }

            JafnaPlugin.Log.LogInfo(line);
        }

        // -- 1. How far the op reaches ---------------------------------------------------

        /// <summary>
        /// TerrainOp.Awake asks GetRadius how far the op reaches and collects every Heightmap
        /// inside that circle, then hands the op to each of them. Widening the radius in
        /// DoOperation alone would therefore produce a swing that is wide in the zone under
        /// your feet and stops dead at the zone border - a straight edge through the middle of
        /// a platform, appearing only sometimes, which is a miserable thing to chase.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(TerrainOp), nameof(TerrainOp.GetRadius))]
        private static void WidenSearch(TerrainOp __instance, ref float __result)
        {
            if (!JafnaConfig.Enabled.Value) return;
            if (!Reach.IsFlatten(__instance.m_settings)) return;

            __result = Mathf.Max(__result, Reach.Earned(__instance.m_settings, Player.m_localPlayer));
        }

        // -- 2. Sending the radius --------------------------------------------------------

        /// <summary>
        /// Replaces ApplyOperation with the same six lines plus two. It is a replacement rather
        /// than a postfix because the package is built and sent inside one method, so there is
        /// no moment between the two to hook.
        ///
        /// Skipped entirely when there is nothing to add, so an ordinary op on an ordinary
        /// swing goes through vanilla's own code and this mod is not in the path at all.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TerrainComp), nameof(TerrainComp.ApplyOperation))]
        private static bool SendReach(TerrainComp __instance, TerrainOp modifier)
        {
            if (!JafnaConfig.Enabled.Value) return true;
            if (modifier == null || !Reach.IsFlatten(modifier.m_settings)) return true;

            float radius = Reach.Earned(modifier.m_settings, Player.m_localPlayer);
            if (radius <= Reach.VanillaRadius(modifier.m_settings)) return true;

            ZNetView nview = Reach.View(__instance);
            if (nview == null) return true;

            ZPackage pkg = new ZPackage();
            pkg.Write(modifier.transform.position);
            pkg.Write(modifier.m_settings.m_rotation);
            if (modifier.m_settings.m_rotation) pkg.Write(modifier.transform.forward);
            modifier.m_settings.Serialize(pkg, modifier.gameObject);

            Reach.Append(pkg, radius);

            nview.InvokeRPC("RPC_ApplyOperation", pkg);
            return false;
        }

        // -- 3. Receiving it --------------------------------------------------------------

        /// <summary>
        /// Reads the appended radius before vanilla parses the package, and puts the read
        /// position back so vanilla sees exactly what it expects.
        ///
        /// It writes unconditionally, including the "nothing there" value. Leaving a stale
        /// radius behind would apply one player's Crafting to the next player's swing, and
        /// because both swings are perfectly ordinary it would look like the mod randomly
        /// choosing a width.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TerrainComp), "RPC_ApplyOperation")]
        private static void ReceiveReach(ZPackage pkg)
        {
            if (!JafnaConfig.Enabled.Value)
            {
                Reach.SetIncoming(-1f);
                return;
            }

            Reach.SetIncoming(Reach.Peek(pkg, out float radius) ? radius : -1f);
        }

        // -- 4. Applying it, and choosing the height --------------------------------------

        /// <summary>
        /// The one that matters. Runs on the client that owns the zone.
        ///
        /// Patched on DoOperation rather than InternalDoOperation because DoOperation also
        /// resets the grass inside modifier.GetRadius(), and a wider swing that reset vanilla's
        /// narrower circle would leave a ring of grass standing on ground that is now flat -
        /// until the zone reloaded, at which point it would fix itself, which is the most
        /// annoying possible way for a bug to behave.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TerrainComp), "DoOperation")]
        private static void LevelTo(TerrainComp __instance, ref Vector3 pos, ref TerrainOp.Settings modifier)
        {
            if (!JafnaConfig.Enabled.Value) return;

            float incoming = Reach.TakeIncoming();

            if (!Reach.IsFlatten(modifier)) return;

            if (incoming > Reach.VanillaRadius(modifier))
            {
                modifier = Reach.With(modifier, incoming);
            }

            // LevelTerrain is handed pos + up * m_levelOffset, so the probe has to be taken at
            // that height and the answer put back below it. On the hoe's own ops the offset is
            // zero and this reads as ceremony; it is here because the field exists, other ops
            // use it, and a mod that ignores it would be wrong on exactly those.
            float offset = modifier.m_levelOffset;
            Vector3 probe = pos + Vector3.up * offset;

            float target = Flat.Target(
                __instance, probe, Reach.VanillaRadius(modifier), Reach.IsSquare(modifier), out Flat.Source source);

            if (source == Flat.Source.ContinuedFlat)
            {
                pos.y = target - offset;
            }

            if (JafnaConfig.Verbose.Value)
            {
                JafnaPlugin.Log.LogInfo(
                    "Level at " + pos.x.ToString("0.0") + "/" + pos.z.ToString("0.0")
                    + ", radius " + Reach.VanillaRadius(modifier).ToString("0.0") + "m"
                    + ", crosshair " + probe.y.ToString("0.00") + "m"
                    + ", used " + (probe.y + (pos.y - (probe.y - offset))).ToString("0.00") + "m"
                    + " (" + source + ", found " + Flat.LastFound
                    + ", used " + Flat.LastUsed
                    + ", spread " + Flat.LastSpread.ToString("0.000") + "m"
                    + " vs tolerance " + JafnaConfig.ContinueTolerance.Value.ToString("0.000") + "m).");
            }
        }

        // -- 5. The ward footprint, and the readout ---------------------------------------

        /// <summary>
        /// Vanilla's own ward test for a terrain op is
        /// PrivateArea.CheckAccess(ghostPosition, 0f, ...) - a single point, whatever the tool
        /// actually covers. This adds the footprint test on top of it and can only ever refuse
        /// more, never allow something vanilla refused.
        ///
        /// It overwrites the status only when vanilla had already decided the spot was fine.
        /// Writing over an existing refusal would change nothing about whether the swing lands
        /// and would replace a more specific reason with a less specific one.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static void WardFootprint(Player __instance)
        {
            if (!JafnaConfig.Enabled.Value) return;
            if (__instance != Player.m_localPlayer) return;
            if (!Bind()) return;

            TerrainOp.Settings settings = SelectedLevelOp(__instance);

            ReportSkill(__instance, settings);

            if (settings == null)
            {
                Readout.Clear();
                return;
            }

            GameObject ghost = _placementGhost(__instance);
            if (ghost == null)
            {
                Readout.Clear();
                return;
            }

            float radius = Reach.Earned(settings, __instance);
            Vector3 point = ghost.transform.position;

            bool wardClear = Reach.FootprintClear(point, radius, Reach.IsSquare(settings));

            if (!wardClear && _placementStatus(__instance) == Player.PlacementStatus.Valid)
            {
                _placementStatus(__instance) = Player.PlacementStatus.PrivateZone;
            }

            Readout.Update(__instance, settings, point, radius, wardClear);
        }

        /// <summary>
        /// Logs the levelling tool's piece table skill once a session, behind Verbose.
        ///
        /// It is here and not in a comment because it cannot be answered any other way. A piece
        /// table is asset data: ilspycmd sees the field and never its value, and a devkit rip
        /// resolves names through ZNetScene and ObjectDB, where _HoePieceTable is not
        /// registered at all. Whether the hoe names Crafting decides whether this mod extends a
        /// rule the game already has - Player.GetBuildStamina already discounts build stamina
        /// by that skill - or invents one, and that is worth knowing before the next version
        /// argues either way.
        /// </summary>
        private static void ReportSkill(Player player, TerrainOp.Settings settings)
        {
            if (_skillReported || settings == null || !JafnaConfig.Verbose.Value) return;
            _skillReported = true;

            PieceTable table = _buildPieces(player);
            string skill = table == null ? "no piece table" : table.m_skill.ToString();

            JafnaPlugin.Log.LogInfo(
                "Levelling tool's piece table skill: " + skill
                + ". Crafting " + CraftingLevel(player).ToString("0")
                + " gives a reach of " + Reach.Earned(settings, player).ToString("0.0")
                + "m against the tool's own " + Reach.VanillaRadius(settings).ToString("0.0") + "m.");
        }

        internal static float CraftingLevel(Player player)
        {
            return player == null ? 0f : player.GetSkillFactor(Skills.SkillType.Crafting) * 100f;
        }
    }
}

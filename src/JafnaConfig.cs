using BepInEx.Configuration;

namespace Jafna
{
    /// <summary>
    /// Everything tunable, bound in one place so the .cfg reads as a document rather than as
    /// whatever order the code happened to need things in.
    ///
    /// The standing BepInEx trap applies: every entry is written to disk on first run and the
    /// saved value beats a new default in code. Changing a default here does nothing on a
    /// machine that has already run the plugin - edit
    /// <c>&lt;profile&gt;\BepInEx\config\ezomic.valheim.jafna.cfg</c> as part of the same
    /// change. When a config-driven change appears to do nothing in game, read the cfg before
    /// reading any code.
    /// </summary>
    internal static class JafnaConfig
    {
        internal static ConfigEntry<bool> Enabled;

        internal static ConfigEntry<bool> ContinueFlat;
        internal static ConfigEntry<float> ContinueTolerance;
        internal static ConfigEntry<int> ContinueMinVertices;

        internal static ConfigEntry<float> MinRadius;
        internal static ConfigEntry<float> MaxRadius;
        internal static ConfigEntry<float> FullLevel;
        internal static ConfigEntry<float> Curve;

        internal static ConfigEntry<bool> RespectWardFootprint;

        internal static ConfigEntry<bool> ShowReadout;
        internal static ConfigEntry<bool> Verbose;

        internal static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("Jafna", "Enabled", true,
                "Off leaves the plugin loaded and changing nothing - vanilla levelling, "
                + "vanilla reach, no readout. Not \"unloaded\": a plugin cannot unload itself, "
                + "and a switch that pretends otherwise is a lie somebody will debug.");

            // -- Continuing a flat -------------------------------------------------------

            ContinueFlat = cfg.Bind("Levelling", "ContinueFlat", true,
                "The whole point of the mod. Vanilla levels every point under the tool to "
                + "whatever height your crosshair happens to be resting on, so walking two "
                + "steps moves the target and the overlap between two swings ends up at two "
                + "heights. That is why a large yard comes out rippled however carefully you "
                + "work. With this on, a swing that touches ground you have already levelled "
                + "takes THAT height instead of your crosshair's, so flat spreads outward "
                + "from wherever you started and keeps spreading across sessions. A swing "
                + "touching nothing you levelled before behaves exactly like vanilla, which "
                + "is how you start a new platform at a new height.");

            ContinueTolerance = cfg.Bind("Levelling", "ContinueTolerance", 0.05f,
                "How far apart, in metres, the already-levelled ground under one swing may be "
                + "before the mod gives up and lets vanilla decide. This is the setting that "
                + "stops two platforms at different heights being averaged into a ramp "
                + "through the middle: stand where a 2m terrace meets a 4m one, the readings "
                + "disagree by 2m, and the swing falls back to your crosshair so you stay in "
                + "control. Raising it past about half a metre turns that safety off and "
                + "starts inventing heights that were never on screen.");

            ContinueMinVertices = cfg.Bind("Levelling", "ContinueMinVertices", 3,
                "How many already-levelled grid points a swing must touch before it will "
                + "adopt their height. Terrain is stored on a 1 metre grid, so this reads as "
                + "\"at least three square metres of existing flat\". One is too few: a single "
                + "stray point left over from an old op, or from the corner of something "
                + "levelled last winter, would silently capture the swing and drag a new "
                + "platform to a height you cannot see.");

            // -- Reach -------------------------------------------------------------------

            MinRadius = cfg.Bind("Reach", "MinRadius", 0f,
                "Levelling radius in metres at Crafting 0. Zero means the mod never makes the "
                + "tool narrower or wider than vanilla at the bottom of the skill - the reach "
                + "used is always the larger of this curve and the tool's own radius, so a "
                + "new character gets exactly the hoe the game shipped. Raise it only if you "
                + "want the reach handed over rather than earned.");

            MaxRadius = cfg.Bind("Reach", "MaxRadius", 6f,
                "Levelling radius in metres once Crafting reaches FullLevel. The tool levels "
                + "a square, so 6 is a 12x12 metre platform in one swing - a longhouse "
                + "footprint and its doorstep. Every extra metre buys three things at once "
                + "and only one of them is good: fewer swings, a wider strip of ground you "
                + "can flatten inside somebody else's ward before the check stops you, and a "
                + "bigger block of height data saved into the zone and pushed to everyone "
                + "near it. Terrain does not heal, so this number is worth being stingy with.");

            FullLevel = cfg.Bind("Reach", "FullLevel", 60f,
                "The Crafting level at which the radius reaches MaxRadius. Above it nothing "
                + "changes. Deliberately not 100, which costs roughly 20,300 crafts against "
                + "about 5,700 for 60 - a reward nobody arrives at is a reward that does not "
                + "exist. Skaft uses the same number for the same reason and they are meant "
                + "to stay in step.");

            Curve = cfg.Bind("Reach", "Curve", 0.8f,
                "Exponent on the skill fraction: radius = min + (max-min) * "
                + "(level/FullLevel)^Curve. 1.0 is a straight line, below 1 opens the reach "
                + "earlier, above 1 hoards it for the top. Matches Skaft.");

            // -- Wards -------------------------------------------------------------------

            RespectWardFootprint = cfg.Bind("Wards", "RespectWardFootprint", true,
                "Refuse a level swing whose footprint does not clear a ward you have no "
                + "access to. Vanilla checks the single point under your crosshair and "
                + "nothing else, so even the stock hoe can already shave ground out from "
                + "under a neighbour's wall while the ghost stays blue and the ward never "
                + "flashes. That is a small hole at 2 metres and a wide one the moment "
                + "Crafting makes the tool bigger, which is why this setting exists and why "
                + "it defaults on. It is enforced where you swing, not where the world is "
                + "saved: terrain is applied by whichever client owns that zone and there is "
                + "no server-side validation anywhere in the path, so this stops the mod and "
                + "an honest player and cannot stop a hostile one. That was already true of "
                + "vanilla and this does not make it worse.");

            // -- Readout -----------------------------------------------------------------

            ShowReadout = cfg.Bind("Readout", "ShowReadout", true,
                "Put the numbers on screen while a levelling tool is out: the height under "
                + "your crosshair, the height the swing will actually use, and whether that "
                + "came from the crosshair or from flat ground the swing is continuing. "
                + "Without it the mod is invisible and indistinguishable from the hoe "
                + "behaving oddly, which is the complaint it was built to answer.");

            Verbose = cfg.Bind("Jafna", "Verbose", false,
                "Write what each swing decided to BepInEx/LogOutput.log. Off unless something "
                + "looks wrong; it is a line per swing. It also logs the levelling tool's "
                + "piece table skill once per session, which is the only way to read that "
                + "number - it is asset data and no decompiler or prefab rip can reach it.");
        }
    }
}

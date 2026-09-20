using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Ezomic.Core;
using HarmonyLib;

namespace Jafna
{
    /// <summary>
    /// Jafna. Levelling with the hoe continues the flat ground it touches instead of chasing
    /// your crosshair, and Crafting decides how far one swing reaches.
    ///
    /// Vanilla's level op sets every point under the tool to the height of the placement
    /// ghost, and the ghost sits wherever the crosshair last met the ground. The reference
    /// height therefore follows the camera: two swings taken a step apart level their shared
    /// overlap to two different heights, permanently, and no amount of care fixes it because
    /// the thing moving is the target rather than the aim. That is the whole reason a large
    /// yard comes out rippled. Jafna gives the swing somewhere fixed to level to, and the
    /// game was already storing it - TerrainComp flags every grid point a terrain op has
    /// touched, saved in the zone and synced like everything else. A swing that covers ground
    /// you levelled before takes that height; a swing that covers none behaves exactly like
    /// vanilla, which is how a new platform at a new height still starts.
    ///
    /// Reach is Skaft's rule on a second tool: Crafting buys reach and never buys a discount.
    /// The curve is shared source in core\shared\CraftingReach.cs so the two mods cannot come
    /// to mean different things by the same sentence.
    ///
    /// The ward rule is not a feature, it is the price of the reach. Vanilla tests a single
    /// point under the crosshair however wide the tool is, so even the stock hoe can already
    /// cut ground from under a neighbour's wall from outside their fence. Widening the tool
    /// widens that hole, so Jafna tests the whole footprint before it will let the swing land.
    ///
    /// Where the work happens: the height and the radius are both applied by whichever client
    /// owns that zone's TerrainComp, which is frequently not the player who swung. The height
    /// needs nothing to travel, because the flags it reads are already on the owner's machine.
    /// The radius does, and vanilla carries no room for it, so it is appended behind a magic
    /// number in a place nothing reads - see Reach.cs. An owner without Jafna never looks, and
    /// applies an ordinary vanilla op.
    ///
    /// No prefabs, no items, no recipes, no saved values of its own. A world played with Jafna
    /// is an ordinary world; the ground you shaped is ground vanilla's own op shaped.
    ///
    /// There is deliberately no BepInProcess attribute. A dedicated server runs
    /// valheim_server.exe, and Core's gate only refuses on the server side of RPC_PeerInfo -
    /// so a mod that must be enforced has to be allowed to load there.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Soft, not hard. A hard dependency that is absent does not degrade - the plugin never
    // loads at all - and every mod here has to be installable on its own, because a stranger
    // should not need two installs to get one mod. Soft still buys the load-order guarantee
    // when Core is present, which is all that registering with the gate needs.
    [BepInDependency(CoreGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class JafnaPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.jafna";
        public const string PluginName = "Jafna";
        public const string PluginVersion = "1.0.0";
        public const string PluginAuthor = "Robbin Thijssen";

        /// <summary>Core's plugin GUID. Optional - see TryRegisterWithCore.</summary>
        private const string CoreGuid = "ezomic.valheim.core";

        internal static ManualLogSource Log;

        /// <summary>
        /// Whether Core answered at load. Worth keeping even when nothing reads it yet: the
        /// difference between gated and ungated is invisible to a player otherwise, and this
        /// is what a warning on spawn would be driven by.
        /// </summary>
        internal static bool CorePresent;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            // Config first. Registering absorbs every entry the mod has bound, so anything
            // bound after this line is carried only because Core re-absorbs at manifest
            // time - and depending on the order of two lines in an Awake is not a thing
            // worth relying on.
            JafnaConfig.Bind(Config);

            TryRegisterWithCore();

            // PatchAll over a named type, never the whole assembly. A bare PatchAll() walks
            // every type in the DLL, so a half-written patch class in another file goes live
            // the moment it compiles.
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(JafnaPatches));

            // Named separately rather than by a bare PatchAll(). Readout carries its own
            // patch because the restore has to happen on a method that keeps running after
            // the tool is put away, and the rule that nothing goes live by merely being
            // written is worth more than the one saved line.
            _harmony.PatchAll(typeof(Readout));

            // The startup line every mod in the suite writes. It is how a log answers "which
            // build of what is actually loaded" without anyone guessing.
            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
        }

        /// <summary>
        /// Joins Core's version gate when Core is installed, and does nothing when it is not.
        ///
        /// Name here exactly what standing alone costs, because it is usually not the mod.
        /// For most of these it is the *enforcement*: without Core nothing refuses a client
        /// that lacks the plugin, so the rule becomes an agreement between players rather
        /// than a property of the server. That is a real loss and a legitimate choice, and
        /// it is the server owner's to make - which is why this logs rather than refusing
        /// to run.
        /// </summary>
        private void TryRegisterWithCore()
        {
            CorePresent = Chainloader.PluginInfos.ContainsKey(CoreGuid);

            if (!CorePresent)
            {
                Log.LogInfo("Core not installed - running standalone, without the version gate.");
                return;
            }

            RegisterWithCore();
        }

        /// <summary>
        /// Kept separate and never inlined on purpose. The JIT resolves the assemblies a
        /// method needs when it first compiles that method, so a Suite call sitting directly
        /// in Awake would drag Ezomic.Core in before the check above could prevent it - and
        /// the missing-assembly exception would land during plugin load, which is the exact
        /// failure this arrangement exists to avoid. Isolating it means the type is only
        /// ever resolved on a machine that has Core.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RegisterWithCore()
        {
            // HostOnly, and it is load-bearing rather than lazy. Jafna registers no prefab and
            // writes no value of its own, so a player without it cannot fail to resolve
            // anything - every swing they take is a vanilla swing and every swing they see is
            // already applied by whoever owns that zone. Everyone would refuse those players
            // the server for no gain. Core treats HostOnly symmetrically, so a Jafna client can
            // also still join a server that does not run it and simply gets vanilla reach.
            //
            // What standing without Core costs here is the ward rule. Nothing then refuses a
            // client that lacks the plugin, so "the footprint has to clear the ward" becomes an
            // agreement between players rather than a property of the server. That is a real
            // loss and it is the server owner's to take, which is why this logs rather than
            // refusing to run.
            Suite.Register(PluginGuid, PluginName, PluginVersion, Config, Requirement.HostOnly);

            // Registering already absorbs the whole config file, so these are a formality. They
            // are still worth writing: naming an entry here is saying out loud that the host
            // decides it, and for these three that is the point. Reach and the ward footprint
            // decide what a player may do to shared ground, and a table where everyone brought
            // their own MaxRadius is not a table anybody agreed to sit at.
            Suite.Sync(JafnaConfig.Enabled);
            Suite.Sync(JafnaConfig.MaxRadius);
            Suite.Sync(JafnaConfig.RespectWardFootprint);

            // If the mod reads a data file that decides what it does, hash it too. The gate
            // catches two ends on different builds; it cannot catch two ends running the
            // same build over different text unless it is told.
            //
            //     Suite.Data(File.ReadAllText(path));
        }

        private void OnDestroy()
        {
            // UnpatchSelf, never UnpatchAll(). The argumentless one unpatches every mod in
            // the process, not just this one.
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }
}

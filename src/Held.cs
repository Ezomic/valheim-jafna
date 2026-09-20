using UnityEngine;

namespace Jafna
{
    /// <summary>
    /// A height the player has pinned by hand, which every swing then flattens to.
    ///
    /// Continuing the flat you touch answers "what height is this ground meant to be" from the
    /// ground itself, and that is right most of the time and unarguable none of the time. Two
    /// cases it cannot settle on its own showed up within an hour of playing: a higher
    /// platform beside you captures swings you are aiming lower, and a swing straddling a zone
    /// line has each zone decide separately. Both are the same shape of problem - the mod
    /// guessing at an intention the player already has.
    ///
    /// So: stand where the floor should be, press the key, and the height under your crosshair
    /// is held. Walk anywhere, aim anywhere, and every swing eases the ground toward that one
    /// number until you press again. Nothing is guessed while it is held, nothing is searched
    /// for, and the ambiguity above cannot arise.
    ///
    /// This began as a buildable marker you would plant and pull up, which was rejected for
    /// being an object to carry and place when the job only needs a number remembered. The key
    /// does the same work with nothing in the world, and unlike the marker it costs no prefab,
    /// no recipe and no saved data.
    ///
    /// It is deliberately not persisted. A height held across a logout is a trap: you would
    /// come back, swing, and watch the ground move to a number you set yesterday for a reason
    /// you no longer remember, with nothing on screen having changed to warn you. Held for the
    /// session, and the readout says so on every frame it is on.
    /// </summary>
    internal static class Held
    {
        internal static bool Active;
        internal static float Height;

        /// <summary>
        /// Pins the height under the crosshair, or lets go of the one being held.
        ///
        /// Takes the height rather than reading it, because the caller already has the
        /// placement ghost's position and that is the exact point the swing would have used.
        /// Reading the ground again here would be a second raycast that could disagree with
        /// the first by a hair, which is the kind of difference that turns up later as a step
        /// nobody can account for.
        /// </summary>
        internal static void Toggle(float height)
        {
            if (Active)
            {
                Active = false;
                if (JafnaConfig.Verbose.Value)
                {
                    JafnaPlugin.Log.LogInfo("Released the held height.");
                }
                return;
            }

            Active = true;
            Height = height;

            if (JafnaConfig.Verbose.Value)
            {
                JafnaPlugin.Log.LogInfo("Holding " + height.ToString("0.00") + "m.");
            }
        }

        /// <summary>
        /// Drops the held height without a message, for the paths where holding one no longer
        /// means anything - the config being turned off, or the mod being disabled by a host.
        /// </summary>
        internal static void Release()
        {
            Active = false;
        }
    }
}

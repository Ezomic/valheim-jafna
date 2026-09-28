using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Jafna
{
    /// <summary>
    /// Reading a configured key, the way the game reads one.
    ///
    /// Copied from Vaettir's Furrow rather than shared, because it is twenty lines and a
    /// shared file that two mods must both have is a dependency; this one is not worth that.
    /// The tap below grew here and stays here for the same reason: Malmr reads its own toggle
    /// its own way, and two mods with no shared code cannot drift into each other's bugs.
    ///
    /// It exists because of a bug that presents as "the key does nothing" while the config
    /// file holds exactly the right value, which is about the least helpful symptom available.
    /// Valheim runs on the new Input System, and <c>ZInput.GetKeyDown</c> routes a KeyCode to
    /// Mouse.current, Keyboard.current or Gamepad.current by hand. The legacy
    /// <c>UnityEngine.Input</c> class does not see the middle mouse button here at all.
    /// </summary>
    internal static class Keys
    {
        /// <summary>What a <see cref="Tap"/> saw of its key this frame.</summary>
        internal enum Edge
        {
            None,

            /// <summary>The key went down on its own, with the game in front. Nothing is decided yet.</summary>
            Down,

            /// <summary>The key came back up, and nothing about the press says it was anything but a tap.</summary>
            Tapped
        }

        /// <summary>
        /// A key read as a tap: pressed on its own and let go again, with the game window in front
        /// of the player the whole time. Acted on when it comes back up, never when it goes down.
        ///
        /// This replaced reading the key's down edge, and the reason is Alt+Tab. The hold key
        /// defaults to Left Alt, and the first scenario run on 2026-09-28 logged "Holding 31,66m."
        /// part-way through a run nobody was playing, most likely from switching to another
        /// window. Switching windows is Alt, then Tab, and the Alt goes down while the game still
        /// has the focus, so a down edge has already toggled the hold before Windows takes the
        /// window away. The held height then stayed on for the rest of the session and every
        /// swing after it went to a number nobody chose.
        /// A down edge cannot tell a tap from the start of a chord, because the chord has not
        /// happened yet. The release can, so the decision waits for it.
        ///
        /// A press is spoiled, and its release does nothing, when:
        ///
        ///  - any other key or mouse button goes down while it is held. That is Alt+Tab when the
        ///    game sees the Tab at all, and it is every other chord with the key in it, vanilla's
        ///    own alt-placement click included. Keys that went down in the same frame as this one
        ///    are not counted, because some keyboard layouts send Right Alt as a Left Control and a
        ///    Right Alt together, and counting that would make a Right Alt hold key never work.
        ///  - the game window does not have the focus when it goes down, when it comes up, or in
        ///    any frame between. This is the rule that catches Alt+Tab when Windows swallows the
        ///    Tab, which it usually does: the focus goes with it.
        ///  - a frame went by without this being read. Then part of the press happened where it
        ///    could not be watched, a focus change or another key included, so it is not vouched
        ///    for. It is read from the placement ghost update, which stops the moment the hoe is
        ///    put away or the selection leaves a levelling entry.
        ///  - chat, the console or a text field has the keystroke, at either end. Without that a
        ///    key typed into chat also works the tool, which reads as the hold toggling on its own.
        ///
        /// Nothing else changed. A press held for as long as you like still counts, because the
        /// key never had a time limit and a slow press is still a press. Only a key that goes
        /// down while this one is held counts as another key, so pressing it while already
        /// walking with W held is fine.
        /// </summary>
        internal sealed class Tap
        {
            private bool _held;
            private bool _spoiled;
            private int _seen = -1;

            /// <summary>
            /// Reads the key for this frame. Once a frame at most: the placement ghost is updated
            /// twice in a frame where a swing lands, once from the swing and once from LateUpdate,
            /// and a second read would find the same edge again. The down-edge version, read from
            /// the same place, toggled on and straight back off when the key went down in the
            /// frame of a swing.
            /// </summary>
            internal Edge Read(KeyCode key)
            {
                int frame = Time.frameCount;
                if (frame == _seen) return Edge.None;

                bool watched = frame == _seen + 1;
                _seen = frame;

                if (key == KeyCode.None)
                {
                    _held = false;
                    return Edge.None;
                }

                if (_held && !watched) _held = false;

                if (!_held)
                {
                    // logWarning false, because ZInput grumbles about KeyCodes it cannot map, and a
                    // key nobody bound is a configuration choice rather than a fault.
                    if (!ZInput.GetKeyDown(key, false)) return Edge.None;

                    _held = true;
                    _spoiled = !Undisturbed();
                    return _spoiled ? Edge.None : Edge.Down;
                }

                if (!Application.isFocused || OtherKeyWentDown(key)) _spoiled = true;

                // Still held, so nothing is decided. Asked as "is it down" rather than "did it come
                // up this frame", so a press and release inside one frame still ends here a frame
                // later instead of being missed.
                if (ZInput.GetKey(key, false)) return Edge.None;

                _held = false;
                return !_spoiled && Undisturbed() ? Edge.Tapped : Edge.None;
            }
        }

        /// <summary>The game has the focus and no text field has the keystroke.</summary>
        private static bool Undisturbed()
        {
            if (!Application.isFocused) return false;
            if (Chat.instance != null && Chat.instance.HasFocus()) return false;
            return !Console.IsVisible() && !TextInput.IsVisible();
        }

        // Every KeyCode ZInput can read, and the Input System key behind each one. ZInput has no
        // "was any key pressed" of its own, and asking it about a KeyCode it has no key for does
        // not answer false: it indexes the keyboard with Key.None and throws. So the list is taken
        // from ZInput's own table rather than from the KeyCode enum. The table is private, which
        // is why it is bound lazily and inside a try/catch: a rename costs the chord rule, with a
        // warning, and the focus rule still stands.
        private static KeyCode[] _others;
        private static object[] _controls;
        private static bool _othersBound;

        private static bool BindOthers()
        {
            if (_othersBound) return _others != null;
            _othersBound = true;

            try
            {
                FieldInfo field = AccessTools.Field(typeof(ZInput), "s_keyCodeToKeyMap");
                IDictionary map = field == null ? null : field.GetValue(null) as IDictionary;
                if (map == null) throw new MissingFieldException("ZInput", "s_keyCodeToKeyMap");

                List<KeyCode> keys = new List<KeyCode>();
                List<object> controls = new List<object>();

                foreach (DictionaryEntry entry in map)
                {
                    if (!(entry.Key is KeyCode code) || code == KeyCode.None) continue;

                    keys.Add(code);
                    controls.Add(entry.Value);
                }

                // The five mouse buttons ZInput reads. Each is its own control, so each stands for
                // itself; a KeyCode never equals an Input System key.
                for (KeyCode code = KeyCode.Mouse0; code <= KeyCode.Mouse4; code++)
                {
                    keys.Add(code);
                    controls.Add(code);
                }

                _others = keys.ToArray();
                _controls = controls.ToArray();
            }
            catch (Exception e)
            {
                _others = null;
                _controls = null;
                JafnaPlugin.Log.LogWarning(
                    "Could not read ZInput's key table, so the hold key cannot tell a tap from a chord "
                    + "this session and only the focus rule stops Alt+Tab from toggling it. " + e.Message);
            }

            return _others != null;
        }

        /// <summary>
        /// Whether any key or mouse button other than <paramref name="key"/> went down this frame.
        ///
        /// "Other" is by the key underneath, not the KeyCode. ZInput reads AltGr and Right Alt off
        /// the same key, so with Right Alt bound, AltGr is the same key and not a chord.
        /// </summary>
        private static bool OtherKeyWentDown(KeyCode key)
        {
            if (!BindOthers()) return false;

            object own = null;
            for (int i = 0; i < _others.Length; i++)
            {
                if (_others[i] != key) continue;

                own = _controls[i];
                break;
            }

            try
            {
                for (int i = 0; i < _others.Length; i++)
                {
                    if (_others[i] == key) continue;
                    if (own != null && own.Equals(_controls[i])) continue;

                    if (ZInput.GetKeyDown(_others[i], false)) return true;
                }
            }
            catch (Exception e)
            {
                // A key in the table the keyboard cannot read after all. Dropped for the session
                // rather than retried every frame the key is held.
                _others = null;
                _controls = null;
                JafnaPlugin.Log.LogWarning(
                    "Reading the other keys threw, so the hold key cannot tell a tap from a chord for "
                    + "the rest of this session. " + e.Message);
            }

            return false;
        }
    }
}

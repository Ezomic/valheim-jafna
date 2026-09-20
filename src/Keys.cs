using UnityEngine;

namespace Jafna
{
    /// <summary>
    /// Reading a configured key, the way the game reads one.
    ///
    /// Copied from Vaettir's Furrow rather than shared, because it is twenty lines and a
    /// shared file that two mods must both have is a dependency; this one is not worth that.
    ///
    /// It exists because of a bug that presents as "the key does nothing" while the config
    /// file holds exactly the right value, which is about the least helpful symptom available.
    /// Valheim runs on the new Input System, and <c>ZInput.GetKeyDown</c> routes a KeyCode to
    /// Mouse.current, Keyboard.current or Gamepad.current by hand. The legacy
    /// <c>UnityEngine.Input</c> class does not see the middle mouse button here at all.
    /// </summary>
    internal static class Keys
    {
        /// <summary>
        /// True on the frame the key goes down, unless the keystroke belongs to a text field.
        /// Without that last part a key typed into chat or the console also works the tool,
        /// which reads as the lock toggling on its own.
        /// </summary>
        internal static bool Pressed(KeyCode key)
        {
            if (key == KeyCode.None) return false;

            // logWarning: false - ZInput grumbles about KeyCodes it cannot map, and a key
            // nobody bound is a configuration choice rather than a fault.
            if (!ZInput.GetKeyDown(key, false)) return false;

            if (Chat.instance != null && Chat.instance.HasFocus()) return false;
            return !Console.IsVisible() && !TextInput.IsVisible();
        }
    }
}

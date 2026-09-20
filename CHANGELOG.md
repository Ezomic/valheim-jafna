# Changelog

## 0.1.0 - unreleased

First version. Built, deployed, never run in a game.

**Levelling continues the flat ground it touches.** Vanilla's level operation sets every point
under the tool to the height of the placement ghost, which sits wherever your crosshair last
met the ground, so the height a swing aims at follows the camera. Two swings taken a step apart
level their shared overlap to two different heights and the second one wins, permanently. That
is the reason a large flat area is miserable to make, and it is worth saying plainly because it
looks like the opposite problem: the tool feels too small, and every attempt to tidy an edge is
another swing at another height, so working more carefully makes it worse. Jafna reads the
per-point "a terrain operation touched this" flag the game already saves in each zone, and a
swing covering ground that was levelled before uses that height instead of the crosshair's. A
swing covering none is untouched, so starting a new platform at a new height still works.
Where two platforms at different heights meet under one swing the heights disagree by more than
`ContinueTolerance` and the decision goes back to the crosshair, rather than averaging them
into a ramp nobody asked for.

**Reach grows with Crafting**, on Skaft's rule and off Skaft's curve, which now lives in
`core\shared\CraftingReach.cs` so the two mods cannot drift into meaning different things by
the same sentence. Skaft still carries its own copy of the arithmetic and should adopt the
shared file the next time it is opened for a real change; refactoring a published mod is its
own release. Nothing at low levels, 12x12 metres at Crafting 60, and never a discount - a swing
costs the stamina vanilla charges.

**A ward you cannot use blocks the whole swing.** Vanilla tests the single point under your
crosshair and nothing else, whatever the tool covers, so even the stock hoe can already cut
ground from under a neighbour's wall from outside their fence with the ghost showing blue.
Jafna tests the square footprint. It is enforced where you swing, not where the world is saved,
because terrain is applied by whichever client owns the zone with no server-side validation
anywhere in that path. It stops the mod and an honest player, not a hostile one, and that was
already true of vanilla.

**The build panel carries the numbers** while a levelling tool is out: reach and the Crafting
it came from, the height the swing will use, and whether that height came from your crosshair
or from flat the swing is continuing. That last part is not decoration. A tool that quietly
overrules your aim is indistinguishable from a tool that has started misbehaving, which is the
complaint this mod exists to answer rather than to join.

### Known and open

- Never run in a game. Everything above is an argument from decompiled source.
- Whether the hoe's piece table names Crafting is still unknown. If it does, levelling already
  earns that skill and already takes vanilla's build stamina discount from it, and this extends
  a rule the game has instead of inventing one. It is asset data that no decompiler or prefab
  rip can reach, so the mod logs it once a session under `Verbose`.
- The appended reach travels to a Jafna owner and is invisible to a vanilla one, by design. Two
  players on very different Crafting levels working the same ground has not been watched.

# Changelog

## 1.0.0 - 20 September 2026

First release. Built, played and corrected in one sitting; the "Verified in game" section
below is what was actually watched rather than what was argued from the source.

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

### Corrected during the first play test

Three things this was built on turned out to be wrong, all of them asset data that only the
running game could answer.

**The hoe does not use a level operation.** Its Level ground entry is `mud_road_v2`, and it is
`m_smooth` with `m_level` false, radius 3 metres, power 1, painting dirt over the same 3. Raise
ground is `m_raise` and Path is paint only; nothing in the hoe's table uses `m_level` at all.
Every guard in the mod tested `m_level`, so it correctly decided the hoe was none of its
business and did nothing, with a clean log. Retargeted onto `m_smooth || m_level`.

**The vanilla radius is 3 metres, not 2.** The 2 was `m_levelRadius`, which this op never
reads. That moves the whole curve: with `MaxRadius` at 6 nothing changes until about Crafting
25, and the top of the curve is double the vanilla radius rather than triple.

**The agreement test was asking for something the tool cannot produce.** Requiring the whole
footprint to agree within 5 cm refused nearly every swing on ground that was visibly being
flattened, because `SmoothTerrain` eases from full effect at the centre to nothing at the rim -
a smoothed patch is a shallow dish, not a plateau. It now judges only the eight points nearest
the crosshair, which are the ones an earlier swing centred on and pulled all the way to its
target, takes their median, and defaults to a 0.25 m tolerance.

**The hoe's piece table names no skill.** So using it raises nothing and `GetBuildStamina`
skips its discount branch - there is no double dip here, unlike the hammer, and reach is the
only thing Crafting buys. It also means the reach is not self-earning, which is the same shape
as Skaft, where repairing buildings trains nothing either.

### Verified in game, 20 September 2026

Singleplayer, one world, Crafting 52, reach 5,4m against the hoe's own 3,0m.

- **Continuing the flat holds a height across a working area.** Swings metres apart all landed
  on 32,08-32,10 from crosshairs at 32,66, so over half a metre of drift removed per swing, with
  the eight nearest points agreeing to within 4-26mm against a 250mm tolerance.
- **Holding a height works in both directions.** Standing 1,7m above the held number and 1,9m
  below it, every swing aimed at the held figure rather than at the player's feet. Toggle on and
  off both clean.
- **Non-terrain tools are untouched.** `piece_repair` and `woodwall` both report no terrain op
  and the mod attaches nothing to them.

### Known and open

- The ward footprint has never refused anything in a test - there was no ward to refuse it.
- **A held height above about a metre away cannot be reached, and it stays that way.**
  SmoothTerrain clamps its accumulated movement to one metre per point and only a level or raise
  operation banks that and frees the budget, so holding 32,08 while standing on 33,76 brings the
  ground down to roughly 32,76 and stops, short of the number the panel is showing. That is
  vanilla's clamp rather than anything here. Converting a held swing into a true level operation
  would remove the ceiling in two lines - **decided against**, because it would turn the hoe into
  a tool that sets ground to any height in one swing, and every mod in this suite is meant to be
  narrower than the thing it replaces. Raise ground first, then hold and flatten. Documented in
  the README so it does not read as a fault.
- Left Alt is confirmed double booked: mud_road_v2's piece sets both `m_groundPiece` and
  `m_allowAltGroundPlacement`, which is exactly the condition `Player.UpdatePlacementGhost`
  requires before it reads `AltPlace`. In practice a tap captured 32,07 on a flat sitting at
  32,08-32,10, so the single frame of alt placement does not appear to shift the reading.
  **Left Alt stays as the default**, on that evidence and because it is the key that was asked
  for; `HoldKey` is configurable if the ghost ever misbehaves, and a server never takes a
  keybind over.
- `SmoothTerrain` clamps its accumulated movement to one metre per point and only a level
  operation banks that and frees the budget, so flattening with the hoe is capped near a metre
  per point however the target is chosen. Whether that ceiling is the real obstacle in practice
  is untested.
- The appended reach travels to a Jafna owner and is invisible to a vanilla one, by design. Two
  players on very different Crafting levels working the same ground has not been watched.

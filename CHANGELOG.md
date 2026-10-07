# Changelog

## Unreleased

**A switch for the stone fill, yours alone.** `AutoRaise` is the host's rule and a file edit, so
on a server a player could not turn the stone off for themselves. There is now `StoneFill`, flipped
in game with J (`StoneFillKey`) while a levelling tool is out. Off, a swing is the hoe's alone: its
free metre, no stone taken, no price on the panel. The panel's Stone fill row, the last row of the Cost group, says whether it is on, and
the key says what it did in the top left. The host's `AutoRaise` stays the upper bound, so off there
is off for everybody and the panel says so. The switch is personal, declared with `Suite.Local` so
Core never syncs it, and it is kept in the cfg between sessions. A new console command,
`jafna stone on|off`, flips it the way the key does, for the scenario
(`scenarios/jafna-stone-switch.txt`), which checks that a swing far below the aimed height takes
no stone and stops at the free metre with the switch off, and fills with it on. Built and
compiled, not yet run in game.

**The hoe's tooltip stays one size.** The build panel box used to grow and shrink as you swept the
crosshair, because the readout gained or lost lines (a warning, a price, the 8 m note) and some of
them were long enough to wrap. It is now thirteen rows in every state, in three labelled groups under
a single warning slot: Ground (how far the ground comes up and goes down, how wide the swing is),
Height (the crosshair, the height the swing uses, where that came from) and Cost (stone, the
workbench and whether stone fill is on). A row with nothing to say holds a dash, the warning slot reads All clear and is
replaced in place by the one thing that is wrong, and no row is longer than the box, so it never
grows sideways or wraps. The numbers are the same ones as before, spread over more rows, with
Raise up to and Lower up to new.

Three small wording changes follow from keeping every row inside the box and a changing number at
the end of its row: the stone row reads `carry 20, need 14 Stone`, naming the item once at the
end (the longer wording was 41 characters in the commonest state and got cut), the short-of-stone
sentence moved into the warning slot as `Short of stone, fills only the middle`, and the held
height reads `HOLDING until LeftAlt` instead of `HOLDING, LeftAlt releases`, which was too long
for a Control key. A key name over 14 characters is cut to 14 on that row. The widest case of
every row is worked out in a comment in the code and is 40 characters or fewer.

The panel is no longer rebuilt on every frame: it is rebuilt when the aim (to the nearest
centimetre), the reach, the price, the pack, the held key or the language changes. Item names are
looked up once per language. When you scroll to another entry of the same hoe, the old entry's
numbers no longer show on the new one for a frame. The readout also logs an error once if it ever
builds anything other than thirteen rows.

A Devkit scenario, `jafna-readout-one-size`, runs the panel through flat ground, a fillable spot, a
short pack, no workbench and a Crafting 100 swing with fifty stones. It holds the drawn line count
and height equal and checks that Jafna's own string is thirteen rows, shown in the label, with no row
cut and the Stone and Workbench rows holding a dash or text as expected. It reads them through a new
`jafnareadout` console command, a development command that ships in the build, only reads and is not
a cheat. Built, not yet seen in a game.

## 1.1.0 - 30 September 2026

**Low ground is raised, and paid for in stone.** This reverses the call made on 20 September
to keep the one metre ceiling. The hoe's flattening moves a point a metre at most and then
stops, so a swing aimed well above the ground never got there. Now the rest is raised, and your
pack pays for it at the rate of the hoe's own Raise ground: what that entry costs, divided by
the ground one Raise ground swing adds to flat ground. Both numbers are read from the running
game and written to the log once. Alternating Raise ground and Level ground by hand can still
be cheaper, because every Raise ground gives Level ground its free metre back and a fill does
not.

A paid swing lifts its whole circle to your height at once, and leaves a step at the edge of the
circle until the next swing beside it that needs stone. A step under a metre is eased away by
ordinary swings instead. That shape was picked over letting the stone follow the hoe's easing,
which would finish only the middle of a swing and creep outward over the next few: the flat top is
faster, and the step is its price. A paid swing takes more stone than one that followed the easing,
because it does the work of several at once, but the ground costs the same in the end. Over ground
two metres low that is about 7 Stone a swing at the hoe's own reach and about 27 at 12 metres
across.

Only the part the hoe could never do is charged. Every point comes up the metre the hoe would have
given it for free, less what earlier swings used of it, and you pay for the rest of the way. A swing
where every point is within that metre of your height is one the hoe can finish on its own, and it
is left exactly as vanilla has it and costs nothing, so the flat top comes with paying and ordinary
flattening is untouched. A point less than three centimetres past its free metre is not charged
for, so going over flat ground does not cost stone. Stone comes out whole and the remainder carries
over to the next swing until you log out. NoBuildCost makes it free, and nocost behaves the way it
does for vanilla's Raise ground. It is on by default as `AutoRaise`, and the host decides it on a
server.

Each point comes all the way up or not at all, and you pay only for what came up. Short of stone,
a swing lifts the middle of the circle first and works outward as far as the stone goes, and says
so. The ground it did not reach gets the hoe's own easing and nothing else, so a short swing leaves
a flat patch in its middle with a step round it. The game never lets ground sit more than eight
metres above where the world made it, so ground further below the height than that is left alone
too, rather than raised partway and charged for a height it can never reach. The build panel says
so before the swing, and the swing says "Too far below to raise".

The build panel says what a swing will cost before you take it, and that is what it takes. Short
of stone it says what you carry, what this swing fills and takes for it, and what all of it would
cost, or that it fills none of it when your stone does not reach even the middle.

**Raising with stone needs a workbench nearby**, the rule vanilla's Raise ground follows. The
station is read off the same Raise ground entry as the price, so on the hoe it is the workbench,
within its build range of where you stand. Out of range, a swing that would need stone is left to
the hoe, its free metre and its easing, takes no stone, and says `Raising needs a Workbench
nearby`, and the build panel says the same in place of the price. NoWorkbench and nocost lift it
and NoBuildCost does not, as for Raise ground.

**Alt+Tab no longer pins a height.** The hold key now counts when you let go of it, and only when
you pressed it on its own with the game in front the whole time. It used to act the moment it went
down, and switching windows starts with Left Alt going down while the game still has the focus, so
tabbing out with a levelling tool in your hands pinned a height and left it on for the rest of the
session. The first scenario run caught it holding a height nobody had asked for. The height held
is still taken when you press, not when you let go.

**Only your own swing is reshaped.** Continuing the flat, the reach Crafting buys and a held
height used to apply to any flattening operation, not only to the hoe in your hands: a location
shaping its own ground, another mod's operation, a Devkit flatten, and on a zone you own, a swing
from a player without Jafna. A scenario log caught a Devkit flatten levelled 0.2 m below its own
target, to a flat its circle happened to touch. Now all of them come through exactly as vanilla
has them, by the rule raising with stone already followed: the operation has to be your swing,
with the entry it came from selected. Your client marks its own swing with the note Jafna already
appends, now on every swing and not only a wide or held one, and the zone owner reshapes only what
carries that note. A client on 1.0.0 added the note only to a wide or held swing, so an owner on
this version no longer continues the flat under that client's other swings. The auto-raise
scenario has a new check for it, and no longer lets Devkit level the ground before it starts.

In multiplayer the client that owns the zone raises the ground and sends your client the bill,
and your client pays it out of your pack. A zone owner without this version raises nothing and
bills nothing, so you are never charged for ground that did not move. How far out your stone
reaches rides at the end of the message Jafna already appends, where 1.0.0 stops reading.
Whether you stand near a workbench is your client's answer too, and a swing it turns down never
asks the owner to raise anything.

Lowering has not changed. Ground well above the target still stops after about a metre, or two
where it was raised before.

### Verified in game, 29 September 2026

Singleplayer, a fresh test world. The auto-raise scenario passed all 168 of its steps, and it
covers this much:

- Digging a mound down further than the hoe's metre takes no stone, and a swing with nothing to
  raise costs nothing and says nothing.
- With enough stone one swing lifts its whole circle to the height. Ground halfway to the edge
  is on the height, ground just outside the circle is where it was, and there is a step between.
- With no stone the ground comes up the hoe's own metre and stops, and the screen says "Not
  enough Stone". With too little, all of it goes, the middle comes up to the height, and nothing
  is left owing.
- Out of a workbench's range a swing that would be paid takes no stone and comes up the free
  metre only, and both the build panel and the middle of the screen say a Workbench is needed.
- A Devkit flatten beside a flat at another height levels to its own height, so an operation that
  is not your swing is left as vanilla has it.

Tapping Left Alt was checked by hand the same day: it holds a height, and Alt+Tab no longer does.

### Known and open

- Nothing here has run with a second player. The bill travelling between two machines, and an
  owner leaving a swing from a player without Jafna as vanilla, are argued from the source only.
- Not watched in a game: the price the build panel shows before a swing, ground past the eight
  metre limit being left alone and the message for it, leftover stone carrying over to the next
  swing, NoBuildCost, nocost and NoWorkbench, and a chord pressed with Left Alt spoiling the tap.

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

# Jafna

Levelling with the hoe continues the flat ground it touches instead of chasing your
crosshair, and your Crafting skill decides how much ground one swing covers.

*Jafna* is Old Norse for to level, to make even.

## Why

Flattening a large area with the hoe is miserable, and it is worth being precise about why,
because the obvious answer is wrong. It is not that the tool is small. It is that the tool has
no fixed idea of what height you are aiming for.

The hoe's Level ground eases every point under the tool toward the height of the placement
ghost, and the ghost sits wherever your crosshair last met the ground. So the reference height
follows the camera. Take one swing, walk two steps, take another, and the strip of ground both
swings covered is pulled toward two different heights. You cannot aim your way out of that,
because the thing moving is not your aim, it is the target. It is also why the ripples get
worse the more carefully you work around the edges: every correction is another swing at
another height.

Jafna gives the swing somewhere fixed to level to, and the game turns out to have been keeping
it all along. Every zone's terrain data carries a flag per grid point meaning "a terrain
operation has touched this", saved with the world and shared with everyone. So a swing can
look at the ground it is about to cover, find the parts of it a previous swing already
shaped, and use that height rather than your crosshair's. Flat spreads outward from wherever you started
it, at one height, and keeps spreading in your next session and under another player's hoe.

A swing that covers nothing you levelled before is left completely alone, which is how you
still start a new platform at a new height: stand clear of the old one and swing.

## What it does

- **A level swing continues the flat it touches.** If it covers ground you already levelled,
  it uses that height. If it covers none, it behaves exactly like vanilla.
- **It refuses rather than guesses.** Where two platforms at different heights meet under one
  swing, it hands the decision back to your crosshair instead of averaging them into a ramp.
- **You can hold a height.** Press Left Alt to pin the one under your crosshair, and every
  swing flattens toward it wherever you stand, until you press again.
- **Reach grows with Crafting.** Nothing below about Crafting 25, growing to a 6 metre radius
  at Crafting 60 against the hoe's own 3. Never a discount: a swing costs exactly the stamina
  vanilla charges.
- **A ward you cannot use blocks the whole swing, not just its centre.**
- **The build panel shows the numbers**: the reach you have, the height the swing will use, and
  where that height came from.
- No new prefabs, items, recipes or saved values. A world played with Jafna is an ordinary
  world, and the ground you shaped is ground vanilla's own operation shaped.

## Holding a height

Continuing the flat answers "what height is this ground meant to be" from the ground itself.
That is right most of the time and certain none of the time, and two cases it cannot settle
turned up within an hour of playing: a higher platform beside you captures swings you are
aiming lower, and a swing crossing a zone boundary has each zone decide from its own half of
the footprint.

Both are the mod guessing at an intention you already have, so you can state it instead. With
a levelling tool out, press **Left Alt** and the height under your crosshair is held. Walk
anywhere, aim anywhere, and every swing eases the ground toward that one number until you press
again. Nothing is searched for and nothing is guessed while it is on.

The panel says `HOLDING height, press LeftAlt to release` for as long as it lasts. That wording
is deliberate: the single mistake this feature can cause is forgetting it is on, and a line
that tells you how to stop cannot be read as decoration.

**A held height more than about a metre from the ground will not be reached, and that is
vanilla rather than this mod.** The hoe's flattening eases each point and caps its total
movement at roughly one metre; only raising ground banks that and frees the budget again. So
hold 32m, stand on 34m, and the ground comes down to about 33m and then stops, short of the
number the panel is still showing. Use Raise ground to get within a metre first. Making a held
swing ignore the cap was considered and rejected: it would turn the hoe into a tool that sets
ground to any height in one swing, which is a different mod.

A held height is not kept across a logout. Coming back, swinging, and watching the ground move
toward a number you set yesterday for a reason you no longer remember - with nothing on screen
having changed to warn you - is a worse trap than setting it again.

Left Alt is also vanilla's alt-placement key, which does have a meaning for terrain tools. If
the placement ghost starts behaving oddly while you use this, move `HoldKey` to something of
its own. A server never takes a keybind over, so it stays yours whatever the host runs.

## Reach, and why it is earned

This is Skaft's rule applied to a second tool: Crafting buys reach and never buys a discount.
The curve is the same one, out of the same shared source file, so "8 metres at 60" cannot come
to mean two different things in two mods.

Reach is deliberately the smaller half of this mod. A wider swing is genuinely useful, because
one swing has one reference height and so whatever a single swing covers is flat by
construction. But it does not fix anything on its own. The moment an area needs two swings you
are back to two heights, and the seam between a pair of 6 metre swings is a far worse thing to
repair than the seam between two vanilla ones. Continuing the flat is what makes a big swing a
good idea rather than a dangerous one, which is why it is on at every skill level and the reach
is not.

The reach used is always the larger of the curve and the tool's own radius, so `MinRadius` can
sit at zero and mean "a new character gets exactly the hoe the game shipped". Worth knowing
when tuning: the hoe's own radius is 3 metres, so with the default curve nothing changes at all
until about Crafting 25, and `MaxRadius` of 6 is double the vanilla radius at the top.

## Wards

Vanilla checks one point when you level: the one under your crosshair. It does not check what
the tool actually covers. So even the stock hoe can already cut ground out from under a
neighbour's wall while you stand outside their fence, with the ghost showing blue and the ward
never flashing. That is a small hole at vanilla's radius and a wide one the moment Crafting
makes the tool bigger, so Jafna tests the whole footprint and refuses the swing if any of it
falls inside a ward you have no access to. The hoe's op is round, so the test is a circle; a
square one is padded to its corners instead.

Be clear about what that is worth. It is enforced where you swing, not where the world is
saved. Terrain in Valheim is applied by whichever client owns that zone, and there is no
server-side validation anywhere in that path, so this stops the mod and it stops an honest
player. It cannot stop a hostile one. That was already true of vanilla and this does not make
it worse.

## Installing

Needs BepInEx. Nothing else. Through a mod manager it is one install. By hand, put `Jafna.dll`
in `BepInEx/plugins/Jafna/`.

Then start the game once and quit. That first run writes the config file. It does not exist
before the mod has loaded, which is the usual reason people think it is broken.

Core is optional and soft. With it, Jafna joins the version gate and the host's reach and ward
settings are the ones everybody plays by. Without it the mod works exactly the same for you,
and the ward rule becomes an agreement between players rather than a property of the server.

## Settings

Every setting is in `BepInEx/config/ezomic.valheim.jafna.cfg`, and each one carries its
reasoning in the file rather than here, so there is only one place to keep up to date. The ones
worth knowing exist:

- `ContinueFlat` turns the whole idea off and leaves you with vanilla levelling.
- `MaxRadius` is the radius a swing reaches at Crafting 60. It defaults to 6 metres, against
  the hoe's own 3.
- `ContinueTolerance` is how far apart the nearby shaped ground may be before the mod stops
  trusting it. A smoothed patch is a shallow dish rather than a plateau, so this is looser than
  it looks like it should be.
- `RespectWardFootprint` is the ward rule above.
- `ShowReadout` is the build panel lines.

BepInEx writes every entry to disk on the first run and the saved value beats a later default
in code, so if a setting looks like it is doing nothing, read the cfg before anything else.

## Multiplayer

Terrain is applied by whichever client owns the zone, which is often not the player swinging.

Continuing a flat needs nothing to travel, because the flags it reads are already on that
client's machine. The reach does, and vanilla's terrain message has no room for it, so Jafna
appends the number in a place nothing reads. An owner running Jafna finds it and uses it; an
owner without Jafna never looks and applies a perfectly ordinary vanilla operation. Nothing
breaks in either direction, and a player without the mod is never refused the server.

## What the hoe actually does

Read off the running game on 20 September 2026, because none of it is on disk. The hoe's
Level ground entry is `mud_road_v2`, and it is a **smooth** operation, not a level one:
`m_smooth` true, `m_level` false, radius 3 metres, power 1, painting dirt over the same
3 metres. Raise ground is `raise_v2` and Path is paint only. Nothing in the hoe's table uses
`m_level`.

That matters for what this mod can promise. `SmoothTerrain` eases each point toward the target
by `1 - (distance/radius)^power` and clamps its own accumulated movement to one metre per
point, and only a level operation ever banks that into the permanent height and frees the
budget again. So flattening with the hoe is asymptotic by design and capped near a metre per
point, which is why a real slope still wants Raise ground first. Jafna does not change any of
that. It changes the height all those swings are easing toward, so they converge on one answer
instead of following your crosshair.

**The hoe's piece table names no skill.** Using it raises nothing, and `Player.GetBuildStamina`
skips its discount branch, so unlike the hammer there is no second reward hiding behind the
first - reach here is the only thing Crafting buys. It also means the reach is not self-earning:
you cannot level your way to a wider hoe, the skill comes from crafting and upgrading at a
station. That is the same shape as Skaft, where repairing buildings trains nothing either.

## What has not been tested

The mod has been in a world and its patches run, but nothing below has been watched end to end:
the height actually holding across a row of swings, the ward footprint refusing anything, and
any of it with a second player.

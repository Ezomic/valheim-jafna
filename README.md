# Jafna

Levelling with the hoe continues the flat ground it touches instead of chasing your
crosshair, and your Crafting skill decides how much ground one swing covers.

*Jafna* is Old Norse for to level, to make even.

## Why

Flattening a large area with the hoe is miserable, and it is worth being precise about why,
because the obvious answer is wrong. It is not that the tool is small. It is that the tool has
no fixed idea of what height you are aiming for.

Vanilla's level operation sets every point under the tool to the height of the placement
ghost, and the ghost sits wherever your crosshair last met the ground. So the reference height
follows the camera. Take one swing, walk two steps, take another, and the strip of ground both
swings covered gets levelled twice, to two different heights, and the second one wins. That
difference is permanent. You cannot aim your way out of it, because the thing moving is not
your aim, it is the target. It is also why the ripples get worse the more carefully you work
around the edges: every correction is another swing at another height.

Jafna gives the swing somewhere fixed to level to, and the game turns out to have been keeping
it all along. Every zone's terrain data carries a flag per grid point meaning "a terrain
operation has touched this", saved with the world and shared with everyone. So a swing can
look at the ground it is about to cover, find the parts of it that were levelled before, and
use that height rather than your crosshair's. Flat spreads outward from wherever you started
it, at one height, and keeps spreading in your next session and under another player's hoe.

A swing that covers nothing you levelled before is left completely alone, which is how you
still start a new platform at a new height: stand clear of the old one and swing.

## What it does

- **A level swing continues the flat it touches.** If it covers ground you already levelled,
  it uses that height. If it covers none, it behaves exactly like vanilla.
- **It refuses rather than guesses.** Where two platforms at different heights meet under one
  swing, it hands the decision back to your crosshair instead of averaging them into a ramp.
- **Reach grows with Crafting.** Nothing at all at low levels, up to a 12x12 metre square from
  Crafting 60. Never a discount: a swing costs exactly the stamina vanilla charges.
- **A ward you cannot use blocks the whole swing, not just its centre.**
- **The build panel shows the numbers**: the reach you have, the height the swing will use, and
  where that height came from.
- No new prefabs, items, recipes or saved values. A world played with Jafna is an ordinary
  world, and the ground you shaped is ground vanilla's own operation shaped.

## Reach, and why it is earned

This is Skaft's rule applied to a second tool: Crafting buys reach and never buys a discount.
The curve is the same one, out of the same shared source file, so "8 metres at 60" cannot come
to mean two different things in two mods.

Reach is deliberately the smaller half of this mod. A wider swing is genuinely useful, because
one swing has one reference height and so whatever a single swing covers is flat by
construction. But it does not fix anything on its own. The moment an area needs two swings you
are back to two heights, and the seam between a pair of 12 metre swings is a far worse thing to
repair than the seam between two vanilla ones. Continuing the flat is what makes a big swing a
good idea rather than a dangerous one, which is why it is on at every skill level and the reach
is not.

The reach used is always the larger of the curve and the tool's own radius, so `MinRadius` can
sit at zero and mean "a new character gets exactly the hoe the game shipped".

## Wards

Vanilla checks one point when you level: the one under your crosshair. It does not check what
the tool actually covers. So even the stock hoe can already cut ground out from under a
neighbour's wall while you stand outside their fence, with the ghost showing blue and the ward
never flashing. That is a small hole at vanilla's radius and a wide one the moment Crafting
makes the tool bigger, so Jafna tests the whole square footprint and refuses the swing if any
of it falls inside a ward you have no access to.

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
- `MaxRadius` is how big a swing gets at Crafting 60. It defaults to 6 metres, which is a
  12x12 square.
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

## What has not been tested

Nothing here has been run in a game yet. It compiles and it deploys. Everything above is an
argument from the decompiled source, not a report from a world.

One number in particular is still open: whether the hoe's piece table names Crafting the way
the hammer's does. If it does, then levelling already earns that skill and already gets
vanilla's build stamina discount from it, and this mod extends a rule the game has rather than
inventing one. That value is asset data and cannot be read off disk by any decompiler or prefab
rip, so Jafna logs it once a session when `Verbose` is on, and the answer belongs in this
section once somebody has read it.

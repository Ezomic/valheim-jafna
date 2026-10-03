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
- **Low ground is raised with stone.** When a swing wants the ground higher than the hoe can
  lift it, that swing lifts its whole circle straight to your height and your pack pays for
  what the hoe would not have done, at the rate the hoe's own Raise ground charges. It leaves a
  step at the edge of the circle until you swing next to it. Like Raise ground, it needs a
  workbench nearby.
- **Reach grows with Crafting.** Nothing below about Crafting 25, growing to a 6 metre radius
  at Crafting 60 against the hoe's own 3. Never a discount: a swing costs exactly the stamina
  vanilla charges.
- **Only your own swing is changed.** Ground shaped by anything else, a location, another mod,
  a Devkit flatten or a player without Jafna, is shaped exactly as vanilla shapes it.
- **A ward you cannot use blocks the whole swing, not just its centre.**
- **The build panel shows the numbers**, in twelve fixed rows that never change count or order:
  what the swing does to the ground, the height it will use and where that height came from, and
  what raising the low ground will cost. See The build panel below.
- No new prefabs, items, recipes or saved values. A world played with Jafna is an ordinary
  world, and the ground you shaped is ground vanilla's own operations shaped.

## The build panel

The numbers go in the hoe's build tooltip, under the piece's own description, and the box they
make is always the same size. A tooltip sizes itself to its text, so a readout that grew a
warning over a bad swing and dropped a price over a good one resized the box as the crosshair
moved, and at working speed that cannot be read. So every state writes the same rows, in the
same places, and a row with nothing to say holds a dash:

```
All clear                              the one warning slot, red when it has something to say
GROUND
Raise up to: 1.20m                     the most the ground under the swing comes up
Lower up to: 0.80m                     and the most it goes down
Flattens 6.0m across (Crafting 52)
HEIGHT
Crosshair: 5.40m
This swing: 5.00m
Taken from: flat ground                or your crosshair, or HOLDING and the key that releases it
COST
Stone: you carry 20, needs 14
Workbench: in range
```

The warning slot is replaced in place, never added to, and shows one thing in this order: a ward
you cannot use, ground past the eight metre limit, no workbench near, then too little stone. The
rows below it say the rest. Each row ends with the value that moves as you look around, so
nothing after it shifts. No row is longer than the box, so none wraps onto a second line, and a
row that would be (several kinds of item in a price, a longer language) is cut with `..` rather
than allowed to. `ShowReadout` turns all of it off.

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

The panel's Taken from row says `HOLDING, LeftAlt releases` for as long as it lasts. That wording
is deliberate: the single mistake this feature can cause is forgetting it is on, and a row
that tells you how to stop cannot be read as decoration.

The key counts when you let go of it, and only when you pressed it on its own. Press another key
or a mouse button while it is down, or let the game window lose focus before you let go, and
nothing happens. That is for Alt+Tab. Switching windows starts with Alt going down while the game
still has the focus, and the first version, which acted the moment the key went down, pinned a
height whenever you tabbed out with a levelling tool in your hands, and it stayed on when you came
back. The height held is still taken when you press, not when you let go.

Hold a height above the ground and swinging raises it all the way, for stone, as long as the
height is within eight metres of where the world made the ground. The next section has the
price. Hold one more than about a metre below the ground and the ground still stops short: hold
32m, stand on 34m, and it comes down to about 33m and no further. The hoe only digs a metre, or
two on ground it raised before, and digging is the pickaxe's job.

A held height is not kept across a logout. Coming back, swinging, and watching the ground move
toward a number you set yesterday for a reason you no longer remember - with nothing on screen
having changed to warn you - is a worse trap than setting it again.

Left Alt is also vanilla's alt-placement key, which does have a meaning for terrain tools. If
the placement ghost starts behaving oddly while you use this, move `HoldKey` to something of
its own. A server never takes a keybind over, so it stays yours whatever the host runs.

## Raising low ground, and what it costs

The hoe's Level ground moves each point a metre at most. It eases the point toward the height
the swing is aiming at, keeps a running total, and stops once that total reaches a metre. Only
raising ground clears the total again. So a swing aimed well above the ground used to bring it
up a metre and then do nothing, however often you swung, while the panel kept showing a height
the ground never reached.

Now a swing over ground further below your height than the hoe's metre can reach lifts its whole
circle straight to your height, and you pay for it in stone. Hold 34m while standing on 32m and
swing, and all the ground inside the swing that is below 34m ends at 34m, in that one swing, with
the stone taken from your pack. Ground above your height is eased down by the hoe for free, as it
always was.

That leaves a step at the edge of the circle, as tall as the ground was low there. Your next swing
beside it that needs stone continues the flat and takes the step away. A step of less than a metre
is within what the hoe does for free, so an ordinary swing beside it eases it into the hoe's usual
slope instead. That is the trade, and it was chosen on purpose. The hoe eases ground toward your
height rather than setting it: fully at the middle of a swing, less further out, and not at all at
its edge. The other way to spend the stone was to follow that easing, finishing only the middle in
one swing and creeping outward over the next few. Lifting the whole circle at once is faster, and
the step is the price of it.

A paid swing takes more stone than one that followed the easing, because it does at once what that
took several swings to do, but the ground costs the same in the end. Every point comes up its free
metre first and you pay for the rest of the way, however many swings it takes to get there. Over
ground two metres below your height, a swing at the hoe's own reach takes about 7 Stone, and one 12
metres across about 27.

A swing where the hoe's free metre is enough for every point in it is left exactly as the hoe does
it, easing and all, and costs nothing. The hoe gets all of that ground there on its own over a few
swings, and charging for it would make ordinary flattening cost stone. The flat top comes with
paying, not with the hoe.

A point is left out of the fill in two cases, and then it gets only the hoe's own free, eased
movement and costs nothing. The game never lets ground sit more than eight metres above where the
world made it, so ground further below your height than that cannot ever get there, however much
stone you carry. And when your stone runs out partway, the ground it did not reach stays where the
hoe puts it, which is covered further down.

Neither is left out in silence, because silence is how this started: a height on the panel and
ground that stops short of it with nothing to say why. Ground too far below turns the panel's
top row red before you swing, `Past 8m of the original ground`, and the swing puts
`Too far below to raise` in the middle of the screen. Running out of stone has its
own panel line and message, below.

The price is the hoe's own. Jafna finds the Raise ground entry on the hoe, takes what it costs,
and works out how much ground one Raise ground swing adds to flat ground. Filling costs stone at
that rate per cubic metre. Both numbers come from the running game and go in the log once a
session, so if another mod changes what Raise ground costs, filling follows it.

So does where you may do it. Raising with stone needs a workbench nearby, because Raise ground
does: its entry names the workbench, and the game will not let you swing it further from one than
the workbench's build range, 20 metres for a bench with no extensions, measured from where you
stand. Filling reads the station off the same entry and follows the same rule. Out of range, a
swing that would need stone is the hoe's alone: the ground comes up its free metre, eased the
hoe's way, and no stone is taken. The panel's top row says `Filling needs a Workbench nearby`
and its Workbench row says none near, and the swing puts `Raising needs a Workbench nearby` in the
middle of the screen. A world with NoWorkbench set lifts the rule and so does the nocost command,
both as they do for Raise ground. NoBuildCost does not: Raise ground is free there and still
wants the workbench.

On a slope Raise ground adds less for the same stone, so there filling is cheaper than doing it
by hand. Alternating Raise ground and Level ground by hand can still beat it. Every Raise ground
gives the next Level ground swing its free metre back, and a fill does not. If it did, every
swing would get a free metre and flattening would raise ground for nothing.

Only the part the hoe could never do is charged. Every point gets the metre the hoe would have
given it for free, less whatever earlier swings already used of it, and you pay for the rest of
the way. A point less than three centimetres past its free metre is not raised past it and costs
nothing, so going over ground that is already flat does not eat stone. Stone leaves your pack
whole. When a swing needs part of a stone, a whole one is taken and the rest is kept toward your
next swing until you log out. If the entry you swing costs stone of its own, the fill leaves that
much in your pack for it.

When you run short, the swing spends your stone on the middle of the circle first and works
outward, lifting each point all the way to your height, until the stone runs out. The ground
further out gets only the hoe's own easing and costs nothing, and the middle of the screen says
you ran out. So a short swing leaves a flat patch in the middle with a step round it. The middle
comes first because that is where you aimed, and a patch that grows out from it leaves one clean
edge to carry on from. The first version spread the stone evenly instead, which brought every
point up by the same fraction of what it needed and left not even the middle at your height.

The build panel shows what a swing will cost before you take it, and that is what the swing
takes. When you carry enough it is the price of the whole circle. When you do not, the Stone row
shows what you carry and what all of it would cost in red, and the top row says
`Short of stone, fills only the middle`. When you cannot pay for even the middle, the top row says
`Not enough stone to fill any of it`, and that swing takes nothing.

A world with NoBuildCost set raises for free. The nocost command lifts the limit but still takes
any stone you carry, which is what vanilla's Raise ground does as well.

`AutoRaise` in the config turns this off and gives the one metre ceiling back. On a server the
host decides it.

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

Core is optional and soft. With it, Jafna joins the version gate and the host's reach, ward and
raising settings are the ones everybody plays by. Without it the mod works exactly the same for you,
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
- `AutoRaise` is raising low ground for stone. On by default.
- `ShowReadout` is the build panel lines.

BepInEx writes every entry to disk on the first run and the saved value beats a later default
in code, so if a setting looks like it is doing nothing, read the cfg before anything else.

## Multiplayer

Terrain is applied by whichever client owns the zone, which is often not the player swinging.

The flags a continued flat reads are already on that client's machine. The reach is not, and
vanilla's terrain message has no room for it, so Jafna appends the number in a place nothing
reads. An owner running Jafna finds it and uses it; an owner without Jafna never looks and
applies a perfectly ordinary vanilla operation. Nothing breaks in either direction, and a player
without the mod is never refused the server.

That appended note is also how the owner knows the operation is a swing at all. By the time a
terrain operation reaches the owner, a hoe swing looks exactly like a location shaping its own
ground, another mod's operation or a Devkit flatten. So your client adds the note to every swing
you take, at the hoe's own width too, and to nothing else, and the owner reshapes only what
carries it. Everything else it applies as vanilla, a swing from a player without Jafna included.
A client on 1.0.0 added the note only to a swing wider than the hoe's own or aimed at a held
height, so an owner on this version no longer continues the flat under that client's other swings.

Raising ground for stone splits the same way. Your client works out whether the swing needs stone
and how far out from the middle of the swing your pack pays for, and sends that distance along
with the reach. The owner lifts every low point inside it to your height, tells your client what
it raised, and your client takes the stone. A distance rather than an amount of stone, so that a
swing across a zone line stops at the same place on both sides of it, and the owner of each zone
takes your client's word that the swing needs stone rather than deciding from its own half, so a
paid swing has one flat top and not a flat half beside an eased one. Paying afterwards is on
purpose: an owner without this version raises nothing and sends no bill, so you pay nothing for
ground that never moved.

Whether you are near a workbench is also your client's answer, since only your client knows
where you stand. When you are not, your client sends no distance at all, so the owner is never
asked to raise anything for that swing and no bill can come back.

When somebody else owns the zone, the stone for a swing leaves your pack once their machine
answers. Two quick swings can therefore be billed for more stone than you held. The difference
is owed, and it comes out of the stone you carry at your next fill.

Your view of that ground can also be a swing behind. A quick second swing may still see ground
the first one already raised, so it raises less of the patch than your stone would pay for and
says you ran short while you still have stone. It can also take a swing for a paid one when the
owner's ground is already close enough for the hoe alone; the owner then lifts only what it finds
still low, and bills only that. Either way you pay for ground that really moved, and the next
swing finishes the patch.

## What the hoe actually does

Read off the running game on 20 September 2026, because none of it is on disk. The hoe's
Level ground entry is `mud_road_v2`, and it is a **smooth** operation, not a level one:
`m_smooth` true, `m_level` false, radius 3 metres, power 1, painting dirt over the same
3 metres. Raise ground is `raise_v2` and Path is paint only. Nothing in the hoe's table uses
`m_level`.

That matters for what this mod can promise. `SmoothTerrain` eases each point toward the target
by `1 - (distance/radius)^power` and clamps its own accumulated movement to one metre per
point, and only a level or raise operation ever banks that into the permanent height and frees
the budget again. So flattening with the hoe is asymptotic by design and capped near a metre per
point. Jafna leaves the easing alone on every swing the cap would not stop short. It changes the
height all those swings are easing toward, so they converge on one answer instead of following
your crosshair. Going up, a swing over ground the cap would stop short is finished for stone:
every point of the circle below the height comes up what is left of its free metre, counted where
vanilla counts it, and the rest of the way goes into the same permanent height Raise ground writes
to. So a swing never gets a fresh free metre out of being filled.

**The hoe's piece table names no skill.** Using it raises nothing, and `Player.GetBuildStamina`
skips its discount branch, so unlike the hammer there is no second reward hiding behind the
first - reach here is the only thing Crafting buys. It also means the reach is not self-earning:
you cannot level your way to a wider hoe, the skill comes from crafting and upgrading at a
station. That is the same shape as Skaft, where repairing buildings trains nothing either.

## What has not been tested

Nothing has been run with a second player: the reach and the bill travelling between two
machines, and an owner leaving a swing from a player without Jafna as vanilla. The ward footprint
has never refused anything in a test either, because there was no ward to refuse it.

Raising low ground for stone went through a scenario in singleplayer on 29 September 2026. That
covers a paid swing's whole circle landing on the height with a step at its edge, the stone it
takes and each point's free metre coming off the bill, the middle coming up first when the stone
runs short, and the workbench rule and its two messages. Not watched yet: the panel's price before
a swing, ground past the eight metre limit being left alone and said so, and leftover stone
carrying over to the next swing.

The hold key ignoring Alt+Tab was checked by hand the same day. A chord pressed with it spoiling
the tap was not.

The same scenario checks that a Devkit flatten beside a flat at another height levels to its own
height, and it passes. Nothing checks a held height or a Crafting reach staying off an operation
that is not a swing.

## Bugs and ideas

Both go to the site. [longhouse.thijssensoftware.nl/bugs](https://longhouse.thijssensoftware.nl/bugs)
is for anything broken, and [longhouse.thijssensoftware.nl/ideas](https://longhouse.thijssensoftware.nl/ideas)
is for what a mod should do next. You can vote on other people's ideas there as well.

Signing in takes a Steam or Discord account. I work from that list, so the votes decide what
I pick up next.


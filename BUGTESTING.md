# Psychic Augmentics bugtesting release

## On-paper gate

Run from the mod root before every test build:

```bash
python3 Tools/validate_release.py
git diff --check
```

Both commands must exit 0. The validator builds Release and checks XML, defs,
custom types, translations, owned texture paths/PNGs, metadata, and the DLL.

## Test profile and log gate

Use an ordinary Steam-launched RimWorld 1.6 game. Minimal order:

1. Core and official expansions
2. Harmony
3. Vanilla Expanded Framework
4. Vanilla Psycasts Expanded
5. Psychic Augmentics

At the main menu, verify `Player.log` has no `PsychicAugmentics` type-load or
static-constructor exception, no missing `PA_` Def, and no missing `PA/`
texture. Inspect every item/building icon for real art rather than pink or
invisible graphics. The old `-nographics` run also lost vanilla textures and
is not a valid visual test.

## Cannon and scaling

- Craft/install/remove the arm cannon on a normal pawn and a psycaster.
- Confirm the built-in verb appears, fires the default bolt, respects range
  and line of sight, and leaves the normal weapon slot usable.
- Compare the health tooltip and observed verb at suppressed, 100%, high, and
  extreme psychic sensitivity. Damage, burst, interval, aim, cooldown, and all
  four accuracies must follow the displayed values without affecting another
  cannon bearer's values.
- Ground/open a faraday cage. Confirm the sensitivity stat is absolutely
  capped at 15% only while grounded, the explanation appears, hostile psychic
  effects are suppressed accordingly, and the toggle survives save/reload.

## Core socket and persistence

For every available core, install it with and without a cannon. Without a
cannon it must remain unregulated (pain and consciousness penalty); with a
cannon it must attune and change the projectile/effect. Removing the cannon
and reinstalling it must switch regulation within 150 ticks.

Install a second, different core. Exactly one old sphere must be ejected and
exactly one core hediff must remain. Repeat while drafted, downed, and after a
save/reload. Confirm surgery failure never silently duplicates or destroys a
core.

## Core effect matrix

- Tox: living targets receive one refreshed three-second DoT; repeated hits do
  not stack hediffs; large bodies take proportionally more ticks; death never
  loses the corpse. Start a new game in the same RimWorld process and confirm
  no queued damage references a pawn from the previous game.
- Heat: only the shooter's current entropy is drained, never below zero; the
  target takes proportional burn damage; a zero-entropy shooter still fires.
- Lightning: main damage plus scaling EMP; verify mechs and shields react.
- Water: heals only fresh, naturally healable flesh injuries. Scars, disease,
  mechs, and buildings remain unchanged.
- Mercury: heals non-flesh pawns and repairs damaged buildings at the stated
  multiplier; living flesh remains unchanged.
- Arcane: projectile stays the default bolt while remaining miss chance is
  reduced; it must not mutate shared accuracy for another pawn.
- Chaos: explosion radius, fire chance, friendly fire, and terrain ignition.
- Cryo: severity stacks to its cap, stage penalties change, then decay away.
- Void (Anomaly): only loads with Anomaly; mutations select valid remaining
  parts and never throw when the target lacks the requested part.
- Vacuum (Odyssey): only loads with Odyssey; damage and secondary vacuum burn
  apply, light targets move farther than large ones, and blocked/map-edge
  pushes remain legal.

## Auto-mend and buildings

- With Doctor work enabled and auto-mend on, water seeks injured faction
  humans/animals and mercury seeks colony mechs. They must not target self,
  enemies, permanent scars, healed pawns, or buildings automatically.
- Toggle auto-mend off/on, swap cores, and save/reload. The setting belongs to
  the cannon and must survive core changes. A wandering target can be chased
  but the job must eventually terminate rather than loop forever.
- Use the vision inducer manually and through recreation. Verify power/path
  rejection, recreation gain, one-day pleasant memory, daily inspiration
  cooldown, and valid inspiration selection.
- Use the trauma engine. Verify power/path rejection, subject and witness
  memories, stacking limits/durations, both success and failure messaging,
  and exactly one psylink level on success.

## Compatibility combinations

Start with neither optional DLC folder active where possible, Anomaly only,
Odyssey only, and both. Also test with RimWorld of Magic absent and present;
the optional magicyte cost must appear only when its Def exists. No conditional
core, recipe, projectile, damage Def, or ingredient may leak into an invalid
combination.

## Bug report minimum

Include RimWorld version, complete mod order, reproduction steps, pawn psychic
sensitivity and installed hediffs, target kind/body size, the full `Player.log`,
and a save immediately before the fault when possible.

Placeholder core/cannon art and balance tuning are expected bugtesting topics.
Red errors, missing assets, save corruption, duplicated/lost implants, stuck
jobs, and cross-pawn scaling leakage are release blockers.

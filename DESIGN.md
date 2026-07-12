# Psychic Augmentics — Design

Transhumanist eltex implant mod. Author: **Nidelese**. Fully independent of
Deepcaller; hard-depends on Vanilla Psycasts Expanded (for `VPE_Eltex`).

## Core fantasy

Channel psychic sensitivity into built-in weaponry. All scaling is an
**uncapped power law** — `stat × sensitivity^exponent`, exponent < 1:
diminishing returns per point, no ceiling. Ridiculous DPS is reachable, but
only at ridiculous sensitivity. Damage and burst count carry growth at
extremes (fire-rate ticks floor at 1, accuracy asymptotes below 100%).

## Architecture

- **Eltex arm cannon** (`PA_ImplantEltexArmCannon` → `PA_Hediff_EltexArmCannon`,
  shoulder): the chassis. Grants `Verb_EltexCannon`, a built-in ranged verb
  whose burst/fire-rate/aim/cooldown/accuracy scale with sensitivity
  (per-stat exponents in the hediff XML). Damage scaling lives on
  `PA_EltexDamageDef` (`sensitivityExponent`), NOT the verb — cores swap
  damage identity without touching the verb.
- **Cores** (spheres, fabricator, ~6 eltex each): implants seated at the
  **Heart**. `Hediff_EltexCore` + `PA_CoreExtension` (requiresCannon,
  projectileOverride, missConversion). Attuned (severity 1) while the bearer
  has the cannon; **unregulated** (severity 0.1: pain 0.35, consciousness
  −15%) without it. One core at a time — installing another ejects the old
  sphere onto the ground. Both cannon and core must be built and installed.
  Core installation runs at 0.9x surgery success chance (heart-adjacent,
  riskier than a standard implant); skill floor stays vanilla Medicine 5 —
  setup quality is the real gate, per vanilla surgery balance.
- Verb picks the attuned core's projectile; no core = plain eltex bolt.
- `DamageWorker_EltexBolt` scales the main hit and supports generic
  `extraDamage` (EMP, VacuumBurn). Core-specific workers subclass it.
- DoT damage is **queued through `PA_GameComponent`**, never dealt inside a
  hediff tick (Deepcaller lesson: victim can die mid-hediff-iteration).

## Cores

| Core | Effect | Key numbers |
|---|---|---|
| Tox | Refreshed (non-stacking) 3s DoT, damage scales with body size ("% max HP" analogue — melts big bosses) | 2 dmg/s/bodysize |
| Heat | Vents SHOOTER's psychic entropy into target as Burn — psycaster pressure valve | 8 entropy/hit, ×0.75 burn |
| Lightning | EMP burst on hit (mild scaling) | 12 EMP, exp 0.35 |
| Water of Tranquility | Heals fresh injuries on living targets; strays harmless. **Auto-mend**: proactively heals injured colonists/animals | 12 heal/bolt, scaled |
| Mercury | Mends mech injuries, repairs buildings ×3. **Auto-mend**: proactively heals colony mechs; buildings stay manual-target-only | 12/bolt, scaled |

**Auto-mend** (`WorkGiver_EltexAutoMend`, Doctor work, priority 60 — below
real tending/feeding): bearers of a mending core shoot damaged faction pawns
better as regular work. Routing is data-driven off `PA_MendDamageDef` flags
(`healFlesh` → flesh, `healNonFleshPawns` → mechs; `repairBuildings` is
deliberately never automated), so future mending cores inherit it. Toggled
per-pawn via a gizmo (`HediffComp_AutoMendToggle` on the cannon hediff, so
the setting survives core swaps; icon = attuned core's sphere).
| Arcane | Converts 25% of remaining miss chance into hits (implemented as accuracy math, not true homing) | missConversion 0.25 |
| Chaos | Explosive bolts, ignites terrain, friendly fire is the balance | r1.5, 85% fire |
| Cryo | Stacking fast-decay slow (move/manip), capped by maxSeverity | 0.12/hit, cap 1.0, −35/day |
| Void-shard | **Anomaly only** (LoadFolders): 15% chance/hit of random mutation (Tentacle, FleshWhip, Fleshmass organs, PsychicInjury) | weighted XML table |
| Vacuum | **Odyssey only**: VacuumBurn extra + knockback ÷ bodySize (bosses can't be displacement-locked) | 2.5 cells / bodysize |

Each core also costs a **flavor ingredient** on top of the shared base
(6 eltex / 10 plasteel / 2 spacer components). Child `costList` entries merge
with the abstract parent's, so only the extra item is declared. Cross-mod /
DLC items use `MayRequire` on the costList entry — the ingredient silently
drops out when the mod isn't loaded:

- Tox: 5× wastepack (`Wastepack`, MayRequire Biotech — bonus: waste sink)
- Heat: 1× psylink neuroformer (defName `PsychicAmplifier` — legacy 1.2 name!);
  MarketValue bumped to 3600 so the core outvalues its rarest part
- Lightning: 1× EMP grenade (`Weapon_GrenadeEMP`)
- Water of Tranquility: 20× jade
- Mercury: 2× luciferium (mechanites retrained to mend machines)
- Arcane: 20× raw magicyte (`RawMagicyte`, MayRequire Torann.ARimworldOfMagic)
- Chaos: 1× high-explosive shell (`Shell_HighExplosive`)
- Cryo: 10× neutroamine (coolant bath; trade-only sourcing is the cost)
- Void-shard: 1× shard (Anomaly folder, no MayRequire needed)
- Vacuum: 4× gravlite panel (Odyssey folder)

## Other implants

| Implant | Effect | Key numbers |
|---|---|---|
| Eltex faraday cage | Sternum implant, toggle gizmo: grounded = PsychicSensitivity CAPPED at absolute 15% via StatPart injected into the vanilla stat (multiplicative factors fail at VPE's 90,000% scale — Evelyn's catch). Mutes drones/lances/hostile psycasts totally; cripples own cannon & psycasting while closed. Foil helmet comparison irrelevant (trade-only, ultra-rare). | cap 0.15, XML-tunable; severity 0.1 open / 1 grounded |

## Buildings

| Building | Effect | Key numbers |
|---|---|---|
| Psychic trauma engine (Misc tab) | Right-click with colonist: 2h session of engineered hallucinations for a CHANCE at +1 psylink. Subject: stacking -8 mood, 6 days, 5 stacks max. All other colonists on map: flat -5 "a colonist was tortured", 4 days, non-stacking. Dystopian trauma-forging. | 25% psylink chance & 5000-tick session, XML-tunable via PA_TraumaEngineExtension; 200W, 150 steel + 4 components + 5 eltex |
| Psychic vision inducer (Joy tab) | The trauma engine with the safety limiters left in: pawns visit on their own as recreation (JoyGiver, Meditative joy, interaction cell south) or by right-click. 1h session refills recreation, leaves "+6 pleasant visions" (1 day, non-stacking), and a completed session can crystallize a random weighted inspiration. The afterglow memory doubles as the inspiration cooldown — no reroll while it lingers (~1 roll/pawn/day max). | 15% inspiration chance & 2500-tick session via PA_VisionInducerExtension; 150W, 120 steel + 3 components + 4 eltex |

**Research branch**: `PA_EltexAugmentics` (3000) → `PA_VisionInduction`
(1200, unlocks the inducer) → `PA_TraumaticAwakening` (2000, unlocks the
trauma engine). You learn to project visions before you weaponize them;
the dark machine is the deepest node. Mid-save note: newly added nodes
load unresearched even after a prior "research all".

## Art

Procedural placeholder icons (PIL script): shaded spheres per core color,
blocky cannon. Real art pass later (ComfyUI).

## Status

- [x] Cannon + sensitivity scaling (playtest pending)
- [x] Core socket system + all 10 cores (playtest pending)
- [ ] In-game playtest: cannon solo, each core, unregulated harm, core swap ejection
- [ ] Balance pass after playtest (exponents, costs, DoT/EMP numbers)
- [ ] Art pass
- [x] Faraday cage implant (playtest pending)
- [x] Psychic trauma engine building (playtest pending)
- [ ] Future: passive implants, more elements

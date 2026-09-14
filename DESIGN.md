# Rune Arena (符文竞技场) — Prototype Spec v0.1

This is the single source of truth for the prototype. Every agent working on this project codes against this document. If something is not specified, pick the simplest option that keeps the game feeling smooth and note it in a code comment.

## 0. Goal

A playable Unity prototype of a round-based team arena brawler (inspired by LoL Arena / Wild Rift 符文大乱斗) with **three distinct gacha-style mechanics**:

1. **Rune Draft (符文抽选)** — between rounds, draw 3 rune cards, pick 1. Rarity weights, pity, comeback bonus, set synergies.
2. **Item Shop (装备刷新商店)** — between rounds, a shop with 5 random items; buy or reroll for gold. Tier unlocks by round.
3. **Treasure Chests (宝箱抽卡)** — chests spawn in the arena during combat; opening one gives a random reward with rarity weights and pity.

Human player + bots (same code for both). Multiplayer networking is out of scope for v0.1 but nothing may assume a single local player: all logic goes through `Unit` and services, never through "the player" singleton, except `PlayerInput`/`PlayerCamera`.

**Feel pillar: smooth.** Responsive movement (no acceleration lag), cast-while-moving, short windups, input buffering, hit-stop, camera shake, hit flash, damage numbers, clear telegraphs, cooldown pings.

## 1. Tech constraints (hard rules)

- Unity **6000.4.5f1**, **Built-in Render Pipeline**, **legacy Input Manager** (`UnityEngine.Input.GetKey`, `Input.mousePosition`, `Input.GetMouseButton`). Do NOT use the Input System package, TextMeshPro, URP, NavMesh, Timeline, or any third-party package.
- UI is uGUI (`UnityEngine.UI`) with legacy `Text`. Font: `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`.
- **Zero hand-authored assets.** Arena, heroes, VFX, UI, audio are all created in code from primitives (`GameObject.CreatePrimitive`), `LineRenderer`/`TrailRenderer`, `ParticleSystem` configured in code, `AudioClip.Create`, and uGUI built by `UiFactory`. Materials: `new Material(Shader.Find("Standard"))` or `"Sprites/Default"`/`"UI/Default"` for UI. Never reference an asset path.
- The game must run from **any scene, including an empty untitled scene**: a static `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]` in `Bootstrap` creates the `GameRoot` object if none exists. It must reuse an existing `Camera.main` if present, else create one. It must create a directional light if none exists.
- C# language level as supported by Unity 6 (C# 9): records, `init`, switch expressions, target-typed `new` are fine. No `record struct`? (C# 10) — avoid. No file-scoped namespaces (C# 10) — avoid.
- Assembly definitions: `Assets/Scripts/RuneArena.Runtime.asmdef` (root namespace `RuneArena`), `Assets/Editor/RuneArena.Editor.asmdef` (references Runtime, editor-only), `Assets/Tests/EditMode/RuneArena.Tests.EditMode.asmdef`, `Assets/Tests/PlayMode/RuneArena.Tests.PlayMode.asmdef` (both reference Runtime + `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, with `nunit.framework.dll` precompiled reference).
- Style: files ≤ 400 lines, methods ≤ 50 lines, nesting ≤ 4. Explicit null checks at boundaries. No `try/catch` swallowing. No `UnityEngine.Random` in gameplay logic (use `Rng`); `UnityEngine.Random` is allowed only for purely cosmetic VFX jitter.
- Definitions (heroes, skills, runes, items, loot tables) are **immutable** C# classes with `init` properties (or records) constructed in `ContentCatalog`. Runtime state (`Unit`, services) may mutate itself, but stat modifiers, damage info, and definitions are immutable values.
- No allocations in per-frame hot paths where avoidable (cache lists, reuse buffers). Unit lookups go through `CombatWorld` (a registry), never `FindObjectsOfType` at runtime.
- Time: gameplay uses `Time.deltaTime` (scaled). Hit-stop manipulates `Time.timeScale` briefly; UI timers that must keep running during hit-stop use `Time.unscaledDeltaTime`.

## 2. Match flow

- Teams: `Team.Blue` (contains the human player at index 0 + bots) vs `Team.Red` (bots). `MatchConfig.TeamSize` default **2** (2v2); supports 1–3.
- Match = up to 5 rounds; first team to **3 round wins**.
- Round length **75 s**. Round ends immediately when one team is fully dead. On timeout: team with higher round points wins (`points = kills*100 + captureSecondsOfControlPoint*10`); tie → team with higher total current HP% wins; still tie → Red wins (arbitrary, documented).
- Within a round, dead units stay dead (no respawn). Between rounds, everyone is fully healed, statuses cleared, respawned at their base.
- `MatchPhase` state machine (in `MatchController`):
  `MainMenu → HeroSelect → RuneDraft → Shop → Countdown → Combat → RoundEnd → (RuneDraft → Shop → Countdown → Combat → RoundEnd)* → MatchEnd`
  - RuneDraft: 12 s or until every unit has picked (bots pick after 1–3 s delay).
  - Shop: 12 s or until player presses Ready (bots buy instantly using their scoring).
  - Countdown: 3 s, units can't move.
  - RoundEnd: 3 s banner.
  - MatchEnd: results panel with Rematch (same config, new seed) and Main Menu.
- Control point at arena center, radius 3.0. A team captures while ≥1 of its living units is inside and no enemy living unit is inside; progress 0→100 at 20/s; contested = paused; empty = decays 10/s. Reaching 100 for team T: every unit of T gets +150 gold, a Chest spawns at the point, progress resets to 0, and a 10 s lockout starts.
- Chests also spawn at random walkable positions every 20 s of combat (max 2 alive at once, spawn only if fewer than 2).
- Economy (`Economy` service): starting gold 500; passive +4/s during Combat; kill +300 to killer, +150 to each other living ally within 10 units (assist); round win +400 per unit, round loss +250 per unit; control point capture +150.

## 3. Player controls (PlayerInput)

- WASD: 8-direction movement at `MoveSpeed` units/s, instant start/stop (tiny smoothing ≤ 0.05 s for visual only). Movement is world-axis aligned (W = +Z, D = +X).
- Mouse: hero faces the cursor projected onto the ground plane (y=0) via `Camera.main.ScreenPointToRay`.
- LMB held: basic attack toward cursor at `AttackSpeed` attacks/s.
- Q / W / E: skills 1–3. R: ultimate. All skills are **quick-cast** toward cursor direction / cursor ground point. No click-to-confirm.
- Esc: pause menu.
- Enter/Space in Shop: Ready. In Draft: number keys 1/2/3 pick a card (also clickable).
- **Cast while moving**: during windup and recovery the unit still moves at 60% speed, except `Dash` shape (movement controlled by dash) and `Channel` skills (rooted).
- **Input buffering**: a skill press during another skill's windup/recovery is queued (buffer window 0.25 s) and fires when the current cast finishes. Dash skills can interrupt basic-attack windups.
- Cast anatomy: `Windup` (0.05–0.25 s, telegraph visible) → effect resolves → `Recovery` (0.1–0.3 s). Cooldown starts at the effect moment.

## 4. Stats

`StatType`: `MaxHealth, HealthRegen, AttackDamage, AbilityPower, AttackSpeed, MoveSpeed, Armor, CooldownReduction, Lifesteal, CritChance, CritDamage, AttackRange`.

- Final value = `(base + Σflat) * (1 + Σpercent)`. `CooldownReduction` is clamped to 0.40; `CritChance` to 1.0.
- Damage reduction from armor: `dmg * 100 / (100 + armor)`, applies to Physical and Magical. `True` damage ignores armor and shields? — no: True damage ignores armor only; shields still absorb it.
- Shields absorb before HP. Shields have a duration; multiple shields stack as a list.
- Lifesteal applies to basic-attack damage only. Crit applies to basic attacks only (`CritDamage` default 1.75 multiplier).
- **No mana.** Skills are gated by cooldown only.

## 5. Heroes (3)

All heroes: capsule body (radius 0.5, height 2), colored per hero, with a small "nose" cube to show facing, overhead HP bar. Team tint: Blue team gets a blue ring under the unit, Red gets a red ring.

Skill definition fields: `Id, Name, Key(Q/W/E/R/Basic), Shape, Windup, Recovery, Cooldown, Range, Radius/Angle, Speed (projectiles/dashes), Delay (ground AoE), Damage (base + scaling: `AdRatio`, `ApRatio`), DamageType, Effects[] (Knockback(force), Stun(dur), Slow(pct,dur), Shield(amount,dur), Heal, Invisible(dur), DamageReduction(pct,dur), SpeedBoost(pct,dur), Pull), AiHint (Engage/Poke/Escape/Burst/Defensive/Ult)`.

`SkillShape`: `Projectile` (single bolt from caster toward aim dir, hits first enemy, `Range` = max travel), `Fan` (N projectiles spread over `Angle`), `Cone` (instant hit all enemies in cone `Angle` within `Range`), `Circle` (ground-targeted at cursor point clamped to `Range`, telegraph disc, resolves after `Delay`), `SelfCircle` (instant AoE around caster radius `Radius`), `Dash` (move caster `Range` units toward aim over `Range/Speed` s; hits enemies passed through), `Blink` (teleport `Range` toward aim), `Buff` (applies effects to self), `Channel` (rooted, ticks `SelfCircle` damage every 0.25 s for `Delay` seconds), `Melee` (instant arc `Angle` within `Range`, used for basic attacks of melee heroes).

### Hero 1 — Blaze 烈焰 (Ranged Mage). Color orange.
Base: HP 520 (+regen 1.5/s), AD 45, AP 60, AS 1.0, MS 6.0, Armor 20, AttackRange 7.
- Basic: `Projectile` bolt, speed 22, dmg 100% AD, windup 0.12, recovery 0.1, magical? no — Physical.
- Q Flame Wave: `Cone` angle 70°, range 5, dmg 70 + 60% AP, Magical, windup 0.15, cd 4.
- W Blink: `Blink` range 5, cd 9, grants 0.2 s invulnerability (`DamageReduction 100%, 0.2s`) and SpeedBoost 30% 1.5 s.
- E Meteor: `Circle` range 9, radius 2.2, delay 0.8, dmg 140 + 80% AP, Magical, Slow 40% 1.5 s, cd 8.
- R Inferno: `Channel` 2.5 s, radius 4, tick dmg 30 + 25% AP per 0.25 s (Magical), self DamageReduction 30% during channel, cd 45, windup 0.2.

### Hero 2 — Vanguard 铁壁 (Melee Bruiser). Color steel blue.
Base: HP 780 (+regen 3/s), AD 62, AP 0, AS 0.9, MS 5.6, Armor 40, AttackRange 2.2.
- Basic: `Melee` arc 90°, range 2.2, dmg 100% AD, windup 0.1, recovery 0.15.
- Q Charge: `Dash` range 6, speed 20, dmg 60 + 70% AD Physical, Knockback 3, cd 9. AiHint Engage.
- W Shield Bash: `Cone` angle 60°, range 2.5, dmg 50 + 60% AD, Stun 0.8 s, windup 0.2, cd 8.
- E Iron Skin: `Buff` Shield (120 + 20% MaxHealth for 3 s) + DamageReduction 20% 3 s, cd 14. AiHint Defensive.
- R Earthquake: `SelfCircle` radius 4.5, dmg 180 + 100% AD Physical, Slow 50% 2 s, Knockback 2, windup 0.35 (telegraph), cd 50.

### Hero 3 — Shade 影刃 (Melee Assassin). Color purple.
Base: HP 560 (+regen 2/s), AD 70, AP 0, AS 1.3, MS 6.4, Armor 25, AttackRange 1.8, CritChance 0.15.
- Basic: `Melee` arc 70°, range 1.8, dmg 100% AD, windup 0.06, recovery 0.08.
- Q Shadow Step: `Dash` range 5.5, speed 26, dmg 50 + 80% AD Physical; enemies hit from behind (dot(targetFacing, dir) > 0.3) take +50% (backstab), cd 7. AiHint Engage/Escape.
- W Fan of Knives: `Fan` 3 projectiles over 30°, speed 24, range 8, each 40 + 45% AD Physical, cd 6. AiHint Poke.
- E Smoke: `Buff` Invisible 2.5 s (bots lose target; renderers alpha 0.3 for own team, hidden for enemies) + SpeedBoost 40% 2.5 s, cd 16. Attacking breaks invisibility. AiHint Escape.
- R Execute: `Melee` arc 60°, range 2.5, dmg 150 + 120% AD True; if target HP < 35% damage ×2, cd 40. AiHint Burst.

## 6. Runes (符文)

`RuneRarity`: Common, Rare, Epic, Legendary. Base draft weights: 60 / 28 / 10 / 2. **Comeback**: if a unit's team is behind in round wins, weights become 40 / 35 / 20 / 5. **Pity**: if a unit has seen no Epic+ card in its last 3 drafts, the next draft guarantees at least one Epic+ card. Draft shows **3 distinct cards**; unique (non-stackable) runes already owned are excluded; stackable stat runes can be owned up to 3 times.

`RuneSet`: Ember 烈焰, Iron 钢铁, Shadow 暗影, Storm 风暴, None. Owning 3 runes of one set triggers its set bonus once (not per rune):
- Ember: skills apply Burn (3% max HP over 2 s, Magical).
- Iron: +15% MaxHealth, +20 Armor.
- Shadow: basic attacks from behind deal +25%.
- Storm: dashes/blinks reset 1 s of all cooldowns; +10% MoveSpeed.

Catalog must have **≥ 30 runes**: ~12 stat runes (stackable, Common/Rare; each belongs to a set), ~14 mechanic runes (unique, Rare/Epic), ~4 Legendary transform runes. Mechanic runes are implemented as `ICombatHook` subscribing to `EventBus` events. Examples the catalog must include (names may be refined):
- Common: 烈焰之心 +40 AP (Ember), 铁骨 +80 HP (Iron), 疾风 +6% MS (Storm), 锋刃 +12 AD (Shadow), 迅捷 +12% AS (Storm), 护甲 +15 Armor (Iron), 吸血 +6% Lifesteal (Shadow), 精准 +8% Crit (Shadow), 冷却 +6% CDR (Storm), 再生 +2.5 HP regen (Iron), 法力涌动 +12% AP (Ember), 巨人 +8% MaxHealth (Iron).
- Rare/Epic mechanics: 三连击 (every 3rd basic attack +60% damage), 燃烧之触 (skills apply burn), 冲刺护盾 (dashes grant shield 10% max HP), 猎杀 (kills restore 20% HP), 反击 (taking damage stores 8%; next skill deals stored bonus), 荆棘 (reflect 12% of basic-attack damage taken), 破釜沉舟 (below 30% HP: +25% AD/AP), 战争狂热 (each basic attack hit: +8% AS for 3 s, stacks 5), 冰霜 (basic attacks slow 20% 1 s), 回响 (skill hits deal +30 Magical bonus, 3 s cooldown), 迅影 (kill: +40% MS 3 s), 先手 (first skill each round deals +40%), 坚韧 (stun/slow durations −40%), 复苏 (round start: shield 150 for 5 s).
- Legendary transforms: 双重施法 (skills Q/W/E get 2 charges), 烈焰足迹 (dashes/blinks leave a fire trail 3 s dealing 20+20%AP per 0.25 s), 分裂弹 (basic-attack projectiles/melee also fire 2 extra bolts at 30% damage), 不死 (once per round, survive lethal damage at 1 HP and gain 3 s of 60% DR).

`RuneDefinition`: `Id, Name, Description, Rarity, Set, Stackable, MaxStacks, StatModifiers[], HookFactory (Func<ICombatHook> or null)`.

## 7. Items (装备)

`ItemTier` 1/2/3. Shop shows 5 random items; tier T is available from round T (round 1: T1 only; round 2: T1+T2; round 3+: all). Weights within the available pool: T1 60, T2 30, T3 10 (only among available tiers, renormalised). Reroll costs 100, +50 per additional reroll in the same shop phase. **6 inventory slots**. Sell = 70% refund. Same item can be owned only once (unique).

Catalog must have **≥ 16 items**, e.g.: T1 (400–600 gold): 长剑 +20 AD; 法杖 +35 AP; 皮甲 +25 Armor; 红宝石 +150 HP; 靴子 +10% MS; 短剑 +15% AS; 吸血刀 +8% Lifesteal; T2 (900–1200): 无尽之刃 +40 AD +20% Crit; 冰霜之锤 +25 AD +200 HP, basic attacks slow 25% 1.5 s; 荆棘甲 +60 Armor, reflect 15% of damage taken as Magical; 血饮 +45 AD +12% Lifesteal, on kill shield 100; 幽梦 +30 AD, on kill +30% MS 3 s; 灭世 +80 AP +10% CDR; T3 (1800–2200): 守护天使 +30 AD +30 Armor, once per round revive with 40% HP after 2 s; 死刑宣告 +50 AD, skills deal +8% of target's missing HP as True; 大天使 +120 AP, skill hits heal 5% dealt; 魔女之爪 +90 AP, skills apply burn 5% max HP over 3 s.

`ItemDefinition`: `Id, Name, Description, Tier, Cost, StatModifiers[], HookFactory (nullable)`.

## 8. Chests (宝箱抽卡)

- Chest: small cube (0.8) with rotating/bobbing animation, gold-colored. Opening: a living unit stays within 1.2 units for 1.0 s (progress ring shown); moving out cancels.
- Reward table (weights): Gold 200 (40), Common rune (30), Rare item (20, random T1/T2 item not owned; inventory full → gold instead), Epic rune (8), Legendary item (2, random T3 item). Pity: every 4th chest opened by the same unit guarantees Epic rune or Legendary item.
- Reveal: HUD shows a "card flip" (a UI panel scaling from x=0→1 with rarity color) for 1.5 s, gameplay is not paused.

## 9. Bots (AI)

FSM per bot, evaluated every 0.1 s (not every frame):
- `Seek`: move toward nearest living enemy (or control point if none visible). Ranged heroes stop at `AttackRange*0.85` and strafe.
- `Fight`: face target, basic attack when in range; cast skills by `AiHint` rules: Engage when target dist > attackRange and dash available; Poke/Burst when in range; Defensive/Escape when HP < 35% and an enemy is within 4 units; Ult when target HP < 50% or ≥2 enemies within radius.
- `Retreat`: HP < 30% and no defensive skill ready → move away from nearest enemy toward own base for 2 s, then re-evaluate.
- `Capture`: if no enemy within 8 units and control point not owned → go to the point.
- `Loot`: if a chest is within 12 units and no enemy within 6 → go open it.
- Aim error: ±8° random offset; reaction delay 0.25 s before switching targets.
- Draft/Shop decisions: score a rune/item as `Σ statWeight[hero.Archetype][stat] * value` + rarity bonus; mechanics get a fixed score by archetype. Buy the highest-score affordable item, repeat while gold allows; never reroll.
- Bots avoid walls by sampling 3 directions (straight, ±45°) and picking the first that `Physics.CheckCapsule` says is free (obstacle layer).

## 10. UI (all built by `UiFactory` in code)

Screen-space overlay canvas (`CanvasScaler` scale with screen size, reference 1920×1080).
- HUD (Combat): top center `Blue 1 – 2 Red`, round number, timer; top-left gold; bottom center skill bar: 5 slots (LMB, Q, W, E, R) with icon (colored square + key letter), radial cooldown overlay (`Image.fillAmount`, `Filled/Radial360`), cooldown seconds text, ready flash; left column: owned runes as small colored squares with 2-char labels and stack counts, set progress `Ember 2/3`; bottom-right: 6 item slots; center-bottom: player HP bar (big) with numbers.
- Overhead: per-unit world-space canvas with name and HP bar (green ally / red enemy) + shield (white segment).
- Floating damage numbers (world-space `Text` rising and fading 0.8 s; Physical = orange, Magical = cyan, True = white; crit ×1.4 size).
- Draft panel: 3 cards (300×420) with rarity-colored border (Common grey, Rare blue, Epic purple, Legendary gold), name, set tag, description, stack count if owned; hover scale 1.05; click or 1/2/3 to pick; countdown text.
- Shop panel: 5 item cards (name, tier, cost, stats/passive), Buy button (disabled if unaffordable/full/owned), Reroll button with cost, inventory row (6 slots, click to Sell), gold display, Ready button, countdown.
- Main menu: title, buttons `1v1`, `2v2`, `3v3`, seed input (`InputField`), Start.
- Hero select: three hero cards with role and skill summaries; click to choose.
- Round end banner, Match end panel (winner, kills per unit, Rematch, Main Menu), Pause (Resume, Main Menu).
- Chest reveal card (see §8).

## 11. Juice (all in `Juice` namespace, driven by EventBus)

- Hit-stop: 40 ms on skill hits, 25 ms on basic hits, 90 ms on kills. Implement via `Time.timeScale = 0.05` and restore after unscaled delay; coalesce overlapping requests (take max remaining).
- Camera shake: trauma model (`trauma += x`, decays 1.5/s, shake = trauma²; positional offset ≤ 0.35 units, rotational ≤ 1.5°). Basic hit 0.15, skill hit 0.3, ult 0.6, own death 0.8.
- Hit flash: all renderers on target set to white emissive for 60 ms.
- Squash & stretch: cast windup scales (1.1, 0.85, 1.1) → (0.9, 1.15, 0.9) at release → 1 over 0.15 s; dash stretches along the dash axis.
- Telegraphs: `Circle` skills show a flat translucent disc (quad on ground) filling from center over `Delay`; cones show a translucent wedge mesh built in code; dashes show a thin line.
- Projectiles: sphere (0.3) + `TrailRenderer` (width 0.25 → 0, 0.2 s), colored per hero. Impact: burst particle (a `ParticleSystem` built in code: 12 particles, 0.3 s) + a quick expanding ring.
- Cooldown ready ping: skill icon flashes white and scales 1.2 → 1 over 0.2 s.
- Audio: `SfxSynth` generates clips at startup with `AudioClip.Create` (44.1 kHz mono, ≤ 0.25 s): cast (rising sine sweep), hit (short noise burst with fast decay), kill (two-tone), pickup (major arpeggio), ui click (tick), draft reveal (soft chime). One pooled `AudioSource` set, volume 0.35. Pitch jitter ±5% (cosmetic; `UnityEngine.Random` allowed here).

## 12. Architecture

```
Assets/
  Scripts/RuneArena.Runtime.asmdef
  Scripts/Core/       Enums.cs, StatBlock.cs, StatModifier.cs, DamageInfo.cs, Definitions/*.cs (HeroDefinition, SkillDefinition, SkillEffect, RuneDefinition, ItemDefinition, LootTable),
                      EventBus.cs, Events.cs, Rng.cs, GameServices.cs, ICombatHook.cs, MatchConfig.cs, GameConstants.cs
  Scripts/Content/    ContentCatalog.cs (entry), HeroCatalog.cs, RuneCatalog.cs, RuneHooks/*.cs, ItemCatalog.cs, ItemHooks/*.cs, LootTables.cs
  Scripts/Combat/     CombatWorld.cs (registry + spatial queries), Unit.cs, UnitMotor.cs, UnitVisuals.cs, SkillCaster.cs (windup/recovery/buffer/cooldowns),
                      SkillExecutor.cs (+ per-shape resolvers), Projectile.cs, StatusEffects.cs, DamagePipeline.cs, Shields.cs, Telegraphs.cs
  Scripts/Runes/      RuneDraftService.cs, RuneInventory.cs, RuneSets.cs
  Scripts/Items/      ShopService.cs, Inventory.cs
  Scripts/Loot/       ChestSpawner.cs, Chest.cs, LootService.cs
  Scripts/AI/         BotBrain.cs, BotCombat.cs, BotShopping.cs
  Scripts/Match/      Bootstrap.cs, GameRoot.cs, MatchController.cs, MatchPhaseRunner.cs, Arena.cs (map builder), ControlPoint.cs, Economy.cs, Scoring.cs, TeamSpawner.cs
  Scripts/Player/     PlayerInput.cs, PlayerCamera.cs
  Scripts/UI/         UiFactory.cs, UiRoot.cs, Hud.cs, SkillBar.cs, OverheadBars.cs, DamageNumbers.cs, DraftPanel.cs, ShopPanel.cs, MenuPanels.cs, ChestRevealPanel.cs
  Scripts/Juice/      HitStop.cs, CameraShake.cs, HitFlash.cs, SquashStretch.cs, VfxFactory.cs, SfxSynth.cs, JuiceListener.cs
  Editor/RuneArena.Editor.asmdef, Editor/SceneSetup.cs  (menu "RuneArena/Create Arena Scene": creates Assets/Scenes/Arena.unity with Camera+Light+GameRoot and adds it to Build Settings; also a static method usable via -executeMethod)
  Tests/EditMode/…    (StatBlock math, Rng weighted pick distribution, RuneDraft pity/comeback/no-duplicates, Shop tier gating & reroll cost, Loot pity, Scoring tie-breaks)
  Tests/PlayMode/…    (Bootstrap creates GameRoot in an empty scene; a 3v3 all-bot match with a fixed seed runs at Time.timeScale=8 for up to 120 s of game time without exceptions and reaches MatchEnd; skills of every hero can be cast without errors)
```

### Core contracts (the Core agent defines these exactly; everyone codes against them)

- `enum Team { Blue, Red }`, `enum StatType {...}`, `enum DamageType { Physical, Magical, True }`, `enum DamageTag { Basic, Skill, Item, Rune, Burn, Reflect }`, `enum SkillKey { Basic, Q, W, E, R }`, `enum SkillShape {...}`, `enum SkillEffectType {...}`, `enum AiHint {...}`, `enum RuneRarity {...}`, `enum RuneSet {...}`, `enum ItemTier { T1 = 1, T2, T3 }`, `enum MatchPhase {...}`, `enum HeroArchetype { Mage, Bruiser, Assassin }`, `enum StatusType { Stun, Slow, SpeedBoost, DamageReduction, Invisible, Burn, Root }`.
- `StatModifier` (immutable): `StatType Stat, float Flat, float Percent, string SourceId`.
- `StatBlock`: `float Get(StatType)`, `void SetBase(StatType, float)`, `void AddModifier(StatModifier)`, `void RemoveBySource(string sourceId)`, `event Action Changed`.
- `DamageInfo` (immutable): `Unit Source, Unit Target, float Amount, DamageType Type, DamageTag Tag, bool IsCrit, string SkillId`.
- `DamageResult` (immutable): `float Dealt, float Absorbed, bool Killed`.
- Definitions: `HeroDefinition { Id, Name, Archetype, Color, BaseStats (IReadOnlyDictionary<StatType,float>), BasicAttack (SkillDefinition), Skills (IReadOnlyList<SkillDefinition> of Q,W,E,R) }`, `SkillDefinition` (fields per §5), `SkillEffect { Type, Value, Duration }`, `RuneDefinition`, `ItemDefinition`, `LootEntry { LootKind Kind, float Weight }`, `enum LootKind { Gold, CommonRune, RareItem, EpicRune, LegendaryItem }`.
- `ICombatHook { void Attach(Unit owner); void Detach(); }`. Hooks subscribe/unsubscribe to `EventBus` in Attach/Detach and filter events by `owner`.
- `EventBus` (static, typed): `Subscribe<T>(Action<T>)`, `Unsubscribe<T>(Action<T>)`, `Publish<T>(T)`, `Clear()`. Event structs (in `Events.cs`): `PhaseChanged(MatchPhase From, To, int Round)`, `RoundStarted(int Round)`, `RoundEnded(int Round, Team Winner)`, `UnitSpawned(Unit)`, `UnitDamaged(DamageInfo Info, DamageResult Result)`, `UnitDied(Unit Victim, Unit Killer)`, `BasicAttackHit(Unit Source, Unit Target, DamageResult)`, `SkillCast(Unit Caster, SkillDefinition Skill)`, `SkillHit(Unit Source, Unit Target, SkillDefinition Skill, DamageResult)`, `DashPerformed(Unit, Vector3 From, Vector3 To)`, `StatusApplied(Unit Target, StatusType, float Duration)`, `GoldChanged(Unit, int NewGold, int Delta)`, `RuneAcquired(Unit, RuneDefinition)`, `ItemAcquired(Unit, ItemDefinition)`, `ItemSold(Unit, ItemDefinition)`, `ChestOpened(Unit, LootKind, string RewardName)`, `ControlPointCaptured(Team)`, `CooldownReady(Unit, SkillKey)`.
- `Rng`: instance class wrapping `System.Random`: `int Range(int minInclusive, int maxExclusive)`, `float Value()`, `float Range(float, float)`, `T WeightedPick<T>(IReadOnlyList<T> items, Func<T,float> weight)`, `void Shuffle<T>(IList<T>)`.
- `GameServices` (static locator, reset on match start): `Rng Rng`, `CombatWorld World`, `MatchController Match`, `Economy Economy`, `RuneDraftService Draft`, `ShopService Shop`, `LootService Loot`, `UiRoot Ui`, `MatchConfig Config`.
- `MatchConfig` (immutable): `int TeamSize = 2, int Seed = 0, string PlayerHeroId = "blaze", float RoundSeconds = 75, int RoundsToWin = 3, bool HumanPlayer = true`.
- `Unit` (MonoBehaviour) public surface: `string UnitName; Team Team; HeroDefinition Hero; StatBlock Stats; float Health; bool IsAlive; bool IsInvisible; int Gold (via Economy); Vector3 Position; Vector3 Facing; UnitMotor Motor; SkillCaster Caster; StatusEffects Status; Shields Shields; RuneInventory Runes; Inventory Items; bool IsHuman;` methods `DamageResult ApplyDamage(DamageInfo)`, `void Heal(float, Unit source)`, `void AddShield(float amount, float duration, string sourceId)`, `void Kill(Unit killer)`, `void ResetForRound(Vector3 spawn)`, `void AddGold(int)`.
- `CombatWorld`: `IReadOnlyList<Unit> Units`, `Register/Unregister`, `IEnumerable<Unit> EnemiesOf(Team)`, `Unit NearestEnemy(Unit from, float maxRange, bool ignoreInvisible=true)`, `void EnemiesInRadius(Vector3 center, float radius, Team enemyOf, List<Unit> results)`, `void EnemiesInCone(Vector3 origin, Vector3 dir, float range, float angleDeg, Team enemyOf, List<Unit> results)`, `bool IsWalkable(Vector3)`, `Vector3 ClampToArena(Vector3)`, `IReadOnlyList<Chest> Chests`.

### Layers & physics
- Layer usage: default layer for everything; obstacles tagged via a `static HashSet<Collider>` in `Arena`, or use `Physics.CheckCapsule` with `LayerMask` of a dedicated layer index 8 named at runtime is not possible (layers are project settings) — so use **layer 8 with whatever name** it has, and keep an `Arena.ObstacleMask = 1 << 8`. `SceneSetup` also sets the layer name to "Obstacle" via `TagManager` when it runs (best effort).
- Heroes use `CharacterController` (radius 0.5, height 2, center y=1) and `controller.Move(delta)`; gravity is irrelevant (flat arena at y=0; keep y locked to 0 after Move).
- Arena: 44 × 26 floor (plane scaled), boundary walls (cubes), 6–8 pillar obstacles symmetric across the center, two bases at x = ±18. Camera: perspective FOV 50, pitch 62°, height ~16, follows player with 0.15 s smoothing and 15% lead toward cursor. For all-bot matches (tests/spectate) the camera follows Blue unit 0.

## 13. Definition of done for v0.1

- Opens in Unity 6000.4.5f1 with no compile errors or warnings-as-errors; pressing Play in an empty scene shows the main menu; a 2v2 match can be played end-to-end with mouse + keyboard.
- All three heroes' 5 abilities work, with telegraphs, hit-stop, shake, flash, damage numbers.
- Rune draft (rarity, pity, comeback, sets), shop (tiers, reroll, sell), chests (pity, reveal) all function and are visible in the HUD.
- Bots fight, use skills, capture, loot, draft, and shop.
- EditMode + PlayMode tests pass in batch mode.

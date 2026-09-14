# Rune Arena — Core contracts

All code lives in one assembly (`RuneArena.Runtime`), so namespaces can reference each other
freely; the *intended* dependency direction is still one way:

```
Core        (enums, definitions, StatBlock, DamageInfo, EventBus, Events, Rng, GameServices, constants)
  ^
Content     (immutable catalogs: heroes, runes + RuneHooks, items + ItemHooks, loot table)
  ^
Combat      (Unit, DamagePipeline, CombatWorld, StatusEffects, Shields, UnitMotor, SkillCaster, SkillExecutor, UnitVisuals)
  ^
Runes / Items / Loot   (services + per-unit inventories; talk to Combat through Unit and to Core through EventBus)
  ^
Match       (MatchController, Arena, Economy, ControlPoint, Scoring, spawning)  +  Player (input, camera)  +  AI (bots)
  ^
UI / Juice  (pure listeners: subscribe to EventBus, read GameServices, never mutate gameplay state except via public Unit/service APIs)
```

Rules: gameplay randomness only via `GameServices.Rng` (or `Rng.Fork`); definitions are immutable;
unit lookups go through `GameServices.World` (CombatWorld); services are reached through `GameServices`.

## Event flow of a skill hit

1. `PlayerInput` / `BotBrain` call `unit.Caster.TryCast(key, aimDir, aimPoint)`.
2. `SkillCaster` runs windup (0.6x move speed, buffer window 0.25 s), then at release publishes
   `SkillCast(caster, skill)`, starts the cooldown and hands the skill to `SkillExecutor`.
3. `SkillExecutor` resolves the shape (cone/projectile/dash/...) using `CombatWorld` queries, builds a
   `DamageInfo` with `DamagePipeline.ForSkill(source, target, skill, multiplier)` and calls
   `target.ApplyDamage(info)`; it then applies `SkillEffect`s via `target.Status.Apply(...)` /
   `target.AddShield(...)` / `target.Heal(...)` / `Motor.BeginDash(...)`.
4. `Unit.ApplyDamage` -> `DamagePipeline.Apply`: armor (`dmg*100/(100+armor)`, True ignores),
   status damage reduction, shields (`Shields.Absorb`), then HP (`SetHealthRaw`), lethal guards
   (不死), lifesteal for `DamageTag.Basic`.
5. `DamagePipeline` publishes `UnitDamaged(info, result)`, then `BasicAttackHit` (tag Basic) or
   `SkillHit` (tag Skill with `info.Skill` set), then `LethalDamagePrevented` if a guard fired,
   and finally calls `target.Kill(source)` which publishes `UnitDied(victim, killer)`.
6. Listeners react: rune/item `ICombatHook`s (extra damage, shields, burns via `Status.ApplyBurn`),
   `JuiceListener` (hit-stop, shake, flash, VFX, SFX), `UiRoot` (damage numbers, HP bars),
   `Economy`/`Scoring` (kill gold, assists, kill points).

Burns tick inside `StatusEffects` every 0.25 s through `DamagePipeline.DealBurnTick` (tag Burn),
so they flow through the same pipeline and events.

## Lifecycle of a round (MatchController)

```
RuneDraft  -> Draft.BeginDraft(round); Ui.ShowDraft(human); bots pick after 1-3 s; ends at 12 s or AllPicked
Shop       -> Shop.OpenShop(round); Ui.ShowShop(human); bots buy instantly + SetReady; ends at 12 s or AllReady
Countdown  -> every unit: ResetForRound(spawn) (full heal, clear statuses/shields/cooldowns), Motor.Locked = true,
              ControlPoint.ResetPoint(), Chests.ClearAll(), Scoring.Reset(); 3 s
Combat     -> publish RoundStarted(round); unlock motors; enable bots; per frame: Economy.TickPassive,
              ControlPoint.Tick, Chests.Tick; ends when a team is fully dead or at 75 s
              (Scoring.DecideTimeoutWinner: points -> HP% -> Red)
RoundEnd   -> publish RoundEnded(round, winner); round win/loss gold; Ui.ShowRoundEnd; 3 s
             -> MatchEnd (publish MatchEnded) when a team reaches RoundsToWin, else back to RuneDraft
```

`PhaseChanged(from, to, round)` is published on every transition. `GameServices.Reset()` runs at
match start before services are recreated; `EventBus.Clear()` is the MatchController's call on teardown.

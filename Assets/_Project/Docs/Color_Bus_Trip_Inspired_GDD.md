# Color Bus Sort Puzzle - Game Design Document

Version: 1.0  
Date: 2026-05-28  
Reference benchmark: Color Bus Trip, Google Play package `com.Tinkerbox.ColorTrip`  
Document goal: build a legally distinct mobile puzzle game inspired by the public mechanics of the reference game, without copying its name, logo, exact art, levels, text, UI, screenshots, store assets, or proprietary content.

## 1. Benchmark Summary

Public Google Play information observed on 2026-05-28:

- Title: Color Bus Trip
- Developer / publisher shown: VOODOO
- Category: Puzzle
- Store description hook: tap buses, match colors, complete pickups in a satisfying sorting puzzle.
- Public gameplay description: tap a bus to send it to the nearest empty station; non-matching passengers get off; same-colored passengers get on; a fully loaded bus exits; player plans taps to avoid traffic and match colors.
- Rating shown: 4.6 stars
- Downloads shown: 1M+
- Updated on: 2026-05-19
- Monetization shown: in-app purchases; store listing also implies a free install flow.
- Content rating: Everyone
- Source: https://play.google.com/store/apps/details?id=com.Tinkerbox.ColorTrip

Design conclusion: the product sits in the hyper-casual / hybrid-casual puzzle lane: very simple input, high readability, short levels, strong color sorting satisfaction, low-friction replay, and monetization through ads/IAP/boosters.

## 2. Legal Product Direction

Do not make a 1:1 clone. We should create a new game with the same broad market appeal:

- Keep: color sorting, bus routing, one-tap decisions, short puzzle sessions, idle-friendly readability.
- Change: game title, bus designs, passenger shapes, station layout language, background world, UI composition, level layouts, progression map, store copy, icons, sound identity, animation timing, booster names, and monetization packaging.
- Recommended working title: Transit Tint, Hue Shuttle, Color Terminal, or Bus Palette Jam.
- Recommended visual identity: clean toy-like transit diorama with unique station signs, lane markings, and city theme progression.

## 3. Core Fantasy

The player is a dispatcher solving tiny traffic puzzles by sending colored buses into pickup bays, cleaning up wrong passengers, and filling each bus with the correct color group before it leaves.

Player feeling target:

- "I understand this in 2 seconds."
- "That move was satisfying."
- "I can fix this if I plan one step ahead."
- "One more level."

## 4. Target Audience

- Primary: casual mobile puzzle players, age 8+, broad gender split.
- Session style: 30 seconds to 3 minutes per level.
- Skill type: visual matching, queue management, light planning.
- Device target: mid/low Android first, 60 FPS target, portrait orientation.

## 5. Core Loop

1. Player sees several colored buses, passengers, and empty/occupied stations.
2. Player taps one available bus.
3. Bus drives to the nearest valid empty station or assigned lane.
4. Bus unloads passengers that do not match its bus color.
5. Matching passengers board until the bus capacity is full or no valid passenger is available.
6. Full bus exits, freeing space and revealing new movement options.
7. Player repeats until all required passengers are transported.
8. Win screen gives coins/stars/progression; next level starts quickly.

Loss/soft-fail:

- Hard fail option: no valid bus movement and all stations are blocked.
- Preferred casual option: level pauses at deadlock and offers Undo, Shuffle, Extra Station, or Watch Ad to continue.

## 6. Main Game Rules

Entities:

- Bus: has `colorId`, `capacity`, current passenger list, route state, and lane position.
- Passenger: has `colorId`, position, and state: waiting, boarding, seated, exiting, removed.
- Station/Bay: temporary stop where one bus can unload/load.
- Exit Gate: target path for completed buses.
- Queue/Lane: ordering system that controls which bus can be tapped or moved.

Rules:

- A tapped bus must be selectable, not blocked, not already moving, and have a valid station.
- The bus moves to the nearest empty station by path distance, not necessarily screen distance.
- Wrong-color passengers on the bus disembark first.
- Correct-color waiting passengers board if they are reachable and capacity remains.
- A bus exits immediately when all seats are filled with its color.
- Level completes when all target passengers are delivered or all buses are cleared.

Recommended capacities:

- Early game: 3-4 seats.
- Mid game: 4-6 seats.
- Late game: 6-8 seats plus special constraints.

## 7. Player Controls

- Tap bus: select and dispatch.
- Tap booster button: activate one helper action.
- Optional drag camera is not recommended for MVP; keep board fully visible.
- No joystick, no swipe requirement.

Input feedback:

- Valid bus: small bounce/highlight on tap.
- Invalid bus: red wiggle, blocked icon, short haptic tick.
- Bus path: brief lane glow or arrow trail.
- Successful completion: bus honk, passenger cheer, coin burst.

## 8. Level Design System

Difficulty variables:

- Number of colors.
- Number of buses.
- Bus capacity.
- Number of station slots.
- Amount of wrong-color passengers inside buses.
- Passenger queue ordering.
- Blocked lanes or one-way lanes.
- Hidden/revealed buses.
- Special vehicles with larger size.

Level pacing:

- Levels 1-5: teach one tap, match color, bus exits.
- Levels 6-15: add wrong-color unload and limited stations.
- Levels 16-30: add multi-color planning and bus order dependencies.
- Levels 31-60: introduce blockers, larger buses, and extra station booster.
- Levels 61+: rotate mechanics in events, daily challenges, and hard levels.

Golden rule: every puzzle should be solvable without boosters. Boosters should recover mistakes or reduce friction, not be mandatory.

## 9. Example Level Templates

Tutorial Level 1:

- Colors: red, blue
- Buses: red capacity 3, blue capacity 3
- Stations: 1
- Passengers: 3 red, 3 blue
- Goal: teach that full matching bus exits.

Planning Level 8:

- Colors: red, blue, yellow
- Buses: red bus contains 1 blue passenger; blue bus contains 1 red passenger.
- Stations: 1
- Goal: teach unloading wrong passengers creates future matches.

Deadlock Training Level 14:

- Colors: 3
- Stations: 2
- One bus blocks another bus path.
- Goal: teach dispatch order.

Hard Level 35:

- Colors: 4
- Buses: 7
- Stations: 2
- Hidden queue: 2 buses reveal after exits.
- Goal: create satisfying multi-step chain.

## 10. Boosters

Undo:

- Reverts last bus dispatch.
- Free tutorial grants 3 uses; later coin/ad/IAP source.

Extra Station:

- Adds one temporary station slot for the current level.
- Best rewarded-ad candidate.

Swap Passengers:

- Swaps top/nearest waiting passengers between two queues or swaps two passengers inside a bus.
- Use carefully because it can break puzzle clarity.

Paint Ticket:

- Converts one passenger to selected bus color.
- Strong power; late-game only.

Hint:

- Highlights a valid next bus. Should choose a move from solver or pre-authored hint path.

## 11. Economy And Monetization

Soft currency: Coins

- Earned per win, streak, daily reward, hard level bonus.
- Spent on boosters and cosmetic bus skins.

Stars:

- Optional mastery layer: 3 stars based on no booster / few moves / fast clear.
- Avoid timer pressure in early game.

Ads:

- Interstitial after every 2-3 wins, never after a loss in the first 10 levels.
- Rewarded ads for Extra Station, Undo pack, x2 coins, revive from deadlock.
- Banner ads are not recommended during gameplay because they reduce puzzle readability.

IAP:

- Remove Ads.
- Starter Pack: coins + boosters + unique bus skin.
- Booster bundles.
- Cosmetic city themes or bus packs.

## 12. Progression

Recommended structure:

- Linear level ladder for MVP.
- Every 5th level: harder puzzle with better reward.
- Every 10th level: new visual city block or station dressing.
- Daily challenge unlocks after level 20.
- Event board unlocks after level 35.

Retention hooks:

- Day 1: simple daily reward.
- Day 3: unlock cosmetic bus skin.
- Day 7: weekly challenge chest.
- Streak saver via rewarded ad.

## 13. UI/UX Screens

Required MVP screens:

- Splash/loading.
- Main menu with Play, level number, coins, settings.
- Gameplay HUD: level, coins, booster buttons, restart, settings.
- Win popup: reward, next button, x2 reward ad.
- Fail/deadlock popup: undo, extra station, restart.
- Shop popup: boosters, coins, remove ads.
- Settings: sound, music, haptics, privacy, restore purchases.

Gameplay HUD principles:

- Keep the board clean and readable.
- Put boosters near thumb zone, bottom left/right.
- Use clear icons; avoid long text during play.
- Colorblind support: add simple symbols on bus roof and passenger hats.

## 14. Art Direction

Camera:

- Portrait, orthographic 3D, slight top-down angle.
- Entire puzzle board visible at once.

Style:

- Soft toy-like 3D, rounded silhouettes, bright but not over-saturated.
- Unique identity: use transit terminal signs, patterned buses, and city-specific station props.

Color palette:

- Use 6-8 gameplay colors with high separation: red, blue, yellow, green, purple, orange, cyan, pink.
- Add symbols/patterns for accessibility: circle, stripe, triangle, star, diamond.

Animation:

- Bus tap bounce: 0.08-0.12s.
- Bus drive to station: 0.45-0.8s depending distance.
- Passenger unload/load: staggered 0.05-0.08s per passenger.
- Bus exit: 0.5s with small trail/confetti for completion.

Audio:

- Soft tap click.
- Small engine loop only while moving.
- Boarding pop.
- Full bus cheer.
- Deadlock low tone.
- Keep sounds short and non-fatiguing.

## 15. Technical Design For Unity

Recommended Unity scene modules:

- `LevelManager`: loads level data and controls win/loss.
- `BoardController`: owns stations, lanes, path graph, and occupancy.
- `BusController`: bus state machine and movement animation.
- `PassengerController`: passenger animation and state.
- `MoveResolver`: validates tap and resolves unload/load/exit sequence.
- `PuzzleSolver`: optional offline tool/runtime helper for hints and validation.
- `BoosterService`: applies undo, extra station, hint, swap, paint.
- `EconomyService`: coins, rewards, purchases.
- `AdService`: interstitial/rewarded placements.
- `SaveService`: progress, currency, settings.

Bus state machine:

- Idle
- Selected
- MovingToStation
- Unloading
- Loading
- Completed
- Exiting
- Blocked

Move resolution order:

1. Validate bus can move.
2. Reserve destination station.
3. Animate bus movement.
4. Unload wrong-color passengers.
5. Match and board correct passengers.
6. Check completion.
7. Release or update station state.
8. Evaluate win/deadlock.

## 16. Level Data Schema

Suggested JSON shape:

```json
{
  "levelId": 1,
  "colors": ["red", "blue"],
  "stations": [
    { "id": "S1", "x": 0, "y": 0 }
  ],
  "buses": [
    {
      "id": "B1",
      "color": "red",
      "capacity": 3,
      "laneId": "L1",
      "startIndex": 0,
      "passengers": ["red", "blue"]
    }
  ],
  "waitingPassengers": [
    { "color": "red", "queueId": "Q1", "count": 3 }
  ],
  "blockers": [],
  "target": { "clearAllBuses": true },
  "moveLimit": null
}
```

## 17. Analytics Events

Core:

- `level_start(levelId, attempt)`
- `bus_tap(levelId, busColor, valid, reasonIfInvalid)`
- `move_resolved(levelId, unloadedCount, loadedCount, completedBus)`
- `booster_used(levelId, boosterType, source)`
- `deadlock(levelId, movesMade)`
- `level_complete(levelId, duration, moves, boostersUsed)`
- `level_fail(levelId, duration, reason)`

Monetization:

- `ad_offer_shown(placement)`
- `ad_started(placement)`
- `ad_completed(placement)`
- `iap_started(productId)`
- `iap_completed(productId)`

Balance KPIs:

- Level completion rate.
- Average attempts per level.
- Deadlock rate.
- Booster usage per level.
- D1/D3/D7 retention.
- Interstitial impression frequency.
- Rewarded ad opt-in rate.

## 18. MVP Scope

MVP must include:

- 50 hand-authored levels.
- 5 gameplay colors.
- 3 bus capacities.
- 1 station type.
- Undo, Extra Station, Hint.
- Coins and basic booster shop.
- Interstitial + rewarded ad hooks.
- Simple level win/fail flow.
- Save progress.
- Sound/music/haptic toggles.

Post-MVP:

- 200+ levels.
- Daily challenge.
- Cosmetic skins.
- Event ladder.
- Cloud save.
- Level editor tool.
- Solver-assisted level validation.

## 19. Production Milestones

Prototype, 3-5 days:

- Tap bus, station movement, load/unload, win condition.
- 10 test levels.

Vertical slice, 1-2 weeks:

- Full gameplay loop, HUD, 30 levels, boosters, save, basic polish.

Soft launch, 3-5 weeks:

- 150 levels, ads/IAP, analytics, balance tuning, onboarding, store assets.

Live iteration:

- Add 50-100 levels weekly at launch pace.
- Tune ad frequency based on retention.
- Add event variants after core metrics stabilize.

## 20. Main Design Risks

- Risk: game feels too automatic. Mitigation: station limits, bus order dependencies, visible future consequences.
- Risk: color confusion. Mitigation: symbols/patterns and high-contrast palette.
- Risk: deadlocks feel unfair. Mitigation: clear invalid feedback, Undo, hint logic, solver validation.
- Risk: ad pressure hurts retention. Mitigation: no early forced ads, reward ads tied to recovery/value.
- Risk: too similar to benchmark. Mitigation: unique visual identity, different progression, original levels, original store presentation.

## 21. Acceptance Criteria

The MVP is ready when:

- A new player understands the first level without reading instructions.
- Levels 1-10 have over 90% completion in internal tests.
- No level requires boosters to solve.
- Deadlock detection is reliable.
- Undo restores board state perfectly.
- Loading and moving animations never block input permanently.
- Game runs at 60 FPS on common Android mid-tier devices.
- Art, UI, and copy are original, not copied from the reference game.


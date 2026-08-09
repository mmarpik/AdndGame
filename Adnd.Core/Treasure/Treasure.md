# Treasure System

This document describes how treasure is generated after combat.

## Main Components

- `TreasureService` (`Adnd.Core/Treasure/TreasureService.cs`)
  - Rolls treasure for each monster in an encounter.
- `ITreasureTableProvider` (`Adnd.Core/Treasure/ITreasureTableProvider.cs`)
  - Resolves a treasure type code (for example `A`, `B`, etc.) into a `TreasureTable`.
- `TreasureTable` (`Adnd.Core/Treasure/TreasureTable.cs`)
  - Defines coin rules, valuables rules, and magic placeholder rules.
- `TreasureResult` (`Adnd.Core/Treasure/TreasureResult.cs`)
  - Aggregated rolled output (coins, valuables, magic placeholders, and logs).

## Encounter Roll Flow

`TreasureService.RollTreasureForEncounter(IEnumerable<MonsterInstance> monsters)`:

1. For each monster, parse `monster.Template.TreasureType` into one or more tokens.
2. Skip empty/`None` treasure types.
3. For each token:
   - Load table with `ITreasureTableProvider.TryGetTable`.
   - If `TreasureChanceOverride` is set on monster template, apply it as a per-table gate.
   - Roll the table:
	 - Coins: CP/SP/EP/GP/PP rules.
	 - Valuables: Gems, Jewelry, Art.
	 - Magic placeholders from `MagicRolls`.
4. Append readable log entries to `TreasureResult.LogLines`.

## Rule Model

### Coin rule (`TreasureRollRule`)
- `ChancePercent` (0-100)
- `AmountExpression` (for example `2d6*100`, `500`, `1d4+1`)

### Valuables rule (`TreasureValuablesRule`)
- `ChancePercent`
- `AmountExpression` (number of items)
- `MinValueGp`, `MaxValueGp` (each item gets a random GP value in this inclusive range)

### Magic rule (`TreasureMagicRule`)
- `Table` (magic table identifier)
- `ChancePercent`
- `AmountExpression` (number of placeholders to produce)

## Amount Expression Format

`TreasureService` supports:

- Fixed integers: `250`
- Dice with optional modifier: `NdS`, `NdS+K`, `NdS-K` (for example `3d6`, `1d8+2`)
- Optional multiplier: `<base>*<multiplier>` (for example `2d4*100`)

Notes:
- Whitespace is ignored.
- Invalid expressions evaluate to `0`.
- Dice count is at least 1; dice sides are clamped to at least 2.

## Chance Handling

- `ChancePercent <= 0` => never rolls.
- `ChancePercent >= 100` => always rolls.
- Otherwise uses `Random.Next(1, 101) <= chancePercent`.

## Result Fields

`TreasureResult` contains:

- Coin totals: `CopperPieces`, `SilverPieces`, `ElectrumPieces`, `GoldPieces`, `PlatinumPieces`
- Valuables lists: `Gems`, `Jewelry`, `Art`
- `MagicPlaceholders`: unresolved magic item placeholders to resolve later
- `LogLines`: detailed roll trace

Computed totals:
- `TotalGemValueGp`
- `TotalJewelryValueGp`
- `TotalArtValueGp`

## Parsing Treasure Types

Treasure type strings are split by `,`, `;`, `/`, `|`.

- Tokens are trimmed and uppercased.
- `None` is ignored.
- Duplicates are removed case-insensitively.

Example:
- Input: `"A, b / None | a"`
- Parsed tokens: `A`, `B`

## Other Treasure Type notes
- I have manually updated the treasure tables to make them adjusted to 
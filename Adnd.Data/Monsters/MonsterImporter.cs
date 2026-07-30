//Konverterar JSON‑modeller → Core‑modeller.
using System;
using System.Linq;
using Adnd.Core.Monsters;

namespace Adnd.Data.Monsters;

public static class MonsterImporter
{
    public static Monster Convert(MonsterJsonModel json)
    {
        return new Monster
        {
            Name = json.Name,
            Type = Enum.TryParse<MonsterType>(json.Type, out var t) ? t : MonsterType.Other,

            ArmorClass = json.ArmorClass,
            HitDice = json.HitDice,
            HitPoints = json.HitPoints,

            XPValue = json.XPValue,
            TreasureType = string.IsNullOrWhiteSpace(json.TreasureType) ? "None" : json.TreasureType,
            TreasureChanceOverride = json.TreasureChanceOverride,

            Movement = new MonsterMovement
            {
                Walk = json.Movement.Walk,
                Fly = json.Movement.Fly,
                Swim = json.Movement.Swim,
                Burrow = json.Movement.Burrow,
                Climb = json.Movement.Climb
            },

            SavingThrows = new MonsterSavingThrows
            {
                ParalyzationPoisonDeath = json.SavingThrows.ParalyzationPoisonDeath,
                RodStaffWand = json.SavingThrows.RodStaffWand,
                PetrificationPolymorph = json.SavingThrows.PetrificationPolymorph,
                BreathWeapon = json.SavingThrows.BreathWeapon,
                Spell = json.SavingThrows.Spell
            },

            Morale = new MonsterMorale
            {
                Value = json.Morale.Value
            },

            Attacks = json.Attacks.Select(a => new MonsterAttack
            {
                Name = a.Name,
                NumberOfAttacks = a.NumberOfAttacks,
                Damage = a.Damage
            }).ToList(),

            SpecialAbilities = json.SpecialAbilities.Select(sa => new MonsterSpecialAbility
            {
                Name = sa.Name,
                Description = sa.Description
            }).ToList()
        };
    }
}

#nullable enable
using System;
using System.Collections.Generic;
using WizardryViewer.Protocol;

namespace WizardryViewer.Presentation
{

    /// <summary>
    /// Turns structured log entries into words. This class exists entirely on the viewer side —
    /// the game never sends a sentence. Swap the <see cref="Vocabulary"/> and you have a
    /// different language, a different tone, or a TTS script; the protocol doesn't change.
    /// </summary>
    public sealed class Narrator
    {
        private readonly Vocabulary _v;
        private readonly Func<string, string> _nameOf;

        /// <param name="nameOf">Resolves an entity id (char:/mon:/group:) to a display name.</param>
        public Narrator(Vocabulary vocabulary, Func<string, string> nameOf)
        {
            _v = vocabulary;
            _nameOf = nameOf;
        }

        public string? Describe(LogEntry e)
        {
            switch (e.T)
            {
                case LogTypes.Attack:
                    return e.Hit == true
                        ? _v.Hit(_nameOf(e.By ?? ""), _nameOf(e.At ?? ""), e.Damage ?? 0)
                        : _v.Miss(_nameOf(e.By ?? ""), _nameOf(e.At ?? ""));

                case LogTypes.Damage:
                    return _v.Damage(_nameOf(e.At ?? ""), e.Amount ?? 0);

                case LogTypes.Heal:
                    return _v.Heal(_nameOf(e.At ?? ""), e.Amount ?? 0);

                case LogTypes.Cast:
                    return _v.Cast(_nameOf(e.By ?? ""), _v.Spell(e.SpellId ?? ""));

                case LogTypes.Save:
                    return e.Result == SaveResults.Success
                        ? _v.SaveSuccess(_nameOf(e.At ?? ""), _v.Spell(e.Vs ?? ""))
                        : _v.SaveFailure(_nameOf(e.At ?? ""), _v.Spell(e.Vs ?? ""));

                case LogTypes.Status:
                    if (e.Add is { Count: > 0 })
                        return _v.StatusGained(_nameOf(e.At ?? ""), _v.Status(e.Add[0]));
                    if (e.Remove is { Count: > 0 })
                        return _v.StatusLost(_nameOf(e.At ?? ""), _v.Status(e.Remove[0]));
                    return null;

                case LogTypes.Death:
                    return _v.Death(_nameOf(e.At ?? ""));

                case LogTypes.Spawn:
                    return null; // the figurine arriving says it better than words

                case LogTypes.Move:
                    return null; // silent

                case LogTypes.Experience:
                    return _v.Experience(e.Amount ?? 0);

                case LogTypes.Treasure:
                    return _v.Treasure(e.Gold ?? 0, (e.ItemIds ?? new List<string>()).ConvertAll(_v.Item));

                default:
                    return null; // unknown type: stay quiet rather than guess
            }
        }
    }

    /// <summary>Every string the viewer shows. Subclass or swap for another language.</summary>
    public class Vocabulary
    {
        protected readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase);

        public virtual string Spell(string id) => Lookup(id);
        public virtual string Item(string id) => Lookup(id);
        public virtual string Status(string id) => Lookup(id);
        public virtual string Monster(string id) => Lookup(id);

        /// <summary>Unknown ids fall back to the raw key: visible and debuggable, never fatal.</summary>
        protected string Lookup(string id) => Names.TryGetValue(id, out var s) ? s : id;

        public virtual string Hit(string a, string b, int dmg) => $"{a} hits {b} for {dmg}.";
        public virtual string Miss(string a, string b) => $"{a} swings at {b} and misses.";
        public virtual string Damage(string who, int n) => $"{who} takes {n} damage.";
        public virtual string Heal(string who, int n) => $"{who} recovers {n} hit points.";
        public virtual string Cast(string who, string spell) => $"{who} casts {spell}!";
        public virtual string SaveSuccess(string who, string vs) => $"{who} resists {vs}.";
        public virtual string SaveFailure(string who, string vs) => $"{who} succumbs to {vs}.";
        public virtual string StatusGained(string who, string s) => $"{who} is {s}.";
        public virtual string StatusLost(string who, string s) => $"{who} is no longer {s}.";
        public virtual string Death(string who) => $"{who} falls.";
        public virtual string Experience(int n) => $"The party gains {n} experience.";

        public virtual string Treasure(int gold, List<string> items) =>
            items.Count == 0 ? $"{gold} gold pieces." : $"{gold} gold pieces and {string.Join(", ", items)}.";

        public Vocabulary()
        {
            Names["sleep"] = "Sleep";
            Names["magic_missile"] = "Magic Missile";
            Names["cure_light_wounds"] = "Cure Light Wounds";
            Names["potion_healing"] = "a Potion of Healing";
            Names["Asleep"] = "asleep";
            Names["Paralyzed"] = "paralysed";
            Names["Invisible"] = "invisible";
        }
    }

    /// <summary>Proof that language is a viewer concern: same protocol, different words.</summary>
    public sealed class SwedishVocabulary : Vocabulary
    {
        public SwedishVocabulary()
        {
            Names["sleep"] = "Sömn";
            Names["magic_missile"] = "Magisk Pil";
            Names["potion_healing"] = "en läkedryck";
            Names["Asleep"] = "sövd";
            Names["Paralyzed"] = "förlamad";
            Names["Invisible"] = "osynlig";
        }

        public override string Hit(string a, string b, int dmg) => $"{a} träffar {b} för {dmg}.";
        public override string Miss(string a, string b) => $"{a} hugger efter {b} och missar.";
        public override string Damage(string who, int n) => $"{who} tar {n} i skada.";
        public override string Heal(string who, int n) => $"{who} återfår {n} kroppspoäng.";
        public override string Cast(string who, string spell) => $"{who} kastar {spell}!";
        public override string SaveSuccess(string who, string vs) => $"{who} motstår {vs}.";
        public override string SaveFailure(string who, string vs) => $"{who} faller offer för {vs}.";
        public override string StatusGained(string who, string s) => $"{who} är {s}.";
        public override string StatusLost(string who, string s) => $"{who} är inte längre {s}.";
        public override string Death(string who) => $"{who} faller.";
        public override string Experience(int n) => $"Sällskapet får {n} erfarenhetspoäng.";
        public override string Treasure(int gold, List<string> items) =>
            items.Count == 0 ? $"{gold} guldmynt." : $"{gold} guldmynt och {string.Join(", ", items)}.";
    }

}

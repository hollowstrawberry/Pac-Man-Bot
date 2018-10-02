using System;
using System.Linq;
using System.Collections.Generic;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG
{
    /// <summary>
    /// Contains extension methods for the RPG such as those that allow strings to get converted into game objects.
    /// </summary>
    public static class Extensions
    {
        public static Item GetItem(this string item) => ItemTypes.GetValueOrDefault(item);
        public static Equipment GetEquip(this string equipment) => EquipTypes.GetValueOrDefault(equipment);
        public static Weapon GetWeapon(this string item) => WeaponTypes.GetValueOrDefault(item);
        public static Armor GetArmor(this string item) => ArmorTypes.GetValueOrDefault(item);
        public static Enemy GetEnemy(this string enemy) => EnemyTypes.GetValueOrDefault(enemy);
        public static Buff GetBuff(this string buff) => BuffTypes.GetValueOrDefault(buff);
        public static Skill GetSkill(this string skill) => SkillTypes.GetValueOrDefault(skill);


        public static IReadOnlyDictionary<string, Item> ItemTypes = GetTypes<Item>();
        public static IReadOnlyDictionary<string, Equipment> EquipTypes = GetTypes<Equipment>();
        public static IReadOnlyDictionary<string, Weapon> WeaponTypes = GetTypes<Weapon>();
        public static IReadOnlyDictionary<string, Armor> ArmorTypes = GetTypes<Armor>();
        public static IReadOnlyDictionary<string, Enemy> EnemyTypes = GetTypes<Enemy>();
        public static IReadOnlyDictionary<string, Buff> BuffTypes = GetTypes<Buff>();
        public static IReadOnlyDictionary<string, Skill> SkillTypes = GetTypes<Skill>();


        private static IReadOnlyDictionary<string, T> GetTypes<T>() where T : IKeyable
        {
            return typeof(RpgGame).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(T)))
                .Select(t => (T)Activator.CreateInstance(t, true))
                .ToDictionary(i => i.Key).AsReadOnly();
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
using PacManBot.Extensions;

namespace PacManBot.Games.Concrete.RPG
{
    public static class Extensions
    {
        public static Item Item(this string item) => ItemTypes[item];
        public static Equipment Equipment(this string equipment) => EquipmentTypes[equipment];
        public static Weapon Weapon(this string item) => WeaponTypes[item];
        public static Enemy Enemy(this string enemy) => EnemyTypes[enemy];
        public static Buff Buff(this string buff) => BuffTypes[buff];


        public static IReadOnlyDictionary<string, Item> ItemTypes = GetTypes<Item>();
        public static IReadOnlyDictionary<string, Equipment> EquipmentTypes = GetTypes<Equipment>();
        public static IReadOnlyDictionary<string, Weapon> WeaponTypes = GetTypes<Weapon>();
        public static IReadOnlyDictionary<string, Enemy> EnemyTypes = GetTypes<Enemy>();
        public static IReadOnlyDictionary<string, Buff> BuffTypes = GetTypes<Buff>();


        private static IReadOnlyDictionary<string, T> GetTypes<T>() where T : IKeyable
        {
            return typeof(RpgGame).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(T)))
                .Select(t => (T)Activator.CreateInstance(t, true))
                .ToDictionary(i => i.Key).AsReadOnly();
        }
    }
}

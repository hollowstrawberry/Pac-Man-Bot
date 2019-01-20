using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PacManBot.Extensions
{
    public static class ReflectionExtensions
    {
        /// <summary>All types in the current <see cref="AppDomain"/>.</summary>
        public static readonly IEnumerable<Type> AllTypes =
            AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).ToArray();



        /// <summary>Shorthand to get a custom attribute from a member.</summary>
        public static TAttribute Get<TAttribute>(this MemberInfo member) where TAttribute : Attribute
            => member.GetCustomAttributes<TAttribute>().FirstOrDefault();


        /// <summary>Invokes a method and casts the return value to the specified type.</summary>
        public static TResult Invoke<TResult>(this MethodInfo method, object obj, params object[] parameters)
            => (TResult)method.Invoke(obj, parameters.Length == 0 ? null : parameters);


        /// <summary>Invokes a method and discards the result.</summary>
        public static void Invoke(this MethodInfo method, object obj, params object[] parameters)
            => method.Invoke(obj, parameters.Length == 0 ? null : parameters);


        /// <summary>Creates an instance of a given type using the default constructor and casts it to a known type.</summary>
        public static T CreateInstance<T>(this Type type)
            => (T)Activator.CreateInstance(type, true);


        /// <summary>Returns all concrete types that inherit from a known type.</summary>
        public static IEnumerable<Type> SubclassesOf<T>(this IEnumerable<Type> source)
            => source.Where(t => t.IsClass && !t.IsAbstract && typeof(T).IsAssignableFrom(t));


        /// <summary>Obtains all subtypes of a known type and instatiates them, to access their properties.</summary>
        public static IEnumerable<T> MakeObjects<T>(this IEnumerable<Type> source)
            => source.SubclassesOf<T>().Select(t => t.CreateInstance<T>());
    }
}

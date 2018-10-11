using System;
using System.Linq;
using System.Reflection;
using Discord.Commands;
using PacManBot.Extensions;
using ParameterInfo = System.Reflection.ParameterInfo; // Disambiguation

namespace PacManBot.Commands
{
    /// <summary>
    /// Allows you to create a new instance of a module. Only ever useful/justifiable in an evaluated script for testing.
    /// The class stores reflection data about a module type after the first use and uses it in the future.
    /// </summary>
    /// <typeparam name="TModule">The type of the command module.</typeparam>
    /// <typeparam name="TContext">The type of the context used by the command module.</typeparam>
    /// 
    public static class ModuleBuilder<TModule, TContext>
        where TModule : ModuleBase<TContext>
        where TContext : class, ICommandContext
    {
        private static Type ModuleType { get; }
        private static ConstructorInfo Constructor { get; set; }
        private static ParameterInfo[] ConstructorParameters { get; set; }
        private static PropertyInfo Context { get; }
        private static MethodInfo BeforeExecute { get; }

        static ModuleBuilder()
        {
            ModuleType = typeof(TModule);
            SetConstructor(ModuleType.GetConstructors().First());
            Context = ModuleType.GetProperty("Context").DeclaringType.GetProperty("Context");
            BeforeExecute = ModuleType.GetRuntimeMethods().First(x => x.Name == "BeforeExecute");
        }


        /// <summary>
        /// Sets a constructor to be used for this type when calling <see cref="Create(TContext, IServiceProvider, bool)"/>.
        /// </summary>
        /// <exception cref="ArgumentException">When the constructor does not match <typeparamref name="TModule"/>.</exception>
        public static void SetConstructor(ConstructorInfo constructor)
        {
            if (constructor.DeclaringType != ModuleType)
            {
                throw new ArgumentException($"Constructor does not belong to the type {ModuleType.Name}");
            }

            Constructor = constructor;
            ConstructorParameters = Constructor.GetParameters();
        }


        /// <summary>
        /// Returns a new instance of <typeparamref name="TModule"/> with the given context and using the given services.
        /// </summary>
        /// <param name="context">The command context to be used in this module.</param>
        /// <param name="provider">The objects to pass to this module's constructor.</param>
        /// <param name="beforeExecute">Whether to run <see cref="ModuleBase{T}.BeforeExecute(CommandInfo)"/>.</param>
        public static TModule Create(TContext context, IServiceProvider provider, bool beforeExecute = true)
        {
            var parameters = ConstructorParameters.Select(x => provider.GetService(x.ParameterType)).ToArray();
            var module = (TModule)Constructor.Invoke(parameters);

            Context.SetValue(module, context, BindingFlags.NonPublic | BindingFlags.Instance, null, null, null);
            if (beforeExecute) BeforeExecute.Invoke(module);

            return module;
        }
    }
}

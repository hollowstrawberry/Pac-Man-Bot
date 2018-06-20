using System;
using System.Linq;
using System.Reflection;
using Discord.Commands;
using ParameterInfo = System.Reflection.ParameterInfo; // Disambiguation

namespace PacManBot.Commands
{
    // This class is a small utility I made for fun.
    // It's very useful if you ever want to call a command's method from a different module.
    // The only valid use case so far in my opinion is from within an evaluated script.

    // The first time the class is accessed for the given generic types,
    // the static constructor is executed and stores the necessary info to create that module type in the future.

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


        static void SetConstructor(ConstructorInfo constructor)
        {
            if (constructor.DeclaringType != ModuleType) throw new ArgumentException("Constructor does not match the generic type");

            Constructor = constructor;
            ConstructorParameters = Constructor.GetParameters();
        }



        public static TModule Create(TContext context, IServiceProvider provider, bool beforeExecute = true)
        {
            var parameters = ConstructorParameters.Select(x => provider.GetService(x.ParameterType)).ToArray();
            var module = (TModule)Constructor.Invoke(parameters);

            Context.SetValue(module, context, BindingFlags.NonPublic | BindingFlags.Instance, null, null, null);
            if (beforeExecute) BeforeExecute.Invoke(module, new object[] { null });

            return module;
        }
    }
}

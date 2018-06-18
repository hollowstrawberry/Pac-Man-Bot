using System;
using System.Linq;
using System.Reflection;
using Discord.Commands;

namespace PacManBot.Commands
{
    // This class is a small utility I made for fun
    // I don't think it has any valid use cases,
    // But it'd be very useful if you ever wanted to
    // call a command from a different module.

    public static class ModuleBuilder<TModule, TContext>
        where TModule : ModuleBase<TContext>
        where TContext : class, ICommandContext
    {
        public static Type ModuleType { get; }
        public static ConstructorInfo Constructor { get; }
        public static System.Reflection.ParameterInfo[] ConstructorParameters { get; }
        public static PropertyInfo Context { get; }
        public static MethodInfo BeforeExecute { get; }

        static ModuleBuilder()
        {
            ModuleType = typeof(TModule);
            Constructor = ModuleType.GetConstructors().First();
            ConstructorParameters = Constructor.GetParameters();
            Context = ModuleType.GetProperty("Context").DeclaringType.GetProperty("Context");
            BeforeExecute = ModuleType.GetRuntimeMethods().First(x => x.Name == "BeforeExecute");
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


    public static class ModuleBuilder<TModule> where TModule : BaseCustomModule
    {
        public static TModule Create(ShardedCommandContext context, IServiceProvider services)
        {
            return ModuleBuilder<TModule, ShardedCommandContext>.Create(context, services);
        }
    }
}

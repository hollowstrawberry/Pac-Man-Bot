using System;
using System.Collections.Generic;
using System.Text;

namespace PacManBot.Commands
{
    /// <summary>
    /// Marks this class as being part of a command module with a name and index
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ModuleAttribute : Attribute
    {
        public string Name { get; private set; }

        public ModuleAttribute(string name)
        {
            Name = name;
        }
    }
}

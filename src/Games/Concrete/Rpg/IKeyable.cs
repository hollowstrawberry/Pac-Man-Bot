
namespace PacManBot.Games.Concrete.Rpg
{
    /// <summary>
    /// An object whose type can be represented by a key, usually the type's name.
    /// </summary>
    public interface IKeyable
    {
        string Key { get; }
    }
}

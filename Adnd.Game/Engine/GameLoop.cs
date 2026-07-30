// Engine/GameLoop.cs
namespace Adnd.Game.Engine;

public sealed class GameLoop
{
    public void Run()
    {
        bool running = true;
        while (running)
        {
            // TODO: input, update, render
            Console.WriteLine("AD&D 1e engine skeleton running...");
            running = false;
        }
    }
}

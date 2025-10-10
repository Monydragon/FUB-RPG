using Fub.Enums;
using Fub.Implementations.Actors;
using Fub.Implementations.Game;
using Fub.Implementations.Generation;
using Fub.Implementations.Parties;
using Fub.Implementations.Player;
using Fub.Implementations.Rendering;
using Spectre.Console;
// Added
using Fub.Interfaces.Actors;

namespace Fub;

class Program
{
    static void Main(string[] args)
    {
        AnsiConsole.MarkupLine("[bold green]Welcome to Fub RPG Prototype (Party Edition)![/]");

        var leader = CreateActor("Leader");
        var party = new Party(leader);

        // Offer to add companions up to 3 more (total 4)
        while (party.Members.Count < party.MaxSize &&
               AnsiConsole.Confirm($"Add companion {party.Members.Count}?", false))
        {
            var companion = CreateActor($"Member{party.Members.Count}");
            party.TryAdd(companion);
        }

        var state = new GameState(party);
        var generator = new SimpleMapGenerator();
        var renderer = new MapRenderer();
        var loop = new GameLoop(state, generator, renderer);

        loop.RunAsync().GetAwaiter().GetResult();

        AnsiConsole.MarkupLine("[grey]Thanks for playing![/]");
    }

    private static PlayerActor CreateActor(string defaultLabel)
    {
        var name = AnsiConsole.Ask<string>($"Enter name for {defaultLabel}:", defaultLabel);

        var species = AnsiConsole.Prompt(
            new SelectionPrompt<Species>()
                .Title($"Select [yellow]species[/] for {name}:")
                .AddChoices(Enum.GetValues<Species>()));

        var baseClass = AnsiConsole.Prompt(
            new SelectionPrompt<ActorClass>()
                .Title($"Select base [yellow]class[/] for {name}:")
                .AddChoices(Enum.GetValues<ActorClass>()));

        var profile = new PlayerProfile(name);
        var actor = new PlayerActor(name, species, baseClass, profile, 0, 0);
        return actor;
    }
}

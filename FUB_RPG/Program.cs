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

        var party = CharacterCreation();

        var state = new GameState(party);
        var generator = new SimpleMapGenerator();
        var renderer = new MapRenderer();
        var loop = new GameLoop(state, generator, renderer);

        loop.RunAsync().GetAwaiter().GetResult();

        AnsiConsole.MarkupLine("[grey]Thanks for playing![/]");
    }

    private static Party CharacterCreation()
    {
        var members = new List<IActor>();
        while (true)
        {
            Console.Clear();
            AnsiConsole.Write(new Rule("[bold cyan]Character Creation[/]").RuleStyle("cyan"));
            if (members.Count == 0)
                AnsiConsole.MarkupLine("[yellow]No party members yet.[/]");
            else
            {
                var table = new Table();
                table.AddColumn("Name");
                table.AddColumn("Species");
                table.AddColumn("Class");
                foreach (var m in members)
                    table.AddRow(m.Name, m.Species.ToString(), m.Class.ToString());
                AnsiConsole.Write(table);
            }
            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Choose an option:")
                .AddChoices("Add Member", "Remove Member", "Set Leader", "Finish"));

            if (choice == "Add Member")
            {
                if (members.Count >= 4)
                {
                    AnsiConsole.MarkupLine("[red]Party is full (max 4).[/]");
                    Console.ReadKey(true);
                    continue;
                }
                var actor = CreateActor($"Member{members.Count+1}");
                members.Add(actor);
            }
            else if (choice == "Remove Member")
            {
                if (members.Count == 0) continue;
                var name = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("Remove who?")
                    .AddChoices(members.Select(m => m.Name)));
                members.RemoveAll(m => m.Name == name);
            }
            else if (choice == "Set Leader")
            {
                if (members.Count == 0) continue;
                var name = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("Set who as leader?")
                    .AddChoices(members.Select(m => m.Name)));
                // Move selected to front
                var selected = members.First(m => m.Name == name);
                members.Remove(selected);
                members.Insert(0, selected);
            }
            else if (choice == "Finish")
            {
                if (members.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]You need at least one member.[/]");
                    Console.ReadKey(true);
                    continue;
                }
                var party = new Party(members[0]);
                for (int i = 1; i < members.Count; i++) party.TryAdd(members[i]);
                return party;
            }
        }
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

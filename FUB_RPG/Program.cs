using Fub.Enums;
using Fub.Implementations.Actors;
using Fub.Implementations.Game;
using Fub.Implementations.Generation;
using Fub.Implementations.Parties;
using Fub.Implementations.Player;
using Fub.Implementations.Rendering;
using Spectre.Console;
using Fub.Interfaces.Actors;
using Fub.Implementations.Input;
using System.Text;

namespace Fub;

class Program
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        bool prevCursor = Console.CursorVisible;
        Console.CursorVisible = false;
        try
        {
            AnsiConsole.MarkupLine("[bold green]Welcome to Fub RPG Prototype (Party Edition)![/]");

            // New: pick input mode by the first input pressed
            AnsiConsole.MarkupLine("[grey]Press any keyboard key or controller button to start...[/]");
            var mode = InputManager.WaitForFirstInput(out var controllerType);
            if (mode == InputMode.Keyboard) controllerType = ControllerType.Unknown;
            PromptNavigator.DefaultInputMode = mode;
            PromptNavigator.DefaultControllerType = controllerType;

            // Inform the player
            AnsiConsole.MarkupLine(mode == InputMode.Controller
                ? $"[cyan]Controller detected ({controllerType}). Showing controller button prompts.[/]"
                : "[cyan]Keyboard/Mouse mode. Showing keyboard key prompts.[/]");

            var party = CharacterCreation(mode, controllerType);

            var state = new GameState(party);
            state.SetInputMode(mode);
            state.SetControllerType(controllerType);

            var generator = new SimpleMapGenerator();
            var renderer = new MapRenderer();
            var loop = new GameLoop(state, generator, renderer);

            loop.RunAsync().GetAwaiter().GetResult();

            AnsiConsole.MarkupLine("[grey]Thanks for playing![/]");
        }
        finally
        {
            Console.CursorVisible = prevCursor;
        }
    }

    private static Party CharacterCreation(InputMode mode, ControllerType controllerType)
    {
        var members = new List<IActor>();
        const int maxPartySize = 4;
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

            // Build dynamic options based on party state
            var options = new List<string>();
            if (members.Count < maxPartySize) options.Add("Add Member");
            
            if (members.Count > 0)
            {
                if (members.Count >= maxPartySize)
                {
                    options.Add("Finish");
                }
                options.Add("Remove Member");
                if (members.Count > 1) options.Add("Set Leader");
            }

            var choice = PromptNavigator.PromptChoice(
                "Choose an option:",
                options,
                mode,
                controllerType);

            if (choice == "Add Member")
            {
                if (members.Count >= maxPartySize)
                {
                    AnsiConsole.MarkupLine($"[red]Party is full (max {maxPartySize}).[/]");
                    InputWaiter.WaitForAny(mode);
                    continue;
                }
                var actor = CreateActor($"Member{members.Count+1}", mode, controllerType);
                members.Add(actor);
            }
            else if (choice == "Remove Member")
            {
                if (members.Count == 0) continue;
                var name = PromptNavigator.PromptChoice(
                    "Remove who?",
                    members.Select(m => m.Name).ToList(),
                    mode,
                    controllerType);
                members.RemoveAll(m => m.Name == name);
            }
            else if (choice == "Set Leader")
            {
                if (members.Count < 2) continue;
                var name = PromptNavigator.PromptChoice(
                    "Set who as leader?",
                    members.Select(m => m.Name).ToList(),
                    mode,
                    controllerType);
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
                    InputWaiter.WaitForAny(mode);
                    continue;
                }
                var party = new Party(members[0]);
                for (int i = 1; i < members.Count; i++) party.TryAdd(members[i]);
                return party;
            }
        }
    }

    private static PlayerActor CreateActor(string defaultLabel, InputMode mode, ControllerType controllerType)
    {
        var name = AnsiConsole.Ask<string>($"Enter name for {defaultLabel}:", defaultLabel);

        var species = PromptNavigator.PromptChoice(
            $"Select [yellow]species[/] for {name}:",
            Enum.GetValues<Species>().ToList(),
            mode,
            controllerType);

        var baseClass = PromptNavigator.PromptChoice(
            $"Select base [yellow]class[/] for {name}:",
            Enum.GetValues<ActorClass>().ToList(),
            mode,
            controllerType);

        var profile = new PlayerProfile(name);
        // Build combined starting stats from species + class
        var stats = ActorStatPresets.Combine(species, baseClass);
        var actor = new PlayerActor(name, species, baseClass, profile, 0, 0, stats);
        return actor;
    }
}

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
using Bogus;
using UnidecodeSharpFork;

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

            // Difficulty selection
            var chosenDifficulty = PromptNavigator.PromptChoice(
                "Choose difficulty:",
                Enum.GetValues<Difficulty>().ToList(),
                mode, controllerType);

            // Setup method selection
            var setup = PromptNavigator.PromptChoice(
                "Setup:",
                new List<string> { "Manual Setup", "Quick Start - Single Random", "Quick Start - Full Random" },
                mode, controllerType);

            Party party;
            if (setup.StartsWith("Quick Start"))
            {
                var rng = new System.Random();
                if (setup.Contains("Single"))
                {
                    var actor = CreateRandomActor(rng);
                    party = new Party(actor);
                }
                else // Full Random = party of up to 4
                {
                    var a1 = CreateRandomActor(rng);
                    party = new Party(a1);
                    for (int i = 0; i < 3; i++) party.TryAdd(CreateRandomActor(rng));
                }
            }
            else
            {
                party = CharacterCreation(mode, controllerType);
            }

            var state = new GameState(party);
            state.SetInputMode(mode);
            state.SetControllerType(controllerType);
            state.SetDifficulty(chosenDifficulty);

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
            options.Add("Add Random Member");
            if (members.Count > 0)
            {
                if (members.Count >= maxPartySize)
                {
                    options.Add("Finish");
                }
                options.Add("Remove Member");
                if (members.Count > 1) options.Add("Set Leader");
                // Always allow finishing once we have at least one member
                if (!options.Contains("Finish")) options.Add("Finish");
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
            else if (choice == "Add Random Member")
            {
                if (members.Count >= maxPartySize)
                {
                    AnsiConsole.MarkupLine($"[red]Party is full (max {maxPartySize}).[/]");
                    InputWaiter.WaitForAny(mode);
                    continue;
                }
                members.Add(CreateRandomActor(new System.Random()));
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
        string name;
        if (mode == InputMode.Controller)
        {
            // Controller-friendly name selection
            var pick = PromptNavigator.PromptChoice(
                $"Name for {defaultLabel}:",
                new List<string> { $"Use Suggested ({defaultLabel})", "Randomize Name" },
                mode,
                controllerType);

            if (pick.StartsWith("Use Suggested"))
            {
                name = defaultLabel;
            }
            else
            {
                name = GenerateRandomName(new System.Random());
            }
        }
        else
        {
            name = AnsiConsole.Ask<string>($"Enter name for {defaultLabel}:", defaultLabel);
        }

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

    private static PlayerActor CreateRandomActor(System.Random rng)
    {
        string name = GenerateRandomName(rng);
        var species = Enum.GetValues<Species>()[rng.Next(Enum.GetValues<Species>().Length)];
        var baseClass = Enum.GetValues<ActorClass>()[rng.Next(Enum.GetValues<ActorClass>().Length)];
        var profile = new PlayerProfile(name);
        var stats = ActorStatPresets.Combine(species, baseClass);
        return new PlayerActor(name, species, baseClass, profile, 0, 0, stats);
    }

    private static string GenerateRandomName(System.Random rng)
    {
        // Pick a locale and generate a first/last name
        var locales = new[] { "en", "en_US", "en_GB" };
        var locale = locales[rng.Next(locales.Length)];

        // Seed Bogus from our RNG for reproducibility
        Randomizer.Seed = new Random(rng.Next());

        var faker = new Faker(locale);
        string first = faker.Name.FirstName();
        string last = faker.Name.LastName();

        // Transliterate to ASCII and clean
        first = first.Unidecode();
        last = last.Unidecode();

        static string Clean(string s)
        {
            
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (char.IsLetter(ch) || ch == '\'' || ch == '-' || ch == ' ') sb.Append(ch);
            }
            return sb.ToString().Trim();
        }

        first = Clean(first);
        last = Clean(last);

        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last))
        {
            // Simple fallback (100 common American / Japanese / Korean first and last names)
            string[] fallbackFirst = {
                // American (34)
                "James","John","Robert","Michael","William","David","Richard","Joseph","Thomas","Charles",
                "Christopher","Daniel","Matthew","Anthony","Mark","Donald","Steven","Paul","Andrew","Joshua",
                "Mary","Patricia","Jennifer","Linda","Elizabeth","Barbara","Susan","Jessica","Sarah","Karen",
                "Nancy","Lisa","Margaret","Emily",
                // // Japanese (33)
                // "Haruto","Yuto","Sota","Yuki","Hayato","Ryota","Kaito","Ren","Yuma","Hiroshi",
                // "Takumi","Daiki","Shota","Kenta","Riku","Sora","Itsuki","Kazuya","Tsubasa","Rei",
                // "Yui","Sakura","Yuna","Mei","Aoi","Hana","Riko","Mio","Akari","Nanami","Emi","Ayaka","Kaori",
                // // Korean (33)
                // "Min-jun","Seo-yeon","Ji-ho","Ji-eun","Min-seo","Soo-jin","Joon","Hyun","Hye-jin","Su-min",
                // "Eun-ji","Ji-won","Hyeon","Seung","Young","Mi-sun","Dong","Sang","Bo-mi","Ye-jin",
                // "In-woo","Hyeon-su","Seok","Chan","Min","So-hee","Da-bin","Ji-hyun","Jae","Soo","Ha-neul","Se-ra","Eun"
            };

            string[] fallbackLast = {
                // American (50)
                "Smith","Johnson","Williams","Brown","Jones","Garcia","Miller","Davis","Rodriguez","Martinez",
                "Hernandez","Lopez","Gonzalez","Wilson","Anderson","Thomas","Taylor","Moore","Jackson","Martin",
                "Lee","Perez","Thompson","White","Harris","Sanchez","Clark","Ramirez","Lewis","Robinson",
                "Walker","Young","Allen","King","Wright","Scott","Torres","Nguyen","Hill","Flores",
                "Green","Adams","Nelson","Baker","Hall","Rivera","Campbell","Mitchell","Carter","Roberts",
                // // Japanese (25)
                // "Sato","Suzuki","Takahashi","Tanaka","Watanabe","Ito","Yamamoto","Nakamura","Kobayashi","Kato",
                // "Yoshida","Yamada","Sasaki","Yamaguchi","Matsumoto","Inoue","Kimura","Hayashi","Shimizu","Nakajima",
                // "Ishikawa","Nakagawa","Fujita","Okada","Hasegawa",
                // // Korean (25)
                // "Kim","Park","Choi","Jung","Kang","Cho","Yoon","Jang","Lim","Han",
                // "Oh","Seo","Shin","Kwon","Hwang","Ahn","Song","Jeong","Yu","Ryu",
                // "Bae","Moon","Lee","Nam","Koo"
            };

            first = fallbackFirst[rng.Next(fallbackFirst.Length)];
            last = fallbackLast[rng.Next(fallbackLast.Length)];
        }

        return $"{first} {last}";
    }
}

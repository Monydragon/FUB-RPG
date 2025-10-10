using System;
using Spectre.Console;
// Added
// Added for concrete populator/config
using Fub.Interfaces.Parties; // Added
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic; // Added
using System.Linq;
using Fub.Enums;
using Fub.Implementations.Actors;
using Fub.Implementations.Combat;
using Fub.Implementations.Generation;
using Fub.Implementations.Items.Weapons;
using Fub.Implementations.Rendering;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Game;
using Fub.Interfaces.Generation;
using Fub.Interfaces.Items.Weapons;
using Fub.Interfaces.Map; // Added
// For casting ActorBase
// Added for combat resolver
// Added

// Added for ClassWeaponMappings

namespace Fub.Implementations.Game;

public sealed class GameLoop : IGameLoop
{
    private readonly GameState _state;
    private readonly IMapGenerator _mapGenerator;
    private readonly MapRenderer _renderer;
    private readonly TurnBasedCombatResolver _combatResolver; // Changed to turn-based
    private bool _running;
    public bool IsRunning => _running;

    public GameLoop(GameState state, IMapGenerator mapGenerator, MapRenderer renderer)
    {
        _state = state;
        _mapGenerator = mapGenerator;
        _renderer = renderer;
        _combatResolver = new TurnBasedCombatResolver(); // Changed to turn-based
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _running = true;
        while (_running && !cancellationToken.IsCancellationRequested)
        {
            switch (_state.Phase)
            {
                case GamePhase.MainMenu:
                    ShowMainMenu();
                    break;
                case GamePhase.Exploring:
                    ExplorationLoop();
                    break;
                case GamePhase.GameOver:
                    AnsiConsole.MarkupLine("[red]Game Over[/]");
                    _running = false;
                    break;
                default:
                    _state.SetPhase(GamePhase.MainMenu);
                    break;
            }
        }
        await Task.CompletedTask;
    }

    private void ShowMainMenu()
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold cyan]Main Menu[/]")
                .PageSize(10)
                .AddChoices("New Game", "Quit"));

        if (choice == "Quit")
        {
            _state.SetPhase(GamePhase.GameOver);
            return;
        }

        if (choice == "New Game")
        {
            StartNewGame();
        }
    }

    private void StartNewGame()
    {
        // Create a world with multiple maps
        var world = new World("Adventure Realm");
        _state.SetWorld(world);

        // Generate starting town map
        var townCfg = new MapGenerationConfig 
        { 
            Width = 30, 
            Height = 20,
            Theme = MapTheme.Dungeon, 
            Kind = MapKind.Town,
            MinRooms = 3,
            MaxRooms = 5
        };
        var townSeed = new ProceduralSeed(townCfg.RandomSeed);
        var townMap = _mapGenerator.Generate(townCfg, townSeed);
        world.AddMap(townMap);
        _state.SetMap(townMap);

        // Generate forest map
        var forestCfg = new MapGenerationConfig 
        { 
            Width = 40, 
            Height = 30,
            Theme = MapTheme.Forest,
            Kind = MapKind.Overworld,
            MinRooms = 5,
            MaxRooms = 8
        };
        var forestSeed = new ProceduralSeed(forestCfg.RandomSeed + 1000);
        var forestMap = _mapGenerator.Generate(forestCfg, forestSeed);
        world.AddMap(forestMap);

        // Generate dungeon map
        var dungeonCfg = new MapGenerationConfig 
        { 
            Width = 50, 
            Height = 40,
            Theme = MapTheme.Dungeon,
            Kind = MapKind.Dungeon,
            MinRooms = 8,
            MaxRooms = 12
        };
        var dungeonSeed = new ProceduralSeed(dungeonCfg.RandomSeed + 2000);
        var dungeonMap = _mapGenerator.Generate(dungeonCfg, dungeonSeed);
        world.AddMap(dungeonMap);

        // Set up map connections
        world.AddMapConnection(townMap.Id, "North Exit", forestMap.Id, 5, 5);
        world.AddMapConnection(forestMap.Id, "South Exit", townMap.Id, 5, 5);
        world.AddMapConnection(forestMap.Id, "Cave Entrance", dungeonMap.Id, 10, 10);
        world.AddMapConnection(dungeonMap.Id, "Exit", forestMap.Id, 20, 15);

        // Place party on starting map
        var map = townMap;
        var leader = _state.Party.Leader;
        (int px, int py) = (0, 0);
        bool placedLeader = false;
        for (int y = 0; y < map.Height && !placedLeader; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (map.GetTile(x, y).TileType == MapTileType.Floor)
                {
                    ((ActorBase)leader).Teleport(x, y);
                    px = x; py = y;
                    placedLeader = true;
                    break;
                }
            }
        }

        // Arrange other party members around the leader
        var offsets = new (int dx, int dy)[] { (1,0), (0,1), (-1,0), (0,-1), (1,1), (-1,1), (1,-1), (-1,-1) };
        int idx = 0;
        foreach (var member in _state.Party.Members)
        {
            if (member.Id == leader.Id) continue;
            for (int i = 0; i < offsets.Length; i++)
            {
                int nx = px + offsets[(idx + i) % offsets.Length].dx;
                int ny = py + offsets[(idx + i) % offsets.Length].dy;
                if (map.InBounds(nx, ny) && map.GetTile(nx, ny).TileType == MapTileType.Floor)
                {
                    ((ActorBase)member).Teleport(nx, ny);
                    idx += 1;
                    break;
                }
            }
        }

        // Give starting weapons to party members
        GiveStartingWeapons();

        // Set movement validator for all members
        foreach (var member in _state.Party.Members)
        {
            member.SetMovementValidator((x, y) =>
                map.InBounds(x, y) && map.GetTile(x, y).TileType == MapTileType.Floor);
        }

        // Populate each map with content
        PopulateMap(townMap, 2, 1, 3);
        PopulateMap(forestMap, 6, 2, 5);
        PopulateMap(dungeonMap, 10, 1, 8);

        _state.SetPhase(GamePhase.Exploring);
    }

    private void GiveStartingWeapons()
    {
        foreach (var member in _state.Party.Members)
        {
            if (member is ActorBase actorBase)
            {
                var weapon = CreateStarterWeaponForClass(member.Class);
                actorBase.Inventory.TryAdd(weapon, 1);
                actorBase.TryEquip(weapon, out _);
            }
        }
    }

    private static Weapon CreateStarterWeaponForClass(ActorClass cls)
    {
        var spec = ClassWeaponMappings.GetStarterSpec(cls);
        return new Weapon(
            spec.name,
            spec.type,
            spec.dmg,
            4, 8, 1.0,
            RarityTier.Common,
            EquipmentSlot.MainHand,
            requiredLevel: 1,
            allowedClasses: new [] { cls },
            statRequirements: null,
            tier: EquipmentTier.Simple
        );
    }

    private void PopulateMap(IMap map, int enemyCount, int npcCount, int itemCount)
    {
        var populator = new MapContentPopulator();
        var contentConfig = new MapContentConfig
        {
            EnemyCount = enemyCount,
            NpcCount = npcCount,
            ItemCount = itemCount,
            MinDistanceFromLeader = 5,
            Seed = null
        };
        populator.Populate(map, _state.Party, contentConfig);
    }

    private void ExplorationLoop()
    {
        if (_state.CurrentMap is null)
        {
            _state.SetPhase(GamePhase.MainMenu);
            return;
        }
        
        var map = _state.CurrentMap;
        var leader = _state.Party.Leader;
        
        // Render game with persistent UI
        RenderGameWithUI(map, leader);
        
        // Get raw keyboard input
        var key = Console.ReadKey(true);
        
        // Movement with WASD or Arrow Keys
        if (key.Key == ConsoleKey.W || key.Key == ConsoleKey.UpArrow)
        {
            if (TryMoveParty(0, -1))
                CheckForAutoInteractions(map);
        }
        else if (key.Key == ConsoleKey.S || key.Key == ConsoleKey.DownArrow)
        {
            if (TryMoveParty(0, 1))
                CheckForAutoInteractions(map);
        }
        else if (key.Key == ConsoleKey.A || key.Key == ConsoleKey.LeftArrow)
        {
            if (TryMoveParty(-1, 0))
                CheckForAutoInteractions(map);
        }
        else if (key.Key == ConsoleKey.D || key.Key == ConsoleKey.RightArrow)
        {
            if (TryMoveParty(1, 0))
                CheckForAutoInteractions(map);
        }
        // Hotkeys for menus and actions
        else if (key.Key == ConsoleKey.I)
        {
            ShowInventoryMenu();
        }
        else if (key.Key == ConsoleKey.P)
        {
            ShowPartyMenu();
        }
        else if (key.Key == ConsoleKey.M)
        {
            ShowWorldMapMenu();
        }
        else if (key.Key == ConsoleKey.Spacebar || key.Key == ConsoleKey.E)
        {
            HandleContextAction(map);
        }
        else if (key.Key == ConsoleKey.R)
        {
            SearchRoom(map);
        }
        else if (key.Key == ConsoleKey.Escape)
        {
            if (ConfirmExitToMenu())
                _state.SetPhase(GamePhase.MainMenu);
        }
        else if (key.Key == ConsoleKey.H || key.Key == ConsoleKey.F1)
        {
            ShowHelpScreen();
        }

        _state.IncrementTurn();
    }

    private void RenderGameWithUI(IMap map, IActor leader)
    {
        Console.Clear();
        _renderer.Render(map, _state.Party);
        
        // Status bar with leader info
        var health = leader.GetStat(StatType.Health);
        var mana = leader.GetStat(StatType.Mana);
        
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
        
        var statusGrid = new Grid();
        statusGrid.AddColumn(new GridColumn().Width(25));
        statusGrid.AddColumn(new GridColumn().Width(25));
        statusGrid.AddColumn(new GridColumn().Width(25));
        
        statusGrid.AddRow(
            $"[red]❤️  HP:[/] [white]{health.Current:F0}/{health.Modified:F0}[/]",
            $"[cyan]⚡ Class:[/] [yellow]{leader.EffectiveClass}[/]",
            $"[green]⭐ Lv:[/] [white]{leader.Level}[/]"
        );
        
        statusGrid.AddRow(
            $"[blue]💧 MP:[/] [white]{mana.Current:F0}/{mana.Modified:F0}[/]",
            $"[grey]📍 Map:[/] [white]{map.Name ?? "Unknown"}[/]",
            $"[grey]🔄 Turn:[/] [white]{_state.TurnNumber}[/]"
        );
        
        AnsiConsole.Write(statusGrid);
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
        
        // Context-aware action hints
        DisplayContextActions(map, leader);
    }

    private void DisplayContextActions(IMap map, IActor leader)
    {
        var objectsHere = map.GetObjectsAt(leader.X, leader.Y);
        var actions = new List<string>();
        
        // Movement is always available
        actions.Add("[cyan]WASD/Arrows[/] Move");
        
        // Context-specific actions
        var items = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Item).ToList();
        var enemies = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Enemy).ToList();
        var npcs = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Npc).ToList();
        
        if (enemies.Any())
            actions.Add("[red]SPACE[/] Attack");
        else if (items.Any())
            actions.Add("[yellow]SPACE[/] Pick Up");
        else if (npcs.Any())
            actions.Add("[blue]SPACE[/] Talk");
        else
            actions.Add("[grey]R[/] Search");
        
        // Always available hotkeys
        actions.Add("[green]I[/] Inventory");
        actions.Add("[magenta]P[/] Party");
        actions.Add("[yellow]M[/] Map");
        actions.Add("[grey]H[/] Help");
        actions.Add("[red]ESC[/] Menu");
        
        AnsiConsole.MarkupLine(string.Join(" [grey]|[/] ", actions));
    }

    private bool TryMoveParty(int dx, int dy)
    {
        var leader = _state.Party.Leader;
        int newX = leader.X + dx;
        int newY = leader.Y + dy;
        
        // Check if movement is valid
        if (!_state.CurrentMap!.InBounds(newX, newY))
            return false;
        
        if (_state.CurrentMap.GetTile(newX, newY).TileType != MapTileType.Floor)
            return false;
        
        // Move all party members
        foreach (var member in _state.Party.Members)
        {
            member.TryMove(dx, dy);
        }
        
        return true;
    }

    private void CheckForAutoInteractions(IMap map)
    {
        var leader = _state.Party.Leader;
        var objectsHere = map.GetObjectsAt(leader.X, leader.Y);
        
        // Auto-pickup items
        var items = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Item).ToList();
        if (items.Any())
        {
            foreach (var itemObj in items)
            {
                if (itemObj.Item != null && leader.Inventory.TryAdd(itemObj.Item, 1))
                {
                    map.RemoveObject(itemObj.Id);
                    AnsiConsole.MarkupLine($"[green]✓ Picked up {itemObj.Item.Name}![/]");
                    Thread.Sleep(300); // Brief pause to show pickup
                }
            }
        }
    }

    private void HandleContextAction(IMap map)
    {
        var leader = _state.Party.Leader;
        var objectsHere = map.GetObjectsAt(leader.X, leader.Y);
        
        var enemies = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Enemy).ToList();
        var npcs = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Npc).ToList();
        var items = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Item).ToList();
        
        // Priority: Combat > NPCs > Items > Search
        if (enemies.Any())
        {
            EngageCombat(map, enemies);
        }
        else if (npcs.Any())
        {
            TalkToNpc(npcs);
        }
        else if (items.Any())
        {
            PickupItem(map, items);
        }
        else
        {
            SearchRoom(map);
        }
    }

    private void ShowHelpScreen()
    {
        Console.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]⚔️  Game Controls  ⚔️[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();
        
        var helpTable = new Table();
        helpTable.Border(TableBorder.Rounded);
        helpTable.AddColumn(new TableColumn("[bold]Key[/]").Centered());
        helpTable.AddColumn(new TableColumn("[bold]Action[/]"));
        
        helpTable.AddRow("[cyan]W / ↑[/]", "Move North");
        helpTable.AddRow("[cyan]S / ↓[/]", "Move South");
        helpTable.AddRow("[cyan]A / ←[/]", "Move West");
        helpTable.AddRow("[cyan]D / →[/]", "Move East");
        helpTable.AddEmptyRow();
        helpTable.AddRow("[yellow]SPACE / E[/]", "Interact (Attack/Talk/Pick up)");
        helpTable.AddRow("[grey]R[/]", "Search current location");
        helpTable.AddEmptyRow();
        helpTable.AddRow("[green]I[/]", "Open Inventory");
        helpTable.AddRow("[magenta]P[/]", "Party Menu");
        helpTable.AddRow("[yellow]M[/]", "World Map");
        helpTable.AddRow("[grey]H / F1[/]", "Show this help");
        helpTable.AddRow("[red]ESC[/]", "Return to Main Menu");
        
        AnsiConsole.Write(helpTable);
        
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(
            "[bold yellow]Tips:[/]\n" +
            "• Items are automatically picked up when you walk over them\n" +
            "• Your class changes based on equipped weapon\n" +
            "• Each class levels up independently\n" +
            "• Explore multiple maps connected through exits"
        ).BorderColor(Color.Yellow).Header("[bold]💡 Gameplay Tips[/]"));
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to return to game...[/]");
        Console.ReadKey(true);
    }

    private bool ConfirmExitToMenu()
    {
        return AnsiConsole.Confirm("\n[yellow]Return to main menu?[/]", false);
    }

    private void MoveParty(int dx, int dy)
    {
        TryMoveParty(dx, dy);
    }

    private void ShowPartyMenu()
    {
        var choices = new List<string> { "View Stats", "Change Leader", "Manage Equipment", "Back" };
        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("[bold cyan]Party Menu[/]")
            .AddChoices(choices));

        if (choice == "View Stats")
            ShowPartyStats();
        else if (choice == "Change Leader")
            ChangeLeader();
        else if (choice == "Manage Equipment")
            ShowEquipmentMenu();
    }

    private void ShowInventoryMenu()
    {
        var leader = _state.Party.Leader;
        var items = leader.Inventory.Slots
            .Where(s => s.Item != null)
            .Select(s => (item: s.Item!, quantity: s.Quantity))
            .ToList();

        if (!items.Any())
        {
            AnsiConsole.MarkupLine("[yellow]Inventory is empty.[/]");
            AnsiConsole.MarkupLine("Press any key to continue...");
            Console.ReadKey(true);
            return;
        }

        var itemChoices = items.Select(i => i.item.Name + " x" + i.quantity.ToString()).ToList();
        itemChoices.Add("Back");

        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("[bold cyan]Inventory[/]")
            .AddChoices(itemChoices.ToArray()));

        if (choice == "Back") return;

        // Handle item action
        var selectedItem = items.First(i => choice.StartsWith(i.item.Name, StringComparison.Ordinal));
        var actions = new List<string> { "Examine", "Back" };
        
        if (selectedItem.item is IWeapon)
            actions.Insert(0, "Equip");

        var action = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("[yellow]" + selectedItem.item.Name + "[/]")
            .AddChoices(actions.ToArray()));

        if (action == "Equip" && selectedItem.item is IWeapon weapon)
        {
            if (leader is ActorBase actorBase)
            {
                if (actorBase.TryEquip(weapon, out var replaced))
                {
                    AnsiConsole.MarkupLine("[green]Equipped " + weapon.Name + "![/]");
                    AnsiConsole.MarkupLine("[cyan]Your class is now: " + leader.EffectiveClass.ToString() + "[/]");
                    if (replaced != null)
                        AnsiConsole.MarkupLine("[grey]Unequipped " + replaced.Name + "[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Cannot equip this weapon![/]");
                }
            }
            AnsiConsole.MarkupLine("Press any key to continue...");
            Console.ReadKey(true);
        }
        else if (action == "Examine")
        {
            AnsiConsole.MarkupLine("[bold]" + selectedItem.item.Name + "[/]");
            AnsiConsole.MarkupLine("Rarity: " + selectedItem.item.Rarity.ToString());
            if (selectedItem.item is IWeapon w)
            {
                AnsiConsole.MarkupLine("Tier: " + (w is Weapon ww ? ww.Tier.ToString() : "-") );
                AnsiConsole.MarkupLine("Damage: " + w.MinDamage.ToString() + "-" + w.MaxDamage.ToString());
                AnsiConsole.MarkupLine("Weapon Type: " + w.WeaponType.ToString());
            }
            AnsiConsole.MarkupLine("Press any key to continue...");
            Console.ReadKey(true);
        }
    }

    private void ShowEquipmentMenu()
    {
        var memberNames = _state.Party.Members.Select(m => m.Name).ToList();
        memberNames.Add("Back");

        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Select party member:")
            .AddChoices(memberNames));

        if (choice == "Back") return;

        var member = _state.Party.Members.First(m => m.Name == choice);
        ShowMemberEquipment(member);
    }

    private void ShowMemberEquipment(IActor member)
    {
        if (member is not ActorBase actorBase) return;

        var table = new Table();
        table.AddColumn("Slot");
        table.AddColumn("Item");
        table.AddColumn("Job Level");

        foreach (EquipmentSlot slot in Enum.GetValues<EquipmentSlot>())
        {
            var equipped = actorBase.GetEquipped(slot);
            table.AddRow(
                slot.ToString(),
                equipped?.Name ?? "[grey]Empty[/]",
                equipped != null ? $"Lv.{member.JobSystem.GetJobLevel(member.EffectiveClass).Level}" : "-"
            );
        }

        AnsiConsole.Write(table);

        // Show job levels
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold cyan]Job Levels for {member.Name}:[/]");
        var jobTable = new Table();
        jobTable.AddColumn("Job");
        jobTable.AddColumn("Level");
        jobTable.AddColumn("Experience");

        foreach (var (jobClass, jobLevel) in member.JobSystem.JobLevels.OrderByDescending(j => j.Value.Level))
        {
            jobTable.AddRow(
                jobClass.ToString(),
                jobLevel.Level.ToString(),
                $"{jobLevel.Experience}/{jobLevel.ExperienceToNextLevel}"
            );
        }

        AnsiConsole.Write(jobTable);
        AnsiConsole.MarkupLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    private void ShowWorldMapMenu()
    {
        if (_state.CurrentWorld == null || _state.CurrentMap == null)
        {
            AnsiConsole.MarkupLine("[red]No world data available.[/]");
            Console.ReadKey(true);
            return;
        }

        AnsiConsole.MarkupLine($"[bold cyan]Current Location:[/] {_state.CurrentMap.Name ?? "Unknown"}");
        AnsiConsole.MarkupLine($"[bold cyan]World:[/] {_state.CurrentWorld.Name}");
        AnsiConsole.WriteLine();

        var maps = _state.CurrentWorld.Maps.Select(m => m.Name ?? "Unnamed Map").ToList();
        AnsiConsole.MarkupLine("[bold yellow]Available Maps:[/]");
        foreach (var mapName in maps)
        {
            var marker = mapName == _state.CurrentMap.Name ? "📍" : "  ";
            AnsiConsole.MarkupLine($"{marker} {mapName}");
        }

        AnsiConsole.MarkupLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    private void ShowPartyStats()
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddRow(
            new Markup("[bold underline]Member[/]"), 
            new Markup("[bold underline]Class[/]"), 
            new Markup("[bold underline]Level[/]"),
            new Markup("[bold underline]Health[/]")
        );
        foreach (var m in _state.Party.Members)
        {
            var health = m.GetStat(StatType.Health);
            grid.AddRow(
                new Text(m.Name), 
                new Text(m.EffectiveClass.ToString()), 
                new Text($"{m.Level}"),
                new Text($"{health.Current:F0}/{health.Modified:F0}")
            );
        }
        AnsiConsole.Write(grid);
        AnsiConsole.MarkupLine("\nPress any key to return...");
        Console.ReadKey(true);
    }

    private void ChangeLeader()
    {
        var current = _state.Party.Leader.Id;
        var options = _state.Party.Members
            .Select(m => new { m.Id, Label = m.Name + (m.Id == current ? " (Leader)" : string.Empty) })
            .ToList();
        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select new leader")
                .AddChoices(options.Select(o => o.Label)));
        var chosen = options.First(o => o.Label == selection);
        _state.Party.SetLeader(chosen.Id);
    }

    private void PickupItem(IMap map, List<IMapObject> items)
    {
        if (items.Count == 0) return;
        
        var choices = items.Select(i => i.Item?.Name ?? "Unknown Item").ToList();
        var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Pick up which item?")
            .AddChoices(choices));
        
        var itemObj = items.First(i => (i.Item?.Name ?? "Unknown Item") == selected);
        if (itemObj.Item != null && _state.Party.Leader.Inventory.TryAdd(itemObj.Item, 1))
        {
            map.RemoveObject(itemObj.Id);
            AnsiConsole.MarkupLine($"[green]Picked up {itemObj.Item.Name}![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Cannot pick up item - inventory full?[/]");
        }
        
        AnsiConsole.MarkupLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    private void TalkToNpc(List<IMapObject> npcs)
    {
        if (npcs.Count == 0) return;
        
        var npc = npcs.First();
        var npcName = npc.Actor?.Name ?? "NPC";
        
        AnsiConsole.MarkupLine($"[blue]{npcName}:[/] \"Greetings, traveler! The dungeon is dangerous. Stay safe!\"");
        AnsiConsole.MarkupLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    private void EngageCombat(IMap map, List<IMapObject> enemies)
    {
        if (enemies.Count == 0) return;
        
        var enemyActors = enemies.Where(e => e.Actor != null).Select(e => e.Actor!).ToList();
        var session = new CombatSession(_state.Party.Members, enemyActors);
        
        _combatResolver.BeginCombat(session);
        
        while (session.IsActive)
        {
            _combatResolver.ProcessTurn(session);
            session.UpdateOutcome();
            
            if (!session.IsActive)
            {
                _combatResolver.EndCombat(session);
                
                if (session.Outcome == CombatOutcome.Victory)
                {
                    // Award experience to all alive party members
                    long totalExp = enemyActors.Sum(e => (long)(50 * Math.Pow(e.Level, 1.5)));
                    foreach (var ally in _state.Party.Members.Where(a => a.GetStat(StatType.Health).Current > 0))
                    {
                        var currentJob = ally.EffectiveClass;
                        bool leveled = ally.JobSystem.AddExperience(currentJob, totalExp);
                        if (leveled)
                        {
                            AnsiConsole.MarkupLine($"[bold green]{ally.Name}'s {currentJob} reached level {ally.JobSystem.GetJobLevel(currentJob).Level}![/]");
                        }
                    }
                    
                    // Remove defeated enemies from map
                    foreach (var enemy in enemies)
                        map.RemoveObject(enemy.Id);
                }
                else if (session.Outcome == CombatOutcome.Defeat)
                {
                    AnsiConsole.MarkupLine("[red]Returning to main menu...[/]");
                    _state.SetPhase(GamePhase.MainMenu);
                }
            }
        }
        
        AnsiConsole.MarkupLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    private void SearchRoom(IMap _)
    {
        var rng = new System.Random();
        var found = rng.Next(100);
        
        if (found < 30)
        {
            AnsiConsole.MarkupLine("[green]You found some gold coins![/]");
        }
        else if (found < 50)
        {
            AnsiConsole.MarkupLine("[yellow]You found a hidden passage... but it's blocked.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]You search the area but find nothing of interest.[/]");
        }
        
        AnsiConsole.MarkupLine("Press any key to continue...");
        Console.ReadKey(true);
    }
}

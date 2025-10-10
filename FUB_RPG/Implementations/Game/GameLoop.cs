using Spectre.Console;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
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
using Fub.Interfaces.Map;
using Fub.Implementations.Input;
using Fub.Interfaces.Items.Weapons;

namespace Fub.Implementations.Game;

public sealed class GameLoop : IGameLoop
{
    private readonly GameState _state;
    private readonly IMapGenerator _mapGenerator;
    private readonly MapRenderer _renderer;
    private readonly TurnBasedCombatResolver _combatResolver;
    private bool _running;
    public bool IsRunning => _running;

    // Collapsible message log
    private readonly List<string> _log = new();
    private bool _logExpanded;
    private const int LogMaxEntries = 200;

    public GameLoop(GameState state, IMapGenerator mapGenerator, MapRenderer renderer)
    {
        _state = state;
        _mapGenerator = mapGenerator;
        _renderer = renderer;
        _combatResolver = new TurnBasedCombatResolver();
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
        var choice = PromptNavigator.PromptChoice(
            "[bold cyan]Main Menu[/]",
            new List<string> { "New Game", "Quit" },
            _state);

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
        var world = new World("Adventure Realm");
        _state.SetWorld(world);

        // Town
        var townCfg = new MapGenerationConfig { Width = 30, Height = 20, Theme = MapTheme.Dungeon, Kind = MapKind.Town, MinRooms = 3, MaxRooms = 5 };
        var townMap = _mapGenerator.Generate(townCfg, new ProceduralSeed(townCfg.RandomSeed));
        world.AddMap(townMap);
        _state.SetMap(townMap);

        // Forest
        var forestCfg = new MapGenerationConfig { Width = 40, Height = 30, Theme = MapTheme.Forest, Kind = MapKind.Overworld, MinRooms = 5, MaxRooms = 8 };
        var forestMap = _mapGenerator.Generate(forestCfg, new ProceduralSeed(forestCfg.RandomSeed + 1000));
        world.AddMap(forestMap);

        // Dungeon
        var dungeonCfg = new MapGenerationConfig { Width = 50, Height = 40, Theme = MapTheme.Dungeon, Kind = MapKind.Dungeon, MinRooms = 8, MaxRooms = 12 };
        var dungeonMap = _mapGenerator.Generate(dungeonCfg, new ProceduralSeed(dungeonCfg.RandomSeed + 2000));
        world.AddMap(dungeonMap);

        // Connections
        world.AddMapConnection(townMap.Id, "North Exit", forestMap.Id, 5, 5);
        world.AddMapConnection(forestMap.Id, "South Exit", townMap.Id, 5, 5);
        world.AddMapConnection(forestMap.Id, "Cave Entrance", dungeonMap.Id, 10, 10);
        world.AddMapConnection(dungeonMap.Id, "Exit", forestMap.Id, 20, 15);

        // Place party
        var map = townMap;
        var leader = _state.Party.Leader;
        (int px, int py) = (0, 0);
        bool placed = false;
        for (int y = 0; y < map.Height && !placed; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (map.GetTile(x, y).TileType == MapTileType.Floor)
                {
                    ((ActorBase)leader).Teleport(x, y);
                    (px, py) = (x, y);
                    placed = true;
                    break;
                }
            }
        }

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
                    idx++;
                    break;
                }
            }
        }

        // Starter gear
        GiveStartingWeapons();

        foreach (var member in _state.Party.Members)
            member.SetMovementValidator((x, y) => map.InBounds(x, y) && map.GetTile(x, y).TileType == MapTileType.Floor);

        // Populate
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
        return new Weapon(spec.name, spec.type, spec.dmg, 4, 8, 1.0,
            RarityTier.Common, EquipmentSlot.MainHand, requiredLevel: 1,
            allowedClasses: new[] { cls }, statRequirements: null, tier: EquipmentTier.Simple);
    }

    private void PopulateMap(IMap map, int enemyCount, int npcCount, int itemCount)
    {
        var populator = new MapContentPopulator();
        var cfg = new MapContentConfig { EnemyCount = enemyCount, NpcCount = npcCount, ItemCount = itemCount, MinDistanceFromLeader = 5, Seed = null };
        populator.Populate(map, _state.Party, cfg);
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

        // Fill console with map while reserving space for UI + log
        int reservedRows = 8;
        int logRows = _logExpanded ? Math.Min(8, _log.Count) : 1;
        int availableRows = Math.Max(3, Console.WindowHeight - reservedRows - logRows);
        int cellWidth = 10; // ~9 for cell + 1 space
        int availableCols = Math.Max(5, (Console.WindowWidth - 2) / cellWidth);
        _renderer.ViewWidth = Math.Min(map.Width, availableCols);
        _renderer.ViewHeight = Math.Min(map.Height, availableRows);

        RenderGameWithUi(map, leader);

        var action = InputManager.ReadNextAction(_state.InputMode);
        switch (action)
        {
            case InputAction.MoveUp:
                if (TryMoveParty(0, -1)) CheckForAutoInteractions(map);
                break;
            case InputAction.MoveDown:
                if (TryMoveParty(0, 1)) CheckForAutoInteractions(map);
                break;
            case InputAction.MoveLeft:
                if (TryMoveParty(-1, 0)) CheckForAutoInteractions(map);
                break;
            case InputAction.MoveRight:
                if (TryMoveParty(1, 0)) CheckForAutoInteractions(map);
                break;
            case InputAction.Inventory:
                ShowInventoryMenu();
                break;
            case InputAction.Party:
                ShowPartyMenu();
                break;
            case InputAction.Map:
                ShowWorldMapMenu();
                break;
            case InputAction.Interact:
                HandleContextAction(map);
                break;
            case InputAction.Search:
                SearchRoom(map);
                break;
            case InputAction.Help:
                ShowHelpScreen();
                break;
            case InputAction.Log:
                _logExpanded = !_logExpanded;
                break;
            case InputAction.Menu:
                if (ConfirmExitToMenu()) _state.SetPhase(GamePhase.MainMenu);
                break;
            default:
                break;
        }

        _state.IncrementTurn();
    }

    private void RenderGameWithUi(IMap map, IActor leader)
    {
        Console.Clear();
        _renderer.Render(map, _state.Party);

        var health = leader.GetStat(StatType.Health);
        var mana = leader.GetStat(StatType.Mana);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule().RuleStyle("grey"));

        var statusGrid = new Grid();
        statusGrid.AddColumn(new GridColumn().Width(25));
        statusGrid.AddColumn(new GridColumn().Width(25));
        statusGrid.AddColumn(new GridColumn().Width(25));
        statusGrid.AddRow(
            "[red]❤️  HP:[/] [white]" + health.Current.ToString("F0") + "/" + health.Modified.ToString("F0") + "[/]",
            "[cyan]⚡ Class:[/] [yellow]" + leader.EffectiveClass + "[/]",
            "[green]⭐ Lv:[/] [white]" + leader.Level + "[/]");
        statusGrid.AddRow(
            "[blue]💧 MP:[/] [white]" + mana.Current.ToString("F0") + "/" + mana.Modified.ToString("F0") + "[/]",
            "[grey]📍 Map:[/] [white]" + map.Name + "[/]",
            "[grey]🔄 Turn:[/] [white]" + _state.TurnNumber + "[/]");

        AnsiConsole.Write(statusGrid);
        AnsiConsole.Write(new Rule().RuleStyle("grey"));

        RenderLog();
        DisplayContextActions(map, leader);
    }

    private void RenderLog()
    {
        if (_log.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]Log: (empty)  [/]" + ToggleLogHint());
            return;
        }

        if (_logExpanded)
        {
            int rows = Math.Min(8, _log.Count);
            var slice = _log.Skip(Math.Max(0, _log.Count - rows)).ToList();
            var panel = new Panel(string.Join(Environment.NewLine, slice)).Header("[bold]Log[/]").BorderColor(Color.Grey);
            AnsiConsole.Write(panel);
            AnsiConsole.MarkupLine(ToggleLogHint());
        }
        else
        {
            var last = _log[^1];
            AnsiConsole.MarkupLine($"[grey]Log:[/] {last}  {ToggleLogHint()}");
        }
    }

    private string ToggleLogHint()
    {
        if (_state.InputMode == InputMode.Controller)
        {
            var c = _state.ControllerType;
            return "[grey](Toggle Log " + ControllerUi.Log(c) + ")[/]";
        }
        return "[grey](Toggle Log: L)[/]";
    }

    private void AddLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _log.Add(message);
        if (_log.Count > LogMaxEntries)
            _log.RemoveRange(0, _log.Count - LogMaxEntries);
    }

    private bool ConfirmExitToMenu()
    {
        var answer = PromptNavigator.PromptChoice(
            "Return to main menu?",
            new[] { "No", "Yes" },
            _state);
        return answer == "Yes";
    }

    private void ShowPartyMenu()
    {
        var choices = new List<string> { "View Stats", "Change Leader", "Manage Equipment", "Back" };
        var choice = PromptNavigator.PromptChoice("[bold cyan]Party Menu[/]", choices, _state);
        if (choice == "View Stats") ShowPartyStats();
        else if (choice == "Change Leader") ChangeLeader();
        else if (choice == "Manage Equipment") ShowEquipmentMenu();
    }

    private void ShowInventoryMenu()
    {
        var leader = _state.Party.Leader;
        var items = leader.Inventory.Slots.Where(s => s.Item != null).Select(s => (item: s.Item!, quantity: s.Quantity)).ToList();
        if (!items.Any())
        {
            AnsiConsole.MarkupLine("[yellow]Inventory is empty.[/]");
            AnsiConsole.MarkupLine("Press any key to continue...");
            InputWaiter.WaitForAny(_state.InputMode);
            return;
        }

        var itemChoices = items.Select(i => i.item.Name + " x" + i.quantity.ToString()).ToList();
        itemChoices.Add("Back");
        var choice = PromptNavigator.PromptChoice("[bold cyan]Inventory[/]", itemChoices, _state);
        if (choice == "Back") return;

        var selectedItem = items.First(i => choice.StartsWith(i.item.Name, StringComparison.Ordinal));
        var actions = new List<string> { "Examine", "Back" };
        if (selectedItem.item is IWeapon) actions.Insert(0, "Equip");
        var action = PromptNavigator.PromptChoice("[yellow]" + selectedItem.item.Name + "[/]", actions, _state);

        if (action == "Equip" && selectedItem.item is IWeapon weapon)
        {
            if (leader is ActorBase actorBase)
            {
                if (actorBase.TryEquip(weapon, out var replaced))
                {
                    AnsiConsole.MarkupLine("[green]Equipped " + weapon.Name + "![/]");
                    AnsiConsole.MarkupLine("[cyan]Your class is now: " + leader.EffectiveClass + "[/]");
                    if (replaced != null) AnsiConsole.MarkupLine("[grey]Unequipped " + replaced.Name + "[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Cannot equip that right now.[/]");
                }
            }
            AnsiConsole.MarkupLine("Press any key to continue...");
            InputWaiter.WaitForAny(_state.InputMode);
        }
        else if (action == "Examine")
        {
            AnsiConsole.MarkupLine("[bold]" + selectedItem.item.Name + "[/]");
            AnsiConsole.MarkupLine("Rarity: " + selectedItem.item.Rarity);
            if (selectedItem.item is IWeapon w)
            {
                AnsiConsole.MarkupLine("Weapon Type: " + w.WeaponType);
                if (w is Weapon ww) AnsiConsole.MarkupLine("Tier: " + ww.Tier);
            }
            AnsiConsole.MarkupLine("Press any key to continue...");
            InputWaiter.WaitForAny(_state.InputMode);
        }
    }

    private void ShowEquipmentMenu()
    {
        var memberNames = _state.Party.Members.Select(m => m.Name).ToList();
        memberNames.Add("Back");
        var choice = PromptNavigator.PromptChoice("Select party member:", memberNames, _state);
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
        foreach (EquipmentSlot slot in System.Enum.GetValues<EquipmentSlot>())
        {
            var equipped = actorBase.GetEquipped(slot);
            table.AddRow(
                slot.ToString(),
                equipped?.Name ?? "[grey]Empty[/]",
                equipped != null ? $"Lv.{member.JobSystem.GetJobLevel(member.EffectiveClass).Level}" : "-");
        }
        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold cyan]Job Levels for {member.Name}:[/]");
        var jobTable = new Table();
        jobTable.AddColumn("Job");
        jobTable.AddColumn("Level");
        jobTable.AddColumn("Experience");
        foreach (var (jobClass, jobLevel) in member.JobSystem.JobLevels.OrderByDescending(j => j.Value.Level))
        {
            jobTable.AddRow(jobClass.ToString(), jobLevel.Level.ToString(), $"{jobLevel.Experience}/{jobLevel.ExperienceToNextLevel}");
        }
        AnsiConsole.Write(jobTable);
        AnsiConsole.MarkupLine("\nPress any key to continue...");
        InputWaiter.WaitForAny(_state.InputMode);
    }

    private void ShowWorldMapMenu()
    {
        if (_state.CurrentWorld == null || _state.CurrentMap == null)
        {
            AnsiConsole.MarkupLine("[red]No world data available.[/]");
            InputWaiter.WaitForAny(_state.InputMode);
            return;
        }
        AnsiConsole.MarkupLine($"[bold cyan]Current Location:[/] {_state.CurrentMap.Name}");
        AnsiConsole.MarkupLine($"[bold cyan]World:[/] {_state.CurrentWorld.Name}");
        AnsiConsole.WriteLine();
        var maps = _state.CurrentWorld.Maps.Select(m => m.Name).ToList();
        AnsiConsole.MarkupLine("[bold yellow]Available Maps:[/]");
        foreach (var mapName in maps)
        {
            var marker = mapName == _state.CurrentMap.Name ? "📍" : "  ";
            AnsiConsole.MarkupLine($"{marker} {mapName}");
        }
        AnsiConsole.MarkupLine("\nPress any key to continue...");
        InputWaiter.WaitForAny(_state.InputMode);
    }

    private void ShowPartyStats()
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddRow(new Markup("[bold underline]Member[/]"), new Markup("[bold underline]Class[/]"), new Markup("[bold underline]Level[/]"), new Markup("[bold underline]Health[/]"));
        foreach (var m in _state.Party.Members)
        {
            var health = m.GetStat(StatType.Health);
            grid.AddRow(new Text(m.Name), new Text(m.EffectiveClass.ToString()), new Text($"{m.Level}"), new Text($"{health.Current:F0}/{health.Modified:F0}"));
        }
        AnsiConsole.Write(grid);
        AnsiConsole.MarkupLine("\nPress any key to return...");
        InputWaiter.WaitForAny(_state.InputMode);
    }

    private void ChangeLeader()
    {
        var current = _state.Party.Leader.Id;
        var options = _state.Party.Members.Select(m => new { m.Id, Label = m.Name + (m.Id == current ? " (Leader)" : string.Empty) }).ToList();
        var selection = PromptNavigator.PromptChoice("Select new leader", options.Select(o => o.Label).ToList(), _state);
        var chosen = options.First(o => o.Label == selection);
        _state.Party.SetLeader(chosen.Id);
    }

    private void CheckForAutoInteractions(IMap map)
    {
        var leader = _state.Party.Leader;
        var objectsHere = map.GetObjectsAt(leader.X, leader.Y);
        var items = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Item).ToList();
        if (items.Any())
        {
            foreach (var itemObj in items)
            {
                if (itemObj.Item != null && leader.Inventory.TryAdd(itemObj.Item, 1))
                {
                    map.RemoveObject(itemObj.Id);
                    AddLog($"Picked up {itemObj.Item.Name}.");
                }
            }
        }
    }

    private void PickupItem(IMap map, List<IMapObject> items)
    {
        if (items.Count == 0) return;
        var choices = items.Select(i => i.Item?.Name ?? "Unknown Item").ToList();
        var selected = PromptNavigator.PromptChoice("Pick up which item?", choices, _state);
        var itemObj = items.First(i => (i.Item?.Name ?? "Unknown Item") == selected);
        if (itemObj.Item != null && _state.Party.Leader.Inventory.TryAdd(itemObj.Item, 1))
        {
            map.RemoveObject(itemObj.Id);
            AddLog($"Picked up {itemObj.Item.Name}.");
        }
        else
        {
            AddLog("Couldn't pick that up.");
        }
    }

    private void TalkToNpc(List<IMapObject> npcs)
    {
        if (npcs.Count == 0) return;
        var npc = npcs.First();
        var npcName = npc.Actor?.Name ?? "NPC";
        AddLog($"{npcName}: \"Greetings, traveler! The dungeon is dangerous. Stay safe!\"");
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
        }
        _combatResolver.EndCombat(session);
        if (session.Outcome == CombatOutcome.Victory)
        {
            long totalExp = enemyActors.Sum(e => (long)(50 * System.Math.Pow(e.Level, 1.5)));
            foreach (var ally in _state.Party.Members.Where(a => a.GetStat(StatType.Health).Current > 0))
            {
                var currentJob = ally.EffectiveClass;
                bool leveled = ally.JobSystem.AddExperience(currentJob, totalExp);
                if (leveled) AddLog($"{ally.Name}'s {currentJob} reached level {ally.JobSystem.GetJobLevel(currentJob).Level}!");
            }
            foreach (var enemy in enemies) map.RemoveObject(enemy.Id);
            AddLog("Victory!");
        }
        else if (session.Outcome == CombatOutcome.Defeat)
        {
            AddLog("Defeat. Returning to main menu...");
            _state.SetPhase(GamePhase.MainMenu);
        }
    }

    private void SearchRoom(IMap _)
    {
        var rng = new System.Random();
        var found = rng.Next(100);
        if (found < 30) AddLog("You found some gold coins!");
        else if (found < 50) AddLog("You found a hidden passage... but it's blocked.");
        else AddLog("You found nothing of interest.");
    }

    private bool TryMoveParty(int dx, int dy)
    {
        var leader = _state.Party.Leader;
        int newX = leader.X + dx;
        int newY = leader.Y + dy;
        if (!_state.CurrentMap!.InBounds(newX, newY)) return false;
        if (_state.CurrentMap.GetTile(newX, newY).TileType != MapTileType.Floor) return false;
        foreach (var member in _state.Party.Members) member.TryMove(dx, dy);
        return true;
    }

    private void HandleContextAction(IMap map)
    {
        var leader = _state.Party.Leader;
        var objectsHere = map.GetObjectsAt(leader.X, leader.Y);
        var enemies = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Enemy).ToList();
        var npcs = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Npc).ToList();
        var items = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Item).ToList();
        if (enemies.Any()) EngageCombat(map, enemies);
        else if (npcs.Any()) TalkToNpc(npcs);
        else if (items.Any()) PickupItem(map, items);
        else SearchRoom(map);
    }

    private void DisplayContextActions(IMap map, IActor leader)
    {
        var objectsHere = map.GetObjectsAt(leader.X, leader.Y);
        var actions = new List<string>();
        var ctype = _state.ControllerType;
        
        if (_state.InputMode == InputMode.Controller)
        {
            actions.Add(ControllerUi.MovePad(ctype) + " Move");
        }
        else
        {
            actions.Add("[cyan]WASD/Arrows[/] Move");
        }
        
        // Context-specific actions
        var items = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Item).ToList();
        var enemies = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Enemy).ToList();
        var npcs = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Npc).ToList();
        
        if (enemies.Any())
            actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " Attack" : "[red]SPACE[/] Attack");
        else if (items.Any())
            actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " Pick Up" : "[yellow]SPACE[/] Pick Up");
        else if (npcs.Any())
            actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " Talk" : "[blue]SPACE[/] Talk");
        else
            actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Cancel(ctype) + " Search" : "[grey]R[/] Search");
        
        // Always-available hotkeys
        if (_state.InputMode == InputMode.Controller)
        {
            actions.Add(ControllerUi.Inventory(ctype) + " Inventory");
            actions.Add(ControllerUi.Party(ctype) + " Party");
            actions.Add(ControllerUi.Help(ctype) + " Help");
            actions.Add(ControllerUi.Menu(ctype) + " Menu");
            actions.Add(ControllerUi.Log(ctype) + " Log");
        }
        else
        {
            actions.Add("[green]I[/] Inventory");
            actions.Add("[magenta]P[/] Party");
            actions.Add("[yellow]M[/] Map");
            actions.Add("[grey]H[/] Help");
            actions.Add("[red]ESC[/] Menu");
            actions.Add("[grey]L[/] Log");
        }
        
        AnsiConsole.MarkupLine(string.Join(" [grey]|[/] ", actions));
    }

    private void ShowHelpScreen()
    {
        Console.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]\u2694\ufe0f  Game Controls  \u2694\ufe0f[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();
        
        var helpTable = new Table();
        helpTable.Border(TableBorder.Rounded);
        helpTable.AddColumn(new TableColumn("[bold]Input[/]").Centered());
        helpTable.AddColumn(new TableColumn("[bold]Action[/]"));
        var ctype = _state.ControllerType;
        
        if (_state.InputMode == InputMode.Controller)
        {
            helpTable.AddRow("[cyan]D-Pad / LS[/]", "Move");
            helpTable.AddRow(ControllerUi.Confirm(ctype), "Interact (Attack/Talk/Pick up)");
            helpTable.AddRow(ControllerUi.Cancel(ctype), "Search current location");
            helpTable.AddRow(ControllerUi.Inventory(ctype), "Open Inventory");
            helpTable.AddRow(ControllerUi.Party(ctype), "Party Menu");
            helpTable.AddRow(ControllerUi.Help(ctype), "Show Help");
            helpTable.AddRow(ControllerUi.Menu(ctype), "Return to Main Menu");
            helpTable.AddRow(ControllerUi.Log(ctype), "Toggle Log");
        }
        else
        {
            helpTable.AddRow("[cyan]W / \u2191[/]", "Move North");
            helpTable.AddRow("[cyan]S / \u2193[/]", "Move South");
            helpTable.AddRow("[cyan]A / \u2190[/]", "Move West");
            helpTable.AddRow("[cyan]D / \u2192[/]", "Move East");
            helpTable.AddEmptyRow();
            helpTable.AddRow("[yellow]SPACE / E[/]", "Interact (Attack/Talk/Pick up)");
            helpTable.AddRow("[grey]R[/]", "Search current location");
            helpTable.AddEmptyRow();
            helpTable.AddRow("[green]I[/]", "Open Inventory");
            helpTable.AddRow("[magenta]P[/]", "Party Menu");
            helpTable.AddRow("[yellow]M[/]", "World Map");
            helpTable.AddRow("[grey]H / F1[/]", "Show Help");
            helpTable.AddRow("[red]ESC[/]", "Return to Main Menu");
            helpTable.AddRow("[grey]L[/]", "Toggle Log");
        }
        
        AnsiConsole.Write(helpTable);
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to return to game...[/]");
        InputWaiter.WaitForAny(_state.InputMode);
    }
}

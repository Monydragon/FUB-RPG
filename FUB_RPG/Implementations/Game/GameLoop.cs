using Spectre.Console;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text; // Added
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
using Fub.Implementations.Map.Objects; // Added for portals

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
    private bool _uiInitialized; // Added

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

        // Generate several maps for variety
        var townCfg = new MapGenerationConfig { Width = 30, Height = 20, Theme = MapTheme.Dungeon, Kind = MapKind.Town, MinRooms = 3, MaxRooms = 5 };
        var townMap = _mapGenerator.Generate(townCfg, new ProceduralSeed(townCfg.RandomSeed));
        world.AddMap(townMap);

        var forestCfg = new MapGenerationConfig { Width = 40, Height = 30, Theme = MapTheme.Forest, Kind = MapKind.Overworld, MinRooms = 5, MaxRooms = 8 };
        var forestMap = _mapGenerator.Generate(forestCfg, new ProceduralSeed(forestCfg.RandomSeed + 1000));
        world.AddMap(forestMap);

        var dungeonCfg = new MapGenerationConfig { Width = 50, Height = 40, Theme = MapTheme.Dungeon, Kind = MapKind.Dungeon, MinRooms = 8, MaxRooms = 12 };
        var dungeonMap = _mapGenerator.Generate(dungeonCfg, new ProceduralSeed(dungeonCfg.RandomSeed + 2000));
        world.AddMap(dungeonMap);

        // Connect maps via named exits
        world.AddMapConnection(townMap.Id, "North Exit", forestMap.Id, 5, 5);
        world.AddMapConnection(forestMap.Id, "South Exit", townMap.Id, 5, 5);
        world.AddMapConnection(forestMap.Id, "Cave Entrance", dungeonMap.Id, 10, 10);
        world.AddMapConnection(dungeonMap.Id, "Exit", forestMap.Id, 20, 15);

        // Place visible portals for exits
        PlacePortal(townMap, "North Exit");
        PlacePortal(forestMap, "South Exit");
        PlacePortal(forestMap, "Cave Entrance");
        PlacePortal(dungeonMap, "Exit");

        // Choose starting map
        var startingMapName = PromptNavigator.PromptChoice(
            "Choose your starting location:",
            world.Maps.Select(m => m.Name + " (" + m.Kind + ")").ToList(),
            _state);
        var chosen = world.Maps.First(m => startingMapName.StartsWith(m.Name));
        _state.SetMap(chosen);

        // Give starting gold
        _state.Party.AddGold(50);

        // Place party on chosen map
        PlacePartyAtFirstFloor(chosen);

        // Starter gear
        GiveStartingWeapons();

        foreach (var member in _state.Party.Members)
            member.SetMovementValidator((x, y) => chosen.InBounds(x, y) && chosen.GetTile(x, y).TileType == MapTileType.Floor);

        // Populate content
        PopulateMap(townMap, 2, 1, 3);
        PopulateMap(forestMap, 6, 2, 5);
        PopulateMap(dungeonMap, 10, 1, 8);

        _state.SetPhase(GamePhase.Exploring);
    }

    private void PlacePartyAtFirstFloor(IMap map)
    {
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

        // Place all party members onto the leader's tile so the party occupies one cell
        foreach (var member in _state.Party.Members)
        {
            if (member.Id == leader.Id) continue;
            ((ActorBase)member).Teleport(px, py);
        }
    }

    private void PlacePortal(IMap map, string exitName)
    {
        // Find an appropriate floor tile based on exit name
        (int x, int y)? pos = exitName.Contains("North") ? FindEdgeFloor(map, Edge.Top)
                         : exitName.Contains("South") ? FindEdgeFloor(map, Edge.Bottom)
                         : exitName.Contains("East") ? FindEdgeFloor(map, Edge.Right)
                         : exitName.Contains("West") ? FindEdgeFloor(map, Edge.Left)
                         : FindAnyRoomCenter(map);
        if (pos.HasValue)
        {
            map.AddObject(new MapPortalObject(exitName, pos.Value.x, pos.Value.y));
        }
    }

    private enum Edge { Top, Bottom, Left, Right }

    private (int x, int y)? FindEdgeFloor(IMap map, Edge edge)
    {
        // Scan from the edge inward to find the first floor
        if (edge == Edge.Top)
        {
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (map.GetTile(x, y).TileType == MapTileType.Floor) return (x, y);
        }
        else if (edge == Edge.Bottom)
        {
            for (int y = map.Height - 1; y >= 0; y--)
                for (int x = 0; x < map.Width; x++)
                    if (map.GetTile(x, y).TileType == MapTileType.Floor) return (x, y);
        }
        else if (edge == Edge.Left)
        {
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    if (map.GetTile(x, y).TileType == MapTileType.Floor) return (x, y);
        }
        else // Right
        {
            for (int x = map.Width - 1; x >= 0; x--)
                for (int y = 0; y < map.Height; y++)
                    if (map.GetTile(x, y).TileType == MapTileType.Floor) return (x, y);
        }
        return null;
    }

    private (int x, int y)? FindAnyRoomCenter(IMap map)
    {
        foreach (var room in map.Rooms)
        {
            int cx = room.X + room.Width / 2;
            int cy = room.Y + room.Height / 2;
            if (map.InBounds(cx, cy) && map.GetTile(cx, cy).TileType == MapTileType.Floor)
                return (cx, cy);
        }
        // fallback: first floor
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.GetTile(x, y).TileType == MapTileType.Floor) return (x, y);
        return null;
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
        int reservedRows = 12; // Increased to fit party HUD
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

    private void ShowPartyMenu()
    {
        var choices = new List<string> { "View Stats", "Change Leader", "Manage Equipment", "Back" };
        var choice = PromptNavigator.PromptChoice("[bold cyan]Party Menu[/]", choices, _state);
        if (choice == "View Stats") ShowPartyStats();
        else if (choice == "Change Leader") ChangeLeader();
        else if (choice == "Manage Equipment") ShowEquipmentMenu();
        _uiInitialized = false; // redraw cleanly
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
        else
        {
            // Back
        }
        _uiInitialized = false; // redraw cleanly
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
            var marker = mapName == _state.CurrentMap.Name ? "\ud83d\udccd" : "  ";
            AnsiConsole.MarkupLine($"{marker} {mapName}");
        }
        AnsiConsole.MarkupLine("\nPress any key to continue...");
        InputWaiter.WaitForAny(_state.InputMode);
        _uiInitialized = false; // redraw cleanly
    }

    private void ShowPartyStats()
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Name");
        table.AddColumn("Species");
        table.AddColumn("Class");
        table.AddColumn("Level");
        table.AddColumn("Experience");
        table.AddColumn("HP");
        table.AddColumn("MP");
        table.AddColumn("TP");
        foreach (var m in _state.Party.Members)
        {
            var hp = m.GetStat(StatType.Health);
            var mp = m.GetStat(StatType.Mana);
            var tp = m.GetStat(StatType.Technical);
            table.AddRow(
                m.Name,
                m.Species.ToString(),
                m.EffectiveClass.ToString(),
                m.Level.ToString(),
                m.Experience.ToString(),
                $"{hp.Current:F0}/{hp.Modified:F0}",
                $"{mp.Current:F0}/{mp.Modified:F0}",
                $"{tp.Current:F0}/{tp.Modified:F0}");
        }
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("\nPress any key to return...");
        InputWaiter.WaitForAny(_state.InputMode);
        _uiInitialized = false; // redraw cleanly
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
                if (itemObj.Item != null)
                {
                    // Convert coins to gold directly
                    if (string.Equals(itemObj.Item.Name, "Coin", StringComparison.OrdinalIgnoreCase))
                    {
                        var rng = new System.Random();
                        int amount = rng.Next(1, 11);
                        _state.Party.AddGold(amount);
                        AddLog($"Picked up {amount} gold coin(s).");
                        map.RemoveObject(itemObj.Id);
                        continue;
                    }
                    if (leader.Inventory.TryAdd(itemObj.Item, 1))
                    {
                        map.RemoveObject(itemObj.Id);
                        AddLog($"Picked up {itemObj.Item.Name}.");
                    }
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
        if (itemObj.Item != null)
        {
            if (string.Equals(itemObj.Item.Name, "Coin", StringComparison.OrdinalIgnoreCase))
            {
                var rng = new System.Random();
                int amount = rng.Next(5, 26);
                _state.Party.AddGold(amount);
                map.RemoveObject(itemObj.Id);
                AddLog($"You collected {amount} gold.");
                return;
            }
            if (_state.Party.Leader.Inventory.TryAdd(itemObj.Item, 1))
            {
                map.RemoveObject(itemObj.Id);
                AddLog($"Picked up {itemObj.Item.Name}.");
            }
            else
            {
                AddLog("Couldn't pick that up.");
            }
        }
    }

    private void RenderGameWithUi(IMap map, IActor leader)
    {
        if (!_uiInitialized) { Console.Clear(); _uiInitialized = true; }

        // Layout constants
        int sepLines = 1;
        int hudLines = 2; // Summary lines (no separate leader stat line)
        int partyLines = _state.Party.MaxSize; // reserve fixed space to avoid overlap
        int maxLogLines = 6; // fixed reserve for log area
        int actionsLines = 1;

        // Compute rows
        int mapHeight = _renderer.ViewHeight;
        int row = 0;

        // Map section
        var mapText = _renderer.RenderToString(map, _state.Party);
        WriteSection(row, mapHeight, mapText);
        row += mapHeight;

        // Separator
        int sepLen = Math.Max(10, Math.Min(Console.WindowWidth - 2, _renderer.ViewWidth * 10));
        string sep = "[grey]" + new string('-', sepLen) + "[/]";
        WriteSection(row, sepLines, sep + "\n");
        row += sepLines;

        // HUD summary (no duplicate leader stat block)
        var sbHud = new StringBuilder();
        sbHud.AppendLine($"[white]Party:[/] {_state.Party.Members.Count}/{_state.Party.MaxSize}  [yellow]Gold:[/] {_state.Party.Gold}  [grey]Turn:[/] {_state.TurnNumber}");
        sbHud.AppendLine($"[grey]Map:[/] {map.Name}");
        WriteSection(row, hudLines, sbHud.ToString());
        row += hudLines;

        // Party list (unified, with leader icon)
        var sbParty = new StringBuilder();
        foreach (var m in _state.Party.Members)
        {
            string leaderIcon = m.Id == leader.Id ? "[yellow]★[/] " : "   ";
            var mh = m.GetStat(StatType.Health);
            var mm = m.GetStat(StatType.Mana);
            var mt = m.GetStat(StatType.Technical);
            sbParty.AppendLine($"{leaderIcon}{m.Name} Lv{m.Level} {m.EffectiveClass}  [red]HP[/]: {mh.Current:F0}/{mh.Modified:F0}  [blue]MP[/]: {mm.Current:F0}/{mm.Modified:F0}  [magenta]TP[/]: {mt.Current:F0}/{mt.Modified:F0}");
        }
        WriteSection(row, partyLines, sbParty.ToString());
        row += partyLines;

        // Separator before log
        WriteSection(row, sepLines, sep + "\n");
        row += sepLines;

        // Log section (reserve fixed space)
        var logText = RenderLogAsString(maxLogLines);
        WriteSection(row, maxLogLines, logText);
        row += maxLogLines;

        // Actions
        string actions = BuildContextActionsString(map, leader);
        WriteSection(row, actionsLines, actions + "\n");
        // no need to pad remainder; sections handle clearing
    }

    private void WriteSection(int startRow, int height, string markup)
    {
        try
        {
            if (height <= 0) return;
            int width = Math.Max(1, Console.WindowWidth);
            // Clear region
            for (int i = 0; i < height; i++)
            {
                if (startRow + i >= Console.WindowHeight) break;
                Console.SetCursorPosition(0, startRow + i);
                Console.Write(new string(' ', System.Math.Max(0, width - 1)));
            }
            if (startRow >= Console.WindowHeight) return;
            // Limit content to at most height lines
            var lines = (markup ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var limited = new StringBuilder();
            for (int i = 0; i < lines.Length && i < height; i++)
            {
                limited.AppendLine(lines[i]);
            }
            Console.SetCursorPosition(0, startRow);
            AnsiConsole.Write(new Markup(limited.ToString()));
        }
        catch
        {
            // In case of cursor errors (e.g., window resized), fallback to clear and full redraw next frame.
            _uiInitialized = false;
        }
    }

    private string RenderLogAsString(int maxLines)
    {
        if (_log.Count == 0)
        {
            var collapsed = "[grey]Log: (empty)[/]  " + ToggleLogHint();
            return collapsed;
        }

        if (_logExpanded)
        {
            int rows = System.Math.Min(maxLines - 1, _log.Count); // leave last line for hint when possible
            rows = System.Math.Max(1, rows);
            var slice = _log.Skip(System.Math.Max(0, _log.Count - rows)).ToList();
            var sb = new StringBuilder();
            foreach (var l in slice) sb.AppendLine(l);
            sb.Append(ToggleLogHint());
            return sb.ToString();
        }
        else
        {
            var last = _log[^1];
            return $"[grey]Log:[/] {last}  {ToggleLogHint()}";
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

    private string BuildContextActionsString(IMap map, IActor leader)
    {
        var objectsHere = map.GetObjectsAt(leader.X, leader.Y);
        var actions = new List<string>();
        var ctype = _state.ControllerType;
        if (_state.InputMode == InputMode.Controller)
            actions.Add(ControllerUi.MovePad(ctype) + " Move");
        else
            actions.Add("[cyan]WASD/Arrows[/] Move");

        var items = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Item).ToList();
        var enemies = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Enemy).ToList();
        var npcs = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Npc).ToList();
        var portals = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Portal).ToList();

        if (enemies.Any()) actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " Attack" : "[red]SPACE[/] Attack");
        else if (items.Any()) actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " Pick Up" : "[yellow]SPACE[/] Pick Up");
        else if (npcs.Any()) actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " Talk" : "[blue]SPACE[/] Talk");
        else if (portals.Any()) actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " Use Exit" : "[green]SPACE[/] Use Exit");
        else actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Cancel(ctype) + " Search" : "[grey]R[/] Search");

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
        return string.Join(" [grey]|[/] ", actions);
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
        var roll = rng.Next(100);
        if (roll < 30)
        {
            int gold = rng.Next(5, 21);
            _state.Party.AddGold(gold);
            AddLog($"You found {gold} gold coins!");
        }
        else if (roll < 50)
        {
            AddLog("You found a hidden passage... but it's blocked.");
        }
        else AddLog("You found nothing of interest.");
    }

    private bool TryMoveParty(int dx, int dy)
    {
        var leader = _state.Party.Leader;
        int newX = leader.X + dx;
        int newY = leader.Y + dy;
        if (_state.CurrentMap == null) return false;
        if (!_state.CurrentMap.InBounds(newX, newY)) return false;
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
        var portals = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Portal).ToList();
        if (enemies.Any()) EngageCombat(map, enemies);
        else if (npcs.Any()) TalkToNpc(npcs);
        else if (items.Any()) PickupItem(map, items);
        else if (portals.Any()) UsePortal(map, portals.First());
        else SearchRoom(map);
    }

    private void UsePortal(IMap currentMap, IMapObject portal)
    {
        if (_state.CurrentWorld == null) return;
        var exitName = portal.Name; // same as ExitName
        if (_state.CurrentWorld.TryGetMapConnection(currentMap.Id, exitName, out var toMapId, out var toX, out var toY))
        {
            var nextMap = _state.CurrentWorld.GetMap(toMapId);
            if (nextMap == null) { AddLog("The portal seems inert..."); return; }

            // Switch map and move party
            _state.SetMap(nextMap);

            // Ensure target is a floor; otherwise find nearest
            if (!nextMap.InBounds(toX, toY) || nextMap.GetTile(toX, toY).TileType != MapTileType.Floor)
            {
                var alt = FindAnyRoomCenter(nextMap) ?? (0, 0);
                toX = alt.x; toY = alt.y;
            }

            var leader = _state.Party.Leader as ActorBase;
            leader?.Teleport(toX, toY);

            // Move all members to the leader's tile (single-cell party)
            foreach (var member in _state.Party.Members)
            {
                if (member.Id == leader?.Id) continue;
                ((ActorBase)member).Teleport(toX, toY);
            }

            // Update validators
            foreach (var m in _state.Party.Members)
                m.SetMovementValidator((x, y) => nextMap.InBounds(x, y) && nextMap.GetTile(x, y).TileType == MapTileType.Floor);

            AddLog($"You travel to {nextMap.Name} via {exitName}.");
        }
        else
        {
            AddLog("This exit doesn't lead anywhere... yet.");
        }
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
        var portals = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Portal).ToList();
        
        if (enemies.Any())
            actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " Attack" : "[red]SPACE[/] Attack");
        else if (items.Any())
            actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " Pick Up" : "[yellow]SPACE[/] Pick Up");
        else if (npcs.Any())
            actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " Talk" : "[blue]SPACE[/] Talk");
        else if (portals.Any())
            actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " Use Exit" : "[green]SPACE[/] Use Exit");
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
            helpTable.AddRow(ControllerUi.Confirm(ctype), "Interact (Attack/Talk/Pick up/Use Exit)");
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
            helpTable.AddRow("[yellow]SPACE / E[/]", "Interact (Attack/Talk/Pick up/Use Exit)");
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

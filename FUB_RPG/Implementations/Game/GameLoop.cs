using Spectre.Console;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
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
using Fub.Implementations.Map.Objects;
using Fub.Implementations.Abilities;
using Fub.Interfaces.Abilities;
using Fub.Interfaces.Combat;
using Fub.Implementations.Map;

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
    private bool _uiInitialized;

    // Map registry for endless generation
    private MapRegistry? _mapRegistry;

    // Simple enemy respawn tracking per map
    private readonly Dictionary<Guid, int> _mapStepCounters = new();
    private const int RespawnIntervalSteps = 30;

    // Viewport configuration optimized for 1920x1080 (approximately 240x67 characters in typical console)
    private const int MaxViewportWidth = 80;  // Maximum cells to show horizontally (each cell ~7 chars wide)
    private const int MaxViewportHeight = 35; // Maximum cells to show vertically
    private const int MinViewportWidth = 15;
    private const int MinViewportHeight = 10;

    public GameLoop(GameState state, IMapGenerator mapGenerator, MapRenderer renderer)
    {
        _state = state;
        _mapGenerator = mapGenerator;
        _renderer = renderer;
        _combatResolver = new TurnBasedCombatResolver(_state);
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
            new List<string> { "New Game", "Settings", "Quit" },
            _state);

        if (choice == "Quit")
        {
            _state.SetPhase(GamePhase.GameOver);
            return;
        }

        if (choice == "Settings")
        {
            ShowSettingsMenu();
            return;
        }

        if (choice == "New Game")
        {
            StartNewGame();
        }
    }

    private void StartNewGame()
    {
        // Use EnhancedMapGenerator for better variety
        var enhancedGenerator = new EnhancedMapGenerator();
        var world = new World("Adventure Realm");
        _state.SetWorld(world);

        // Initialize map registry for endless generation
        _mapRegistry = new MapRegistry(world, enhancedGenerator);

        // Create starting city with the registry
        var startCity = _mapRegistry.CreateStartingCity();
        _state.SetMap(startCity);
        _state.Party.AddGold(50);
        
        PlacePartyAtFirstFloor(startCity);

        // Starter gear
        GiveStartingWeapons();

        foreach (var member in _state.Party.Members)
            member.SetMovementValidator((x, y) => startCity.InBounds(x, y) && startCity.GetTile(x, y).TileType == MapTileType.Floor);

        // Populate starting city (safe zone)
        PopulateMap(startCity, 0, 4, 4);

        _state.SetPhase(GamePhase.Exploring);
    }

    private void ShowSettingsMenu()
    {
        var options = new List<string> { "Slow", "Normal", "Fast", "Instant", "Back" };
        var current = _state.CombatSpeed.ToString();
        AnsiConsole.MarkupLine($"[bold cyan]Settings[/]  [grey](Current Speed: [yellow]{current}[/])[/]");
        var pick = PromptNavigator.PromptChoice("Combat Text/Action Speed:", options, _state);
        if (pick == "Back") return;
        var newSpeed = pick switch { "Slow" => CombatSpeed.Slow, "Normal" => CombatSpeed.Normal, "Fast" => CombatSpeed.Fast, "Instant" => CombatSpeed.Instant, _ => CombatSpeed.Normal };
        _state.SetCombatSpeed(newSpeed);
        AnsiConsole.MarkupLine($"[green]Combat speed set to[/] [yellow]{newSpeed}[/].");
        AnsiConsole.MarkupLine("Press any key to continue...");
        InputWaiter.WaitForAny(_state.InputMode);
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

        // Calculate optimal viewport size for 1920x1080 display
        // Reserve space for UI elements (HUD takes ~20 lines, actions/log ~8 lines)
        int reservedVerticalSpace = 28;
        int logRows = _logExpanded ? Math.Min(8, _log.Count) : 1;
        int availableVerticalSpace = Math.Max(5, Console.WindowHeight - reservedVerticalSpace - logRows);
        
        // Each map cell renders as 7 visible characters ("[xxxxx]") plus 1 space between cells
        int cellWidthChars = 8;
        int availableHorizontalSpace = Math.Max(5, (Console.WindowWidth - 2) / cellWidthChars);
        
        // Clamp to sensible limits and map size
        _renderer.ViewWidth = Math.Clamp(availableHorizontalSpace, 5, Math.Min(MaxViewportWidth, map.Width));
        _renderer.ViewHeight = Math.Clamp(availableVerticalSpace, 5, Math.Min(MaxViewportHeight, map.Height));
        // Enforce odd dimensions for proper centering
        if (_renderer.ViewWidth % 2 == 0) _renderer.ViewWidth = Math.Max(5, _renderer.ViewWidth - 1);
        if (_renderer.ViewHeight % 2 == 0) _renderer.ViewHeight = Math.Max(5, _renderer.ViewHeight - 1);

        RenderGameWithUi(map, leader);

        var action = InputManager.ReadNextAction(_state.InputMode);
        bool moved = false;
        switch (action)
        {
            case InputAction.MoveUp:
                moved = TryMoveParty(0, -1);
                break;
            case InputAction.MoveDown:
                moved = TryMoveParty(0, 1);
                break;
            case InputAction.MoveLeft:
                moved = TryMoveParty(-1, 0);
                break;
            case InputAction.MoveRight:
                moved = TryMoveParty(1, 0);
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

        if (moved)
        {
            CheckForAutoInteractions(map);
            RegeneratePartyResources(mpPct: 0.02, tpPct: 0.03);
            TrackStepAndMaybeRespawn(map);
        }

        _state.IncrementTurn();
    }

    private void TrackStepAndMaybeRespawn(IMap map)
    {
        if (!_mapStepCounters.ContainsKey(map.Id)) _mapStepCounters[map.Id] = 0;
        _mapStepCounters[map.Id]++;
        if (_mapStepCounters[map.Id] >= RespawnIntervalSteps)
        {
            _mapStepCounters[map.Id] = 0;
            RespawnEnemies(map, maxNew: 3);
        }
    }

    private void RespawnEnemies(IMap map, int maxNew)
    {
        if (map.Kind == MapKind.Town || map.Kind == MapKind.Interior) return; // safe zones
        // Collect floor tiles not occupied and far enough from leader
        var leader = _state.Party.Leader;
        var floors = new List<(int x, int y)>();
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.GetTile(x, y).TileType == MapTileType.Floor)
                    floors.Add((x, y));
        var occupied = map.Objects.Select(o => (o.X, o.Y)).ToHashSet();
        floors.RemoveAll(p => occupied.Contains(p));
        floors.RemoveAll(p => Math.Abs(p.x - leader.X) + Math.Abs(p.y - leader.Y) < 6);
        if (floors.Count == 0) return;
        var rng = new System.Random();
        floors = floors.OrderBy(_ => rng.Next()).ToList();
        int placed = 0;
        var allSpecies = System.Enum.GetValues<Species>().ToList();
        var allClasses = System.Enum.GetValues<ActorClass>()
            .Where(c => c is not (ActorClass.Carpenter or ActorClass.Blacksmith or ActorClass.Armorer or ActorClass.Goldsmith or ActorClass.Leatherworker or ActorClass.Weaver or ActorClass.Alchemist or ActorClass.Culinarian or ActorClass.Miner or ActorClass.Botanist or ActorClass.Fisher))
            .ToList();
        foreach (var p in floors)
        {
            if (placed >= maxNew) break;
            var species = PickRandom(rng, allSpecies);
            var cls = PickRandom(rng, allClasses);
            var enemy = new MonsterActor($"{species} {cls}", species, cls, p.x, p.y);
            map.AddObject(new MapEnemyObject(enemy.Name, enemy, p.x, p.y));
            placed++;
        }
        if (placed > 0) AddLog($"You sense new foes nearby... ({placed} spawned)");
    }

    private static T PickRandom<T>(System.Random rng, IReadOnlyList<T> list) => list[rng.Next(list.Count)];

    private void CheckForAutoInteractions(IMap map)
    {
        var leader = _state.Party.Leader;
        var objectsHere = map.GetObjectsAt(leader.X, leader.Y);

        // Auto-engage enemies on entry
        var enemies = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Enemy).ToList();
        if (enemies.Any())
        {
            EngageCombat(map, enemies);
            return; // combat will resolve before further interactions
        }

        // Auto-pickup items
        var items = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Item).ToList();
        foreach (var itemObj in items)
        {
            if (itemObj.Item == null) continue;
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

        // Auto-open chests on entry; shops show a hint
        var interacts = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Interactable).ToList();
        var chest = interacts.FirstOrDefault(i => i.Name.Contains("Chest", System.StringComparison.OrdinalIgnoreCase));
        if (chest != null)
        {
            OpenChest(map, chest);
        }
        else if (interacts.Any(i => i.Name.Contains("Shop", System.StringComparison.OrdinalIgnoreCase)))
        {
            AddLog("You see a shop here. Press Interact to browse.");
        }

        // Greet NPCs on entry
        var npcs = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Npc).ToList();
        if (npcs.Any())
        {
            var npc = npcs.First();
            var npcName = npc.Actor?.Name ?? "NPC";
            AddLog($"{npcName}: 'Greetings!' (Press Interact to talk.)");
        }
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
        var interacts = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Interactable).ToList();

        if (enemies.Any()) actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " Attack" : "[red]SPACE[/] Attack");
        else if (items.Any()) actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " Pick Up" : "[yellow]SPACE[/] Pick Up");
        else if (npcs.Any()) actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " Talk" : "[blue]SPACE[/] Talk");
        else if (interacts.Any())
        {
            var label = interacts.Any(i => i.Name.Contains("Shop", System.StringComparison.OrdinalIgnoreCase)) ? "Shop" : "Open";
            actions.Add(_state.InputMode == InputMode.Controller ? ControllerUi.Confirm(ctype) + " " + label : ($"[green]SPACE[/] {label}"));
        }
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

    private void HandleContextAction(IMap map)
    {
        var leader = _state.Party.Leader;
        var objectsHere = map.GetObjectsAt(leader.X, leader.Y);
        var enemies = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Enemy).ToList();
        var npcs = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Npc).ToList();
        var items = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Item).ToList();
        var interacts = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Interactable).ToList();
        var portals = objectsHere.Where(o => o.ObjectKind == MapObjectKind.Portal).ToList();
        if (enemies.Any()) EngageCombat(map, enemies);
        else if (items.Any()) PickupItem(map, items);
        else if (npcs.Any()) TalkToNpc(npcs);
        else if (interacts.Any()) InteractWithObject(map, interacts.First());
        else if (portals.Any()) UsePortal(map, portals.First());
        else SearchRoom(map);
    }

    private void InteractWithObject(IMap map, IMapObject obj)
    {
        var name = obj.Name?.ToLowerInvariant() ?? string.Empty;
        if (name.Contains("chest")) OpenChest(map, obj);
        else if (name.Contains("shop")) OpenShop(map, obj);
        else AddLog("There's nothing to do here.");
    }

    private void OpenChest(IMap map, IMapObject chest)
    {
        var rng = new System.Random();
        int gold = rng.Next(10, 51);
        _state.Party.AddGold(gold);
        AddLog($"You open the chest and find {gold} gold!");
        map.RemoveObject(chest.Id);
    }

    private void OpenShop(IMap map, IMapObject shop)
    {
        var options = new List<(string name, int price)> { ("Potion", 15), ("Herb", 10), ("Scroll", 25) };
        var menu = options.Select(o => $"Buy {o.name} ({o.price}g)").ToList();
        menu.Add("Leave");
        var pick = PromptNavigator.PromptChoice("[bold]Shop[/]", menu, _state);
        if (pick == "Leave") return;
        var choice = options.First(o => pick.Contains(o.name));
        if (_state.Party.Gold < choice.price)
        {
            AddLog("Not enough gold.");
            return;
        }
        if (!_state.Party.TrySpendGold(choice.price))
        {
            AddLog("Not enough gold.");
            return;
        }
        var item = new Fub.Implementations.Items.SimpleItem(choice.name, choice.name, RarityTier.Common, stackable: false);
        if (_state.Party.Leader.Inventory.TryAdd(item, 1)) AddLog($"Bought {choice.name}.");
        else AddLog($"Couldn't carry {choice.name}.");
    }

    private void UsePortal(IMap currentMap, IMapObject portal)
    {
        if (_state.CurrentWorld == null || _mapRegistry == null) 
        {
            AddLog("Portal system not initialized.");
            return;
        }
        
        var exitName = portal.Name;

        // Show brief loading message
        Console.Clear();
        AnsiConsole.MarkupLine($"[cyan]Traveling via {exitName}...[/]");
        
        // Use the registry to handle portal travel and map generation
        var (destMap, toX, toY) = _mapRegistry.UsePortal(currentMap, exitName);

        if (destMap == null) 
        { 
            AddLog("The portal seems inert..."); 
            return; 
        }

        // Update game state
        _state.SetMap(destMap);

        // Validate spawn position
        if (!destMap.InBounds(toX, toY) || destMap.GetTile(toX, toY).TileType != MapTileType.Floor)
        {
            // Find nearest floor tile
            (toX, toY) = FindNearestFloorTile(destMap, toX, toY);
        }

        // Teleport party
        var leader = _state.Party.Leader as ActorBase;
        leader?.Teleport(toX, toY);
        foreach (var member in _state.Party.Members)
        {
            if (member.Id == leader?.Id) continue;
            ((ActorBase)member).Teleport(toX, toY);
        }

        // Update movement validators
        foreach (var m in _state.Party.Members)
            m.SetMovementValidator((x, y) => destMap.InBounds(x, y) && destMap.GetTile(x, y).TileType == MapTileType.Floor);

        // Populate the new map if it's empty
        if (!destMap.Objects.Any())
        {
            int enemyCount = destMap.Kind switch
            {
                MapKind.Town => 0,
                MapKind.Overworld => 6,
                MapKind.Dungeon => 10,
                _ => 4
            };
            
            int npcCount = destMap.Kind switch
            {
                MapKind.Town => 4,
                MapKind.Overworld => 1,
                _ => 0
            };
            
            int itemCount = destMap.Kind switch
            {
                MapKind.Town => 4,
                MapKind.Overworld => 3,
                MapKind.Dungeon => 6,
                _ => 2
            };
            
            PopulateMap(destMap, enemyCount, npcCount, itemCount);
        }

        AddLog($"You travel to {destMap.Name} via {exitName}.");
        
        // Force UI refresh
        _uiInitialized = false;
    }

    private (int x, int y) FindNearestFloorTile(IMap map, int startX, int startY)
    {
        // Start from the given position and spiral outward to find a floor tile
        for (int radius = 0; radius < Math.Max(map.Width, map.Height); radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue; // Only check the edge
                    
                    int x = startX + dx;
                    int y = startY + dy;
                    
                    if (map.InBounds(x, y) && map.GetTile(x, y).TileType == MapTileType.Floor)
                        return (x, y);
                }
            }
        }
        
        // Fallback: find any floor tile
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.GetTile(x, y).TileType == MapTileType.Floor)
                    return (x, y);
        
        // Last resort: return center of map
        return (map.Width / 2, map.Height / 2);
    }

    private void RenderGameWithUi(IMap map, IActor leader)
    {
        if (!_uiInitialized) { Console.Clear(); _uiInitialized = true; }

        // Layout constants
        int sepLines = 1;
        int hudLines = 2;
        int maxLogLines = 6;
        int actionsLines = 1;

        // Compute rows
        int mapHeight = _renderer.ViewHeight;
        int row = 0;
        int consoleHeight = Math.Max(10, Console.WindowHeight);

        // Map section
        var mapText = _renderer.RenderToString(map, _state.Party);
        WriteSection(row, mapHeight, mapText);
        row += mapHeight;

        // Separator
        int mapLineWidth = (_renderer.ViewWidth * 7) + Math.Max(0, _renderer.ViewWidth - 1); // 7 content + spaces between cells
        int sepLen = Math.Max(10, Math.Min(Console.WindowWidth - 2, mapLineWidth));
        string sep = "[grey]" + new string('-', sepLen) + "[/]";
        WriteSection(row, sepLines, sep + "\n");
        row += sepLines;

        // HUD summary
        var sbHud = new StringBuilder();
        sbHud.AppendLine($"[white]Party:[/] {_state.Party.Members.Count}/{_state.Party.MaxSize}  [yellow]Gold:[/] {_state.Party.Gold}  [grey]Turn:[/] {_state.TurnNumber}");
        sbHud.AppendLine($"[grey]Map:[/] {map.Name} [dim]({map.Width}x{map.Height})[/]  [grey]View:[/] [dim]({_renderer.ViewWidth}x{_renderer.ViewHeight})[/]");
        WriteSection(row, hudLines, sbHud.ToString());
        row += hudLines;

        // Party grid (compact core stats with colored bars), clamp to available rows leaving room for sep, actions, log
        string partyGrid = BuildPartyCoreGrid(_state.Party.Members, leader.Id);
        var partyGridLines = (partyGrid ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int remainingAfterParty = consoleHeight - row - sepLines - actionsLines - maxLogLines;
        remainingAfterParty = Math.Max(0, remainingAfterParty);
        int partyLinesToWrite = Math.Min(Math.Max(1, remainingAfterParty), partyGridLines.Length);
        string partyGridClamped = string.Join("\n", partyGridLines.Take(partyLinesToWrite));
        WriteSection(row, partyLinesToWrite, partyGridClamped);
        row += partyLinesToWrite;

        // Separator before actions/log block
        WriteSection(row, sepLines, sep + "\n");
        row += sepLines;

        // Actions (above log)
        string actions = BuildContextActionsString(map, leader);
        WriteSection(row, actionsLines, actions + "\n");
        row += actionsLines;

        // Log at the very bottom, clamp to remaining rows
        int remainingForLog = Math.Max(1, consoleHeight - row);
        var logText = RenderLogAsString(Math.Min(maxLogLines, remainingForLog));
        WriteSection(row, Math.Min(maxLogLines, remainingForLog), logText);
        row += Math.Min(maxLogLines, remainingForLog);

        // Clear out any leftover lines below our last section to avoid overlap
        ClearRemainingRows(row);
    }

    private string FormatPartyLine(IActor actor, Guid leaderId)
    {
        // Fixed-width columns for alignment
        string leaderIcon = actor.Id == leaderId ? "[yellow]★[/] " : "   ";
        string name = TruncPad(actor.Name, 12);
        string job = TruncPad(actor.EffectiveClass.ToString(), 12);
        string lvl = $"Lv{actor.Level}".PadRight(4);
        var hp = actor.GetStat(StatType.Health);
        var mp = actor.GetStat(StatType.Mana);
        var tp = actor.GetStat(StatType.Technical);
        string hpStr = $"[red]HP[/]: {hp.Current:F0}/{hp.Modified:F0}".PadRight(18);
        string mpStr = $"[blue]MP[/]: {mp.Current:F0}/{mp.Modified:F0}".PadRight(18);
        string tpStr = $"[magenta]TP[/]: {tp.Current:F0}/{tp.Modified:F0}".PadRight(18);
        return $"{leaderIcon}{name} {lvl} {job}  {hpStr} {mpStr} {tpStr}";
    }

    private static string TruncPad(string value, int width)
    {
        if (string.IsNullOrEmpty(value)) return new string(' ', width);
        var trimmed = value.Length > width ? value.Substring(0, width) : value;
        return trimmed.PadRight(width);
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
                Console.Write(new string(' ', width));
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
            _uiInitialized = false;
        }
    }

    private void ClearRemainingRows(int startRow)
    {
        try
        {
            int width = Math.Max(1, Console.WindowWidth);
            for (int y = startRow; y < Console.WindowHeight; y++)
            {
                Console.SetCursorPosition(0, y);
                Console.Write(new string(' ', width));
            }
        }
        catch
        {
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

    // === New: Compact party HUD grid with colored bars for HP/MP/TP/EXP ===
    private string BuildPartyCoreGrid(IReadOnlyList<IActor> members, Guid leaderId)
    {
        if (members.Count == 0) return "[grey]No party members[/]\n";
        int consoleWidth = Math.Max(80, Console.WindowWidth);
        int barWidth = Math.Clamp((consoleWidth - 10) / 4 - 10, 12, 24);

        // Dynamically size name/species/class columns to reduce truncation
        // Visible chars per line: star(3) + name + 2 + species + 2 + class + 2 + lvl(6) = sum + 15
        int minName = 16, minSpecies = 12, minClass = 16;
        int fixedOverhead = 15 + 0; // see formula above
        int baseTotal = minName + minSpecies + minClass + fixedOverhead;
        int extra = Math.Max(0, consoleWidth - baseTotal);
        // Distribute extra width: favor class, then name, then species
        int addClass = Math.Min(extra, 8); extra -= addClass; // up to +8
        int addName = Math.Min(extra, 6);  extra -= addName;  // up to +6
        int addSpec = Math.Min(extra, 4);  extra -= addSpec;  // up to +4
        int nameW = minName + addName;
        int speciesW = minSpecies + addSpec;
        int classW = minClass + addClass;

        var sb = new StringBuilder();
        foreach (var m in members)
        {
            string star = m.Id == leaderId ? "[yellow]★[/] " : "  ";
            string name = TruncPad(m.Name, nameW);
            string species = TruncPad(m.Species.ToString(), speciesW);
            string cls = TruncPad(m.EffectiveClass.ToString(), classW);
            string lvl = $"Lv {m.Level}".PadRight(6);

            sb.AppendLine($"{star}[white]{name}[/]  [green]{species}[/]  [cyan]{cls}[/]  [yellow]{lvl}[/]");

            var hp = m.GetStat(StatType.Health);
            var mp = m.GetStat(StatType.Mana);
            var tp = m.GetStat(StatType.Technical);
            var jl = m.JobSystem.GetJobLevel(m.EffectiveClass);
            double expCur = jl.Experience;
            double expMax = Math.Max(1, jl.ExperienceToNextLevel);
            long toNext = Math.Max(0, jl.ExperienceToNextLevel - jl.Experience);
            string hpBar = MakeBar("HP", hp.Current, hp.Modified, barWidth, "red1");
            string mpBar = MakeBar("MP", mp.Current, mp.Modified, barWidth, "deepskyblue1");
            string tpBar = MakeBar("TP", tp.Current, tp.Modified, barWidth, "orchid");
            string xpBar = MakeBar("EXP", expCur, expMax, barWidth, "yellow3");
            sb.AppendLine($"{hpBar}  {mpBar}  {tpBar}  {xpBar}  [yellow]ToNext:[/] [white]{toNext}[/]");
        }
        return sb.ToString();
    }

    private static string MakeBar(string label, double current, double max, int width, string color)
    {
        max = Math.Max(1.0, max);
        current = Math.Max(0.0, Math.Min(current, max));
        int filled = (int)Math.Round((current / max) * width);
        int empty = Math.Max(0, width - filled);
        string fill = new string('\u2588', Math.Max(0, filled));
        string rest = new string('\u2500', Math.Max(0, empty));
        string value = $"{current:0}/{max:0}";
        return $"[{color}]{label}[/]: [{color}]{fill}[/][grey]{rest}[/] [{color}]{value}[/]";
    }

    private void ShowVictoryResults(ICombatSession session, List<IActor> enemyActors, Dictionary<Guid,int> preLevels)
    {
        // Compute total EXP from enemies
        long totalExp = enemyActors.Sum(e => (long)(50 * System.Math.Pow(e.Level, 1.5)));

        // Prepare result data per ally
        var results = new List<(IActor actor, int levelBefore, int levelAfter, long expGained, List<string> learned)>();
        foreach (var ally in _state.Party.Members.Where(a => a.GetStat(StatType.Health).Current > 0))
        {
            var job = ally.EffectiveClass;
            int before = preLevels.TryGetValue(ally.Id, out var lvl) ? lvl : ally.JobSystem.GetJobLevel(job).Level;
            bool leveled = ally.JobSystem.AddExperience(job, totalExp);
            int after = ally.JobSystem.GetJobLevel(job).Level;

            var learned = new List<string>();
            if (after > before)
            {
                for (int lvlUp = before + 1; lvlUp <= after; lvlUp++)
                {
                    foreach (var unlock in ClassAbilityLearnset.GetUnlocks(job))
                    {
                        if (unlock.Level == lvlUp && ally is IHasAbilityBook hab)
                        {
                            var gained = hab.AbilityBook.Learn(unlock.Factory());
                            if (gained)
                                learned.Add(hab.AbilityBook.KnownAbilities[^1].Name);
                        }
                    }
                }
            }

            results.Add((ally, before, after, totalExp, learned));
        }

        // Render results screen
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold green]\ud83c\udf89 Victory Results \ud83c\udf89[/]").RuleStyle("green"));
        AnsiConsole.WriteLine();

        // Enemies defeated list
        var enemiesPanel = new Panel(string.Join(System.Environment.NewLine, enemyActors.Select(e => $"[red]{e.Name}[/] Lv {e.Level}")))
            .Header("[bold red]Enemies Defeated[/]").BorderColor(Color.Red);
        AnsiConsole.Write(enemiesPanel);

        // Party results table with colored bars
        var table = new Table().Border(TableBorder.Rounded).Title("[bold cyan]Party Gains[/]");
        table.AddColumn("Name");
        table.AddColumn("Class");
        table.AddColumn("Lv Before");
        table.AddColumn("Lv After");
        table.AddColumn("EXP Gained");
        table.AddColumn("EXP Bar");

        int barWidth = Math.Clamp((System.Console.WindowWidth - 40) / 4, 12, 24);
        foreach (var r in results)
        {
            var jl = r.actor.JobSystem.GetJobLevel(r.actor.EffectiveClass);
            double cur = jl.Experience;
            double max = System.Math.Max(1, jl.ExperienceToNextLevel);
            table.AddRow(
                r.actor.Name,
                r.actor.EffectiveClass.ToString(),
                $"[yellow]{r.levelBefore}[/]",
                $"[yellow]{r.levelAfter}[/]",
                $"[yellow]{r.expGained}[/]",
                Bar("EXP", cur, max, barWidth, "yellow3")
            );
        }
        AnsiConsole.Write(table);

        // Learned abilities panel (if any)
        var learnedAll = results.SelectMany(r => r.learned.Select(name => ($"{r.actor.Name}", name))).ToList();
        if (learnedAll.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var g in learnedAll)
                sb.AppendLine($"[green]{g.Item1}[/] learned [yellow]{g.Item2}[/]!");
            var learnedPanel = new Panel(new Markup(sb.ToString()))
                .Header("[bold yellow]\u2728 New Abilities[/]").BorderColor(Color.Yellow);
            AnsiConsole.Write(learnedPanel);
        }

        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        InputWaiter.WaitForAny(_state.InputMode);
    }

    private void EngageCombat(IMap map, List<IMapObject> enemies)
    {
        if (enemies.Count == 0) return;
        var enemyActors = enemies.Where(e => e.Actor != null).Select(e => e.Actor!).ToList();

        // Capture pre-combat levels per ally
        var preLevels = _state.Party.Members.ToDictionary(a => a.Id, a => a.JobSystem.GetJobLevel(a.EffectiveClass).Level);

        var session = new CombatSession(_state.Party.Members, enemyActors);
        _combatResolver.BeginCombat(session);
        while (session.IsActive)
        {
            _combatResolver.ProcessTurn(session);
            session.UpdateOutcome();
        }
        _combatResolver.EndCombat(session);

        _uiInitialized = false;

        if (session.Outcome == CombatOutcome.Victory)
        {
            // Show results screen that also applies EXP and learning
            ShowVictoryResults(session, enemyActors, preLevels);

            // Remove defeated enemies from the map
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

    private void TalkToNpc(List<IMapObject> npcs)
    {
        if (npcs == null || npcs.Count == 0) return;
        var npc = npcs.First();
        var npcName = npc.Actor?.Name ?? "NPC";
        AddLog($"{npcName}: \"Greetings, traveler! The dungeon is dangerous. Stay safe!\"");
    }


    private void AddLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _log.Add(message);
        if (_log.Count > LogMaxEntries)
            _log.RemoveRange(0, _log.Count - LogMaxEntries);
    }

    private static int SafeLineCount(string text)
    {
        if (text == null) return 0;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return lines.Length;
    }

    // Colored bar consistent with battle/results UI
    private static string Bar(string label, double current, double max, int width, string color)
    {
        max = Math.Max(1.0, max);
        current = Math.Max(0.0, Math.Min(current, max));
        int filled = (int)Math.Round((current / max) * width);
        int empty = Math.Max(0, width - filled);
        string fill = new string('\u2588', Math.Max(0, filled));
        string rest = new string('\u2500', Math.Max(0, empty));
        string value = $"{current:0}/{max:0}";
        return $"[{color}]{label}[/]: [{color}]{fill}[/][grey]{rest}[/] [{color}]{value}[/]";
    }

    private void ShowPartyMenu()
    {
        var choices = new List<string> { "Inspect Member", "View Stats", "Change Leader", "Manage Equipment", "Settings", "Back" };
        var choice = PromptNavigator.PromptChoice("[bold cyan]Party Menu[/]", choices, _state);
        if (choice == "Inspect Member") InspectMember();
        else if (choice == "View Stats") ShowPartyStats();
        else if (choice == "Change Leader") ChangeLeader();
        else if (choice == "Manage Equipment") ShowEquipmentMenu();
        else if (choice == "Settings") ShowSettingsMenu();
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

        // Equipment actions
        var actions = new List<string> { "Change Weapon", "Back" };
        var action = PromptNavigator.PromptChoice("Equip Menu", actions, _state);
        if (action == "Change Weapon")
        {
            var weapons = actorBase.Inventory.Slots
                .Where(s => s.Item is IWeapon)
                .Select(s => (weapon: (IWeapon)s.Item!, qty: s.Quantity))
                .ToList();
            if (!weapons.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No weapons in inventory.[/]");
                AnsiConsole.MarkupLine("Press any key to continue...");
                InputWaiter.WaitForAny(_state.InputMode);
                return;
            }
            var choices = weapons.Select(w => w.weapon.Name + " x" + w.qty.ToString()).ToList();
            choices.Add("Back");
            var picked = PromptNavigator.PromptChoice("Choose a weapon to equip:", choices, _state);
            if (picked == "Back") return;
            var sel = weapons.First(w => picked.StartsWith(w.weapon.Name, StringComparison.Ordinal)).weapon;
            if (actorBase.TryEquip(sel, out var replaced))
            {
                AnsiConsole.MarkupLine("[green]Equipped " + sel.Name + "![/]");
                AnsiConsole.MarkupLine("[cyan]Class is now: " + member.EffectiveClass + "[/]");
                if (replaced != null) AnsiConsole.MarkupLine("[grey]Unequipped " + replaced.Name + "[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Cannot equip that.[/]");
            }
            AnsiConsole.MarkupLine("Press any key to continue...");
            InputWaiter.WaitForAny(_state.InputMode);
        }
        _uiInitialized = false; // redraw cleanly
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

    private void RegeneratePartyResources(double mpPct, double tpPct)
    {
        foreach (var member in _state.Party.Members)
        {
            var mp = member.GetStat(StatType.Mana);
            var tp = member.GetStat(StatType.Technical);
            
            double mpRestore = mp.Modified * mpPct;
            double tpRestore = tp.Modified * tpPct;
            
            if (mp is Fub.Implementations.Stats.StatValue mpStat)
                mpStat.ApplyDelta(mpRestore);
            if (tp is Fub.Implementations.Stats.StatValue tpStat)
                tpStat.ApplyDelta(tpRestore);
        }
    }

    private void PickupItem(IMap map, List<IMapObject> items)
    {
        if (items == null || items.Count == 0) return;
        
        var leader = _state.Party.Leader;
        foreach (var itemObj in items)
        {
            if (itemObj.Item == null) continue;
            
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
            else
            {
                AddLog($"Inventory is full! Can't pick up {itemObj.Item.Name}.");
            }
        }
    }

    private void ShowHelpScreen()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]Help & Controls[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        var controlsTable = new Table().Border(TableBorder.Rounded);
        controlsTable.AddColumn("[bold]Action[/]");
        controlsTable.AddColumn("[bold]Key[/]");
        
        if (_state.InputMode == InputMode.Controller)
        {
            var ctype = _state.ControllerType;
            controlsTable.AddRow("Move", ControllerUi.MovePad(ctype));
            controlsTable.AddRow("Interact/Confirm", ControllerUi.Confirm(ctype));
            controlsTable.AddRow("Search/Cancel", ControllerUi.Cancel(ctype));
            controlsTable.AddRow("Inventory", ControllerUi.Inventory(ctype));
            controlsTable.AddRow("Party Menu", ControllerUi.Party(ctype));
            controlsTable.AddRow("Help", ControllerUi.Help(ctype));
            controlsTable.AddRow("Menu", ControllerUi.Menu(ctype));
            controlsTable.AddRow("Toggle Log", ControllerUi.Log(ctype));
        }
        else
        {
            controlsTable.AddRow("Move", "WASD or Arrow Keys");
            controlsTable.AddRow("Interact/Confirm", "Space");
            controlsTable.AddRow("Search", "R");
            controlsTable.AddRow("Inventory", "I");
            controlsTable.AddRow("Party Menu", "P");
            controlsTable.AddRow("World Map", "M");
            controlsTable.AddRow("Help", "H");
            controlsTable.AddRow("Menu", "ESC");
            controlsTable.AddRow("Toggle Log", "L");
        }

        AnsiConsole.Write(controlsTable);
        AnsiConsole.WriteLine();
        
        AnsiConsole.MarkupLine("[bold yellow]Game Tips:[/]");
        AnsiConsole.MarkupLine("• Walk over items to automatically pick them up");
        AnsiConsole.MarkupLine("• Enemies will engage you automatically when you enter their tile");
        AnsiConsole.MarkupLine("• Search rooms (R key) to find hidden treasure");
        AnsiConsole.MarkupLine("• Visit shops to buy helpful items");
        AnsiConsole.MarkupLine("• Use portals to travel between maps");
        AnsiConsole.MarkupLine("• Party members regenerate MP and TP as you walk");
        AnsiConsole.WriteLine();
        
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        InputWaiter.WaitForAny(_state.InputMode);
        _uiInitialized = false;
    }

    private bool ConfirmExitToMenu()
    {
        var choice = PromptNavigator.PromptChoice(
            "[yellow]Exit to Main Menu?[/]",
            new List<string> { "Yes", "No" },
            _state);
        return choice == "Yes";
    }

    private void InspectMember()
    {
        var memberNames = _state.Party.Members.Select(m => m.Name).ToList();
        memberNames.Add("Back");
        var choice = PromptNavigator.PromptChoice("Select party member to inspect:", memberNames, _state);
        if (choice == "Back") return;
        
        var member = _state.Party.Members.First(m => m.Name == choice);
        ShowMemberDetails(member);
    }

    private void ShowMemberDetails(IActor member)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[bold cyan]{member.Name}[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        // Basic info
        var infoTable = new Table().Border(TableBorder.Rounded);
        infoTable.AddColumn("[bold]Property[/]");
        infoTable.AddColumn("[bold]Value[/]");
        infoTable.AddRow("Name", member.Name);
        infoTable.AddRow("Species", member.Species.ToString());
        infoTable.AddRow("Base Class", member.Class.ToString());
        infoTable.AddRow("Effective Class", member.EffectiveClass.ToString());
        infoTable.AddRow("Level", member.Level.ToString());
        infoTable.AddRow("Experience", member.Experience.ToString());
        
        AnsiConsole.Write(infoTable);
        AnsiConsole.WriteLine();

        // Stats
        var statsTable = new Table().Border(TableBorder.Rounded);
        statsTable.AddColumn("[bold]Stat[/]");
        statsTable.AddColumn("[bold]Current[/]");
        statsTable.AddColumn("[bold]Max[/]");
        
        var hp = member.GetStat(StatType.Health);
        var mp = member.GetStat(StatType.Mana);
        var tp = member.GetStat(StatType.Technical);
        var str = member.GetStat(StatType.Strength);
        var agi = member.GetStat(StatType.Agility);
        var vit = member.GetStat(StatType.Vitality);
        var intel = member.GetStat(StatType.Intellect);
        var spirit = member.GetStat(StatType.Spirit);
        var luck = member.GetStat(StatType.Luck);
        
        statsTable.AddRow("[red]Health[/]", $"{hp.Current:F0}", $"{hp.Modified:F0}");
        statsTable.AddRow("[blue]Mana[/]", $"{mp.Current:F0}", $"{mp.Modified:F0}");
        statsTable.AddRow("[magenta]Technical[/]", $"{tp.Current:F0}", $"{tp.Modified:F0}");
        statsTable.AddRow("Strength", $"{str.Current:F0}", $"{str.Modified:F0}");
        statsTable.AddRow("Agility", $"{agi.Current:F0}", $"{agi.Modified:F0}");
        statsTable.AddRow("Vitality", $"{vit.Current:F0}", $"{vit.Modified:F0}");
        statsTable.AddRow("Intellect", $"{intel.Current:F0}", $"{intel.Modified:F0}");
        statsTable.AddRow("Spirit", $"{spirit.Current:F0}", $"{spirit.Modified:F0}");
        statsTable.AddRow("Luck", $"{luck.Current:F0}", $"{luck.Modified:F0}");
        
        AnsiConsole.Write(statsTable);
        AnsiConsole.WriteLine();

        // Abilities (if available)
        if (member is IHasAbilityBook hab && hab.AbilityBook.KnownAbilities.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]Known Abilities:[/]");
            foreach (var ability in hab.AbilityBook.KnownAbilities)
            {
                AnsiConsole.MarkupLine($"  • {ability.Name} ({ability.Category})");
            }
            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        InputWaiter.WaitForAny(_state.InputMode);
        _uiInitialized = false;
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
        var cfg = new MapContentConfig 
        { 
            EnemyCount = enemyCount, 
            NpcCount = npcCount, 
            ItemCount = itemCount, 
            MinDistanceFromLeader = 5, 
            Seed = null 
        };
        populator.Populate(map, _state.Party, cfg);
    }
}

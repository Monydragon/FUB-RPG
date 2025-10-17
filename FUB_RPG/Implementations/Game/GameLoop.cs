using Spectre.Console;
using Spectre.Console.Rendering;
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
using Fub.Interfaces.Combat;
using Fub.Implementations.Map;
using Fub.Interfaces.Config;
using Fub.Implementations.Config;
using Fub.Interfaces.Items;
using Fub.Implementations.Items;
using Fub.Implementations.Random;
using Fub.Interfaces.Items.Equipment;
using Fub.Implementations.Stats;
using Fub.Implementations.Progression;
using Fub.Implementations.Abilities;

namespace Fub.Implementations.Game;

public sealed class GameLoop : IGameLoop
{
    private readonly GameState _state;
    private readonly IMapGenerator _mapGenerator;
    private readonly MapRenderer _renderer;
    private readonly TurnBasedCombatResolver _combatResolver;
    private readonly IGameConfig _config;
    private readonly IItemDatabase _itemDb;
    private bool _running;
    public bool IsRunning => _running;

    // Collapsible message log
    private readonly List<string> _log = new();
    private bool _logExpanded;
    // private const int LogMaxEntries = 200; // replaced by config
    private bool _uiInitialized;

    // Map registry for endless generation
    private MapRegistry? _mapRegistry;

    // Simple enemy respawn tracking per map
    private readonly Dictionary<Guid, int> _mapStepCounters = new();
    // private const int RespawnIntervalSteps = 30; // replaced by config

    // Living world manager for moving NPCs/enemies
    private LivingWorldManager? _worldManager;

    // Double-buffer renderer to eliminate flicker
    private ConsoleDoubleBufferRenderer? _doubleBuffer;

    // Viewport config moved to _config
    // private const int MaxViewportWidth = 80;
    // private const int MaxViewportHeight = 35;
    // private const int MinViewportWidth = 15;
    // private const int MinViewportHeight = 10;

    // New: Tier-based equipment generator for auto-equipping best gear
    private TierBasedEquipmentGenerator _tierGenerator;

    public GameLoop(GameState state, IMapGenerator mapGenerator, MapRenderer renderer)
        : this(state, mapGenerator, renderer, new DefaultGameConfig(), new InMemoryItemDatabase()) { }

    public GameLoop(GameState state, IMapGenerator mapGenerator, MapRenderer renderer, IGameConfig config, IItemDatabase itemDb)
    {
        _state = state;
        _mapGenerator = mapGenerator;
        _renderer = renderer;
        _combatResolver = new TurnBasedCombatResolver(_state);
        _config = config;
        _itemDb = itemDb;
        _tierGenerator = new TierBasedEquipmentGenerator(new RandomSource(Environment.TickCount));
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
        // Seed a few default shop items
        SeedItemDatabase();

        // Use configured map generator for better variety
        var world = new World("Adventure Realm");
        _state.SetWorld(world);

        // Initialize map registry for endless generation
        _mapRegistry = new MapRegistry(world, _mapGenerator);

        // Create starting city with the registry
        var startCity = _mapRegistry.CreateStartingCity();
        _state.SetMap(startCity);
        _state.Party.AddGold(_config.StartingGold);
        
        PlacePartyAtFirstFloor(startCity);

        // Starter gear
        GiveStartingWeapons();

        foreach (var member in _state.Party.Members)
            member.SetMovementValidator((x, y) => startCity.InBounds(x, y) && startCity.GetTile(x, y).TileType == MapTileType.Floor);

        // Populate starting city (safe zone)
        PopulateMap(startCity, 0, 4, 4);

        // Initialize living world manager for NPC/enemy movement
        _worldManager = new LivingWorldManager(startCity, _state.Party, new RandomSource(world.Seed), _state.Difficulty);
        _worldManager.EnsureMovementControllersForExistingEntities();

        _state.SetPhase(GamePhase.Exploring);
    }

    private void SeedItemDatabase()
    {
        // Minimal shop assortment
        _itemDb.Register(new SimpleItem("Potion", ItemType.Consumable, RarityTier.Common, stackable: true, description: "Heals a small amount."), 15);
        _itemDb.Register(new SimpleItem("Herb", ItemType.Consumable, RarityTier.Common, stackable: true, description: "Restores a little MP."), 10);
        _itemDb.Register(new SimpleItem("Scroll", ItemType.Consumable, RarityTier.Uncommon, stackable: false, description: "A mysterious spell scroll."), 40);
        // A couple of generic weapons
        _itemDb.Register(new Weapon("Rusty Sword", WeaponType.Sword, DamageType.Physical, 2, 5, 1.1, RarityTier.Common, EquipmentSlot.MainHand), 60);
        _itemDb.Register(new Weapon("Old Staff", WeaponType.Staff, DamageType.Holy, 1, 4, 1.2, RarityTier.Common, EquipmentSlot.MainHand), 55);
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

        // Calculate optimal viewport size based on console and config
        int reservedVerticalSpace = _config.UiReservedVerticalLines;
        int logRows = _logExpanded ? Math.Min(_config.LogMaxExpandedRows, _log.Count) : 1;
        int availableVerticalSpace = Math.Max(5, Console.WindowHeight - reservedVerticalSpace - logRows);
        
        int cellWidthChars = _config.CellWidthChars;
        int availableHorizontalSpace = Math.Max(5, (Console.WindowWidth - 2) / cellWidthChars);
        
        _renderer.ViewWidth = Math.Clamp(availableHorizontalSpace, 5, Math.Min(_config.MaxViewportWidth, map.Width));
        _renderer.ViewHeight = Math.Clamp(availableVerticalSpace, 5, Math.Min(_config.MaxViewportHeight, map.Height));
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
            // Increment global steps
            _state.IncrementSteps();

            // Update living world (move NPCs/enemies and AI)
            _worldManager?.Update(0.5f);

            CheckForAutoInteractions(map);
            RegeneratePartyResources(_config.RegenMpPercentPerStep, _config.RegenTpPercentPerStep);
            TrackStepAndMaybeRespawn(map);
        }

        _state.IncrementTurn();
    }

    private void TrackStepAndMaybeRespawn(IMap map)
    {
        if (!_mapStepCounters.ContainsKey(map.Id)) _mapStepCounters[map.Id] = 0;
        _mapStepCounters[map.Id]++;
        if (_mapStepCounters[map.Id] >= _config.RespawnIntervalSteps)
        {
            _mapStepCounters[map.Id] = 0;
            RespawnEnemies(map, _config.RespawnMaxNew);
        }
    }

    private void RespawnEnemies(IMap map, int maxNew)
    {
        if (map.Kind == MapKind.Town || map.Kind == MapKind.Interior) return; // safe zones
        var leader = _state.Party.Leader;
        var floors = new List<(int x, int y)>();
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.GetTile(x, y).TileType == MapTileType.Floor)
                    floors.Add((x, y));
        var occupied = map.Objects.Select(o => (o.X, o.Y)).ToHashSet();
        floors.RemoveAll(p => occupied.Contains(p));
        floors.RemoveAll(p => Math.Abs(p.x - leader.X) + Math.Abs(p.y - leader.Y) < _config.RespawnMinDistanceFromLeader);
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
                int amount = rng.Next(_config.CoinPickupMin, _config.CoinPickupMax);
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

    private void OpenChest(IMap _, IMapObject chest)
    {
        var rng = new System.Random();
        int gold = rng.Next(_config.ChestGoldMin, _config.ChestGoldMax);
        _state.Party.AddGold(gold);
        AddLog($"You open the chest and find {gold} gold!");
        _state.CurrentMap?.RemoveObject(chest.Id);
    }

    private void OpenShop(IMap map, IMapObject shop)
    {
        // If this is our richer MapShopObject, use its themed inventory.
        if (shop is Fub.Implementations.Map.Objects.MapShopObject richShop)
        {
            if (richShop.Inventory == null || richShop.Inventory.Count == 0)
            {
                AddLog("The shop is empty.");
                return;
            }

            // Build menu with stock counts
            var menu = richShop.Inventory.Select(e => $"Buy {e.Item.Name} ({e.Price}g) [x{e.Quantity}]").ToList();
            menu.Add("Leave");
            var pick = PromptNavigator.PromptChoice($"[bold]{shop.Name}[/]", menu, _state);
            if (pick == "Leave") return;
            var chosen = richShop.Inventory.FirstOrDefault(e => pick.Contains(e.Item.Name));
            if (chosen == null)
            {
                AddLog("Item not found.");
                return;
            }
            if (chosen.Quantity <= 0)
            {
                AddLog("That item is out of stock.");
                return;
            }

            var leader = _state.Party.Leader;
            if (!leader.Inventory.CanAdd(chosen.Item, 1))
            {
                AddLog($"Can't carry {chosen.Item.Name}. Inventory full.");
                return;
            }
            if (_state.Party.Gold < chosen.Price || !_state.Party.TrySpendGold(chosen.Price))
            {
                AddLog("Not enough gold.");
                return;
            }

            // Remove from shop stock and add to inventory
            if (!richShop.TryTakeItem(chosen.Item.Name, out var purchased))
            {
                AddLog("That item is no longer available.");
                // Refund gold
                _state.Party.AddGold(chosen.Price);
                return;
            }

            if (leader.Inventory.TryAdd(purchased.item, 1))
            {
                AddLog($"Bought {purchased.item.Name}.");
            }
            else
            {
                // Should not happen since CanAdd checked; safeguard refund
                AddLog($"Couldn't carry {purchased.item.Name}.");
                _state.Party.AddGold(purchased.price);
            }
            return;
        }

        // Fallback: use item database if populated; otherwise simple defaults
        var dbItems = _itemDb.GetAll().ToList();
        if (dbItems.Count > 0)
        {
            var menu = dbItems.Select(e => $"Buy {e.item.Name} ({e.price}g)").ToList();
            menu.Add("Leave");
            var pick = PromptNavigator.PromptChoice("[bold]Shop[/]", menu, _state);
            if (pick == "Leave") return;
            var match = dbItems.FirstOrDefault(e => pick.Contains(e.item.Name));
            if (match.item == null) { AddLog("Item not found."); return; }

            var leader = _state.Party.Leader;
            if (!leader.Inventory.CanAdd(match.item, 1)) { AddLog($"Can't carry {match.item.Name}. Inventory full."); return; }
            if (_state.Party.Gold < match.price || !_state.Party.TrySpendGold(match.price)) { AddLog("Not enough gold."); return; }
            if (leader.Inventory.TryAdd(match.item, 1)) AddLog($"Bought {match.item.Name}.");
            else { AddLog($"Couldn't carry {match.item.Name}."); _state.Party.AddGold(match.price); }
            return;
        }
        // Original simple fallback
        var options = new List<(string name, int price)> { ("Potion", 15), ("Herb", 10), ("Scroll", 25) };
        var simpleMenu = options.Select(o => $"Buy {o.name} ({o.price}g)").ToList();
        simpleMenu.Add("Leave");
        var simplePick = PromptNavigator.PromptChoice("[bold]Shop[/]", simpleMenu, _state);
        if (simplePick == "Leave") return;
        var choice = options.First(o => simplePick.Contains(o.name));
        if (_state.Party.Gold < choice.price || !_state.Party.TrySpendGold(choice.price))
        {
            AddLog("Not enough gold.");
            return;
        }
        var item = new SimpleItem(choice.name, ItemType.Consumable, RarityTier.Common, stackable: false);
        if (_state.Party.Leader.Inventory.TryAdd(item, 1)) AddLog($"Bought {choice.name}.");
        else AddLog($"Couldn't carry {choice.name}.");
    }

    private void TalkToNpc(List<IMapObject> npcs)
    {
        if (npcs == null || npcs.Count == 0) return;
        var npc = npcs.First();
        var npcName = npc.Actor?.Name ?? "NPC";

        if (npc is Fub.Implementations.Map.Objects.MapNpcObject richNpc && richNpc.DialogueLines != null && richNpc.DialogueLines.Count > 0)
        {
            foreach (var line in richNpc.DialogueLines)
                AddLog($"{npcName}: \"{line}\"");
        }
        else
        {
            AddLog($"{npcName}: \"Greetings, traveler! The dungeon is dangerous. Stay safe!\"");
        }
    }

    private void EngageCombat(IMap map, List<IMapObject> enemies)
    {
        if (enemies.Count == 0) return;

        // Build encounter group based on difficulty and party size
        var rng = new System.Random();
        int desired = EnemyScaler.GetEnemyCount(_state.Party.Members.Count, _state.Difficulty, rng);

        // Gather enemies on current cell and adjacent cells until reaching desired count
        var leader = _state.Party.Leader;
        var encounterObjects = new List<IMapObject>();
        encounterObjects.AddRange(enemies);
        if (encounterObjects.Count < desired)
        {
            var neighbors = new List<(int x,int y)>
            {
                (leader.X+1, leader.Y), (leader.X-1, leader.Y), (leader.X, leader.Y+1), (leader.X, leader.Y-1),
                (leader.X+1, leader.Y+1), (leader.X-1, leader.Y-1), (leader.X+1, leader.Y-1), (leader.X-1, leader.Y+1)
            };
            foreach (var (nx, ny) in neighbors)
            {
                if (!map.InBounds(nx, ny)) continue;
                var at = map.GetObjectsAt(nx, ny).Where(o => o.ObjectKind == MapObjectKind.Enemy).ToList();
                foreach (var o in at)
                {
                    if (encounterObjects.Count >= desired) break;
                    if (!encounterObjects.Any(e => e.Id == o.Id)) encounterObjects.Add(o);
                }
                if (encounterObjects.Count >= desired) break;
            }
        }

        var enemyActors = encounterObjects.Where(e => e.Actor != null).Select(e => e.Actor!).ToList();

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
            foreach (var enemy in encounterObjects) map.RemoveObject(enemy.Id);
            AddLog("Victory!");
        }
        else if (session.Outcome == CombatOutcome.Defeat)
        {
            AddLog("Defeat. Returning to main menu...");
            _state.SetPhase(GamePhase.MainMenu);
        }
    }

    // Allow changing a member's base class (outside of combat). Automatically equips best available gear.
    private void ShowChangeClassMenu()
    {
        var members = _state.Party.Members.ToList();
        if (members.Count == 0) return;
        var who = PromptNavigator.PromptChoice("Change class for who?", members.Select(m => m.Name).Append("Back").ToList(), _state);
        if (who == "Back") return;
        var actor = members.First(m => m.Name == who) as ActorBase;
        if (actor is null) return;

        var all = Enum.GetValues<ActorClass>().ToList();
        var pick = PromptNavigator.PromptChoice($"Select new class for {actor.Name}:", all, _state);
        if (pick == actor.Class)
        {
            AnsiConsole.MarkupLine("[grey]No change.[/]");
            InputWaiter.WaitForAny(_state.InputMode);
            return;
        }

        actor.ChangeClass(pick);

        // Remove gear not valid for new class
        foreach (EquipmentSlot slot in Enum.GetValues<EquipmentSlot>())
        {
            var eq = actor.GetEquipped(slot);
            if (eq == null) continue;
            if (eq.AllowedClasses.Any() && !eq.AllowedClasses.Contains(pick))
            {
                if (actor.TryUnequip(slot, out var removed) && removed != null)
                {
                    if (!actor.Inventory.TryAdd(removed, 1)) AddLog($"No space for {removed.Name}. It was dropped.");
                }
            }
        }

        // Equip best available items for the new class (generate missing pieces to avoid empty slots)
        AutoEquipBestForClass(actor, generateMissing: true);

        AnsiConsole.MarkupLine($"[green]{actor.Name} is now a {pick} with optimized equipment.[/]");
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        InputWaiter.WaitForAny(_state.InputMode);
    }

    // Choose the best gear for each slot for the actor's current class/level. Optionally generate tier-matched items when inventory lacks one.
    private void AutoEquipBestForClass(ActorBase actor, bool generateMissing)
    {
        var desiredTier = TierBasedEquipmentGenerator.GetTierForLevel(actor.Level);
        var invEquip = actor.Inventory.Slots
            .Where(s => s.Item is IEquipment)
            .Select(s => (IEquipment)s.Item!)
            .Where(e => e.RequiredLevel <= actor.Level && (!e.AllowedClasses.Any() || e.AllowedClasses.Contains(actor.Class)))
            .ToList();
        var bySlot = invEquip.GroupBy(e => e.Slot).ToDictionary(g => g.Key, g => g.ToList());

        foreach (EquipmentSlot slot in Enum.GetValues<EquipmentSlot>())
        {
            IEquipment? best = null;
            if (bySlot.TryGetValue(slot, out var list) && list.Count > 0)
            {
                best = list
                    .OrderByDescending(e => (int)e.Tier)
                    .ThenByDescending(e => e.Rarity)
                    .ThenByDescending(e => e.BaseValue)
                    .FirstOrDefault();
            }
            if (best == null && generateMissing)
            {
                var set = _tierGenerator.GenerateCompleteSet(actor.Class, desiredTier);
                if (set.TryGetValue(slot, out var gen)) best = gen;
            }
            if (best != null)
            {
                if (actor.TryEquip(best, out var replaced))
                {
                    // If best came from inventory, remove it
                    actor.Inventory.TryRemove(best.Id, 1);
                    if (replaced != null && !actor.Inventory.TryAdd(replaced, 1))
                        AddLog($"No space for {replaced.Name}. It was dropped.");
                }
            }
        }
    }

    // Ensure that main-hand weapon meets the tier for the current level; auto-upgrade at level 10/20/.../100.
    private void EnsureWeaponTierForLevel(ActorBase actor)
    {
        var desired = TierBasedEquipmentGenerator.GetTierForLevel(actor.Level);
        var current = actor.GetEquipped(EquipmentSlot.MainHand) as IEquipment;
        if (current != null && (int)current.Tier >= (int)desired) return;

        // Prefer inventory weapons that satisfy the desired tier
        var candidates = actor.Inventory.Slots
            .Where(s => s.Item is IEquipment eq && eq.Slot == EquipmentSlot.MainHand)
            .Select(s => (IEquipment)s.Item!)
            .Where(e => e.RequiredLevel <= actor.Level && (!e.AllowedClasses.Any() || e.AllowedClasses.Contains(actor.Class)))
            .OrderByDescending(e => (int)e.Tier)
            .ThenByDescending(e => e.Rarity)
            .ThenByDescending(e => e.BaseValue)
            .ToList();

        IEquipment? best = candidates.FirstOrDefault(e => (int)e.Tier >= (int)desired) ?? candidates.FirstOrDefault();
        if (best == null)
        {
            var set = _tierGenerator.GenerateCompleteSet(actor.Class, desired);
            set.TryGetValue(EquipmentSlot.MainHand, out best);
        }

        if (best != null)
        {
            if (actor.TryEquip(best, out var replaced))
            {
                actor.Inventory.TryRemove(best.Id, 1);
                if (replaced != null && !actor.Inventory.TryAdd(replaced, 1))
                    AddLog($"No space for {replaced.Name}. It was dropped.");
                AddLog($"{actor.Name}'s weapon has been upgraded to {desired}.");
            }
        }
    }

    private void ShowVictoryResults(ICombatSession session, List<IActor> enemyActors, Dictionary<Guid,int> preLevels)
    {
        // Compute total EXP from enemies
        long totalExp = enemyActors.Sum(e => (long)(50 * System.Math.Pow(e.Level, 1.5)));

        // NEW: Generate loot & gold based on enemies
        var rng = new System.Random();
        int totalGold = enemyActors.Sum(e => rng.Next(5, 10) * e.Level);
        var droppedItems = new List<IItem>();
        // Simple placeholder loot: one basic item per enemy chance
        foreach (var enemy in enemyActors)
        {
            if (rng.NextDouble() < 0.5)
            {
                droppedItems.Add(new SimpleItem($"LootShard Lv{enemy.Level}", ItemType.Material, RarityTier.Common, stackable: true));
            }
        }
        if (totalGold > 0) _state.Party.AddGold(totalGold);
        if (droppedItems.Count > 0)
        {
            foreach (var it in droppedItems)
            {
                _state.Party.Leader.Inventory.TryAdd(it, 1);
            }
        }

        var xpCalculator = new Fub.Implementations.Progression.ExperienceCalculator();
        var victoryScreen = new Fub.Implementations.Combat.VictoryScreen(xpCalculator);

        var rewardsList = new List<Fub.Implementations.Combat.VictoryRewards>();

        foreach (var ally in _state.Party.Members)
        {
            var job = ally.EffectiveClass;
            int before = preLevels.TryGetValue(ally.Id, out var lvl) ? lvl : ally.JobSystem.GetJobLevel(job).Level;

            // Capture pre-battle stat snapshot
            var preStats = ally.AllStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Modified);

            // Capture pre-known abilities
            HashSet<Guid> preAbilities = new();
            List<string> learned = new();
            if (ally is Fub.Interfaces.Abilities.IHasAbilityBook hasBook)
            {
                preAbilities = hasBook.AbilityBook.KnownAbilities.Select(a => a.Id).ToHashSet();
            }

            // Apply EXP
            ally.JobSystem.AddExperience(job, totalExp);
            int after = ally.JobSystem.GetJobLevel(job).Level;
            int levelsGained = System.Math.Max(0, after - before);

            // Apply stat growth & resource restoration
            if (levelsGained > 0)
            {
                Fub.Implementations.Progression.ClassStatGrowth.ApplyGrowth(ally, before, after);
            }

            // Ability unlocks for newly reached levels
            if (ally is Fub.Interfaces.Abilities.IHasAbilityBook hasBookAfter)
            {
                foreach (var unlock in Fub.Implementations.Abilities.ClassAbilityLearnset.GetUnlocks(job))
                {
                    if (unlock.Level > before && unlock.Level <= after)
                    {
                        var ability = unlock.Factory();
                        if (hasBookAfter.AbilityBook.Learn(ability))
                        {
                            learned.Add(ability.Name);
                        }
                    }
                }
            }

            // Compute stat changes post-growth
            var statChanges = new List<Fub.Implementations.Combat.VictoryStatChange>();
            foreach (var kv in ally.AllStats)
            {
                var type = kv.Key;
                var newVal = kv.Value.Modified;
                preStats.TryGetValue(type, out var oldVal);
                if (System.Math.Abs(newVal - oldVal) > 0.0001)
                {
                    statChanges.Add(new Fub.Implementations.Combat.VictoryStatChange { Stat = type, OldValue = oldVal, NewValue = newVal });
                }
            }

            var rewards = new Fub.Implementations.Combat.VictoryRewards
            {
                ExperienceGained = totalExp,
                GoldGained = 0, // set on first reward only below
                ItemsDropped = new List<IItem>(),
                OldLevel = before,
                NewLevel = after,
                LevelsGained = levelsGained,
                StatChanges = statChanges,
                LearnedAbilities = learned
            };
            rewardsList.Add(rewards);
        }

        // Assign shared loot/gold to first reward for aggregation
        if (rewardsList.Count > 0)
        {
            rewardsList[0].GoldGained = totalGold;
            rewardsList[0].ItemsDropped = droppedItems;
        }

        // Show party victory
        victoryScreen.DisplayPartyVictory(_state.Party.Members.ToList(), rewardsList, animationSpeedMs: 30);

        // After showing party victory, mark UI for refresh and log victory
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold green]\ud83c\udf89 Victory! \ud83c\udf89[/]").RuleStyle("green"));
        AnsiConsole.WriteLine();
        AddLog("Victory!");

        // Auto-upgrade each ally's weapon if they crossed a tier threshold
        foreach (var ally in _state.Party.Members)
        {
            if (ally is ActorBase ab)
            {
                EnsureWeaponTierForLevel(ab);
            }
        }

        _uiInitialized = false; // ensure next explore frame redraws
    }

    private void SearchRoom(IMap _)
    {
        var rng = new System.Random();
        var roll = rng.Next(100);
        if (roll < _config.SearchFindGoldChancePercent)
        {
            int gold = rng.Next(_config.SearchGoldMin, _config.SearchGoldMax);
            _state.Party.AddGold(gold);
            AddLog($"You found {gold} gold coins!");
        }
        else if (roll < _config.SearchFindGoldChancePercent + _config.SearchFindPassageChancePercent)
        {
            AddLog("You found a hidden passage... but it's blocked.");
        }
        else AddLog("You found nothing of interest.");
    }

    private void RenderGameWithUi(IMap map, IActor leader)
    {
        // Initialize console settings for smooth rendering (one-time setup)
        if (!_uiInitialized)
        {
            try
            {
                // Set buffer size to match window to prevent scrolling
                Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);
            }
            catch { /* Ignore if terminal doesn't support */ }
            Console.CursorVisible = false;
            Console.Clear();
        }

        // Use cursor positioning to redraw from top instead of clearing entire screen
        if (_uiInitialized)
        {
            try
            {
                Console.SetCursorPosition(0, 0);
            }
            catch { Console.Clear(); }
        }
        
        _uiInitialized = true;

        // Header shows map name and type information
        var header = $"[bold cyan]{Markup.Escape(map.Name)}[/]  [grey]({map.Kind} • {map.Theme})[/]  [grey]|[/] Turn {_state.TurnNumber}";
        AnsiConsole.Write(new Rule(header).RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        // Map viewport
        _renderer.Render(map, _state.Party);
        AnsiConsole.WriteLine();

        // Party header line with leader indicator and meta info
        var leaderName = _state.Party.Leader.Name;
        AnsiConsole.MarkupLine($"[bold]Party[/] | [yellow]Leader: ★ {Markup.Escape(leaderName)}[/] | [yellow]Members: {_state.Party.Members.Count}/4[/] | [yellow]Gold: {_state.Party.Gold}g[/] | [cyan]Steps: {_state.Steps}[/]");

        // Create horizontal grid for all party members in one row
        var grid = new Grid().Expand();
        // Add one column per party member (up to 4)
        for (int i = 0; i < _state.Party.Members.Count; i++)
        {
            grid.AddColumn(new GridColumn().NoWrap().PadRight(1));
        }

        // Compute dynamic widths so panels/bars expand with console size
        int memberCount = Math.Max(1, _state.Party.Members.Count);
        int totalPadding = Math.Max(0, memberCount - 1); // grid PadRight(1) per col
        int availableForPanels = Math.Max(40, Console.WindowWidth - 2 - totalPadding);
        int perPanelWidth = Math.Max(26, availableForPanels / memberCount);
        // Bar width budget: subtract label/value cosmetics (~14 chars) and clamp
        int barWidth = Math.Clamp(perPanelWidth - 14, 14, 60);

        // Build panels for each member
        var panels = new List<IRenderable>();
        foreach (var m in _state.Party.Members)
        {
            var hp = m.GetStat(StatType.Health);
            var mp = m.GetStat(StatType.Mana);
            var tp = m.GetStat(StatType.Technical);

            // Resource label lines (stylized) and bars with dynamic width
            string hpHeader = ResourceHeader("HP", hp.Current, hp.Modified, "red");
            var hpLine = Bar("HP", hp.Current, hp.Modified, barWidth, "red");

            string mpHeader = ResourceHeader("MP", mp.Current, mp.Modified, "deepskyblue1");
            var mpLine = Bar("MP", mp.Current, mp.Modified, barWidth, "deepskyblue1");

            string tpHeader = ResourceHeader("TP", tp.Current, tp.Modified, "yellow1");
            var tpLine = Bar("TP", tp.Current, tp.Modified, barWidth, "yellow1");

            // Calculate EXP bar for current level progress
            var currentLevel = m.Level;
            var currentExp = m.Experience;
            var xpCalc = new ExperienceCalculator();
            var xpForCurrentLevel = xpCalc.GetExperienceForLevel(currentLevel, LevelCurveType.Moderate);
            var xpForNextLevel = xpCalc.GetExperienceForLevel(currentLevel + 1, LevelCurveType.Moderate);
            var xpIntoCurrentLevel = currentExp - xpForCurrentLevel;
            var xpNeededForLevel = xpForNextLevel - xpForCurrentLevel;
            string expHeader = ResourceHeader("EXP", xpIntoCurrentLevel, xpNeededForLevel, "green");
            var expLine = Bar("EXP", xpIntoCurrentLevel, xpNeededForLevel, barWidth, "green");

            // Prominent Level/Class line (inside body so it won't get truncated like headers can)
            string levelClassLine = $"[bold yellow]Lv {m.Level}[/]  [bold cyan]{Markup.Escape(m.EffectiveClass.ToString())}[/]";

            // Compact extra stats to use added width (two lines)
            double str = m.GetStat(StatType.Strength).Modified;
            double agi = m.GetStat(StatType.Agility).Modified;
            double intl = m.GetStat(StatType.Intellect).Modified;
            double vit = m.GetStat(StatType.Vitality).Modified;
            double atk = m.GetStat(StatType.AttackPower).Modified;
            double arm = m.GetStat(StatType.Armor).Modified;
            double crit = m.GetStat(StatType.CritChance).Modified;
            double spd = m.GetStat(StatType.Speed).Modified;
            string statLine1 = $"[grey]STR[/]: [white]{str:0}[/]  [grey]AGI[/]: [white]{agi:0}[/]  [grey]INT[/]: [white]{intl:0}[/]  [grey]VIT[/]: [white]{vit:0}[/]";
            string statLine2 = $"[grey]ATK[/]: [white]{atk:0}[/]  [grey]ARM[/]: [white]{arm:0}[/]  [grey]CRIT[/]: [white]{crit:0}[/]  [grey]SPD[/]: [white]{spd:0}[/]";

            // Leader indicator
            bool isLeader = m.Id == _state.Party.Leader.Id;
            string leaderPrefix = isLeader ? "[yellow]\u2605[/] " : "";

            // Build the panel content
            var body = new Markup(
                levelClassLine + "\n" +
                hpHeader + "\n" +
                hpLine + "\n" +
                mpHeader + "\n" +
                mpLine + "\n" +
                tpHeader + "\n" +
                tpLine + "\n" +
                expHeader + "\n" +
                expLine + "\n" +
                statLine1 + "\n" +
                statLine2
            );

            // Header focused on name; Level/Class moved into body for visibility
            var headerTitle = new PanelHeader($"{leaderPrefix}[bold]{Markup.Escape(m.Name)}[/]");
            var panel = new Panel(body)
                .RoundedBorder()
                .Header(headerTitle)
                .BorderColor(isLeader ? Color.Yellow : Color.Grey)
                .Expand();

            panels.Add(panel);
        }

        // Add all panels to the grid in a single row
        grid.AddRow(panels.ToArray());
        AnsiConsole.Write(grid);

        // Context actions hint
        var actions = BuildContextActionsString(map, leader);
        AnsiConsole.MarkupLine("\n" + actions);

        // Message log (collapsed or expanded)
        if (_log.Count > 0)
        {
            if (_logExpanded)
            {
                var last = _log.TakeLast(Math.Min(_config.LogMaxExpandedRows, _log.Count));
                foreach (var line in last)
                    AnsiConsole.MarkupLine(Markup.Escape(line));
            }
            else
            {
                AnsiConsole.MarkupLine(Markup.Escape(_log[^1]));
            }
        }

        // Clear any leftover content below our render (in case window resized)
        try
        {
            int currentRow = Console.CursorTop;
            if (currentRow < Console.WindowHeight - 1)
            {
                // Write blank lines to clear any leftover content
                for (int i = currentRow; i < Console.WindowHeight - 1; i++)
                {
                    Console.WriteLine(new string(' ', Console.WindowWidth - 1));
                }
            }
        }
        catch { /* Ignore if terminal doesn't support positioning */ }
    }

    // Colored bar simple renderer (no inline text); label parameter kept for signature compatibility
    private static string Bar(string label, double current, double max, int width, string barColor)
    {
        max = Math.Max(1.0, max);
        current = Math.Max(0.0, Math.Min(current, max));
        width = Math.Max(6, width);
        int filled = (int)Math.Round((current / max) * width);
        filled = Math.Clamp(filled, 0, width);
        var sb = new System.Text.StringBuilder();
        // filled segment
        if (filled > 0) sb.Append('[').Append(barColor).Append(']').Append(new string('\u2588', filled)).Append("[/]");
        // empty segment
        int empty = width - filled;
        if (empty > 0) sb.Append("[grey]").Append(new string('\u2500', empty)).Append("[/]");
        return sb.ToString();
    }

    // Stylized resource header: colored label plus value
    private static string ResourceHeader(string label, double current, double max, string color)
    {
        max = Math.Max(1.0, max);
        current = Math.Max(0.0, current);
        return $"[{color}][bold]{label}[/]:[/] [white]{current:0}/{max:0}[/]";
    }

    private void InspectPartyMember()
    {
        var names = _state.Party.Members.Select(m => m.Name).ToList();
        names.Add("Back");
        var pick = PromptNavigator.PromptChoice("Inspect which member?", names, _state);
        if (pick == "Back") return;
        var m = _state.Party.Members.First(x => x.Name == pick);

        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[bold cyan]Inspect: {m.Name}[/]").RuleStyle("cyan"));
        var info = new Table().Border(TableBorder.Rounded);
        info.AddColumn("Field");
        info.AddColumn("Value");
        info.AddRow("Species", m.Species.ToString());
        info.AddRow("Base Class", m.Class.ToString());
        info.AddRow("Effective Class", m.EffectiveClass.ToString());
        info.AddRow("Level", m.Level.ToString());
        info.AddRow("Experience", m.Experience.ToString());
        var hp = m.GetStat(StatType.Health);
        var mp = m.GetStat(StatType.Mana);
        var tp = m.GetStat(StatType.Technical);
        info.AddRow("HP", $"{hp.Current:F0}/{hp.Modified:F0}");
        info.AddRow("MP", $"{mp.Current:F0}/{mp.Modified:F0}");
        info.AddRow("TP", $"{tp.Current:F0}/{tp.Modified:F0}");
        AnsiConsole.Write(info);
        AnsiConsole.WriteLine();

        // Stats grid
        var statsTable = new Table().Border(TableBorder.Rounded);
        statsTable.AddColumn("Stat");
        statsTable.AddColumn("Value");
        foreach (var s in m.AllStats)
            statsTable.AddRow(s.Key.ToString(), s.Value.Modified.ToString("F0"));
        AnsiConsole.Write(statsTable);
        AnsiConsole.WriteLine();

        // Abilities
        if (m is Fub.Interfaces.Abilities.IHasAbilityBook hasBook && hasBook.AbilityBook.KnownAbilities.Count > 0)
        {
            var abil = new Table().Border(TableBorder.Rounded);
            abil.AddColumn("Ability");
            abil.AddColumn("Category");
            abil.AddColumn("Target");
            foreach (var a in hasBook.AbilityBook.KnownAbilities)
                abil.AddRow(a.Name, a.Category.ToString(), a.TargetType.ToString());
            AnsiConsole.Write(abil);
            AnsiConsole.WriteLine();
        }

        // Equipment
        if (m is ActorBase ab)
        {
            var eq = new Table().Border(TableBorder.Rounded);
            eq.AddColumn("Slot");
            eq.AddColumn("Item");
            foreach (EquipmentSlot slot in System.Enum.GetValues<EquipmentSlot>())
            {
                var e = ab.GetEquipped(slot);
                eq.AddRow(slot.ToString(), e?.Name ?? "[grey]Empty[/]");
            }
            AnsiConsole.Write(eq);
            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine("[grey]Press any key to return...[/]");
        InputWaiter.WaitForAny(_state.InputMode);
        _uiInitialized = false;
    }

    private void ShowPartyMenu()
    {
        var choices = new List<string> { "Inspect Member", "View Stats", "Change Leader", "Manage Equipment", "Change Class", "Settings", "Back" };
        var choice = PromptNavigator.PromptChoice("[bold cyan]Party Menu[/]", choices, _state);
        if (choice == "Inspect Member") InspectPartyMember();
        else if (choice == "View Stats") ShowPartyStats();
        else if (choice == "Change Leader") ChangeLeader();
        else if (choice == "Manage Equipment") ShowEquipmentMenu();
        else if (choice == "Change Class") ShowChangeClassMenu();
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

        var selected = items.First(i => choice.StartsWith(i.item.Name, StringComparison.Ordinal)).item;
        var actions = new List<string> { "Examine", "Back" };
        if (selected is IEquipment) actions.Insert(0, "Equip");
        if (selected is ConsumableItem) actions.Insert(0, "Use");
        var action = PromptNavigator.PromptChoice("[yellow]" + selected.Name + "[/]", actions, _state);

        if (action == "Equip" && selected is IEquipment eq)
        {
            if (leader is ActorBase actorBase)
            {
                if (actorBase.TryEquip(eq, out var replaced))
                {
                    // Remove one from inventory for newly equipped item
                    leader.Inventory.TryRemove(eq.Id, 1);
                    // Return replaced to inventory if any
                    if (replaced != null && !leader.Inventory.TryAdd(replaced, 1))
                    {
                        AddLog($"No space for {replaced.Name}. It was dropped.");
                    }
                    AnsiConsole.MarkupLine("[green]Equipped " + eq.Name + "![/]");
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
        else if (action == "Use" && selected is ConsumableItem consumable)
        {
            // Choose a target party member
            var names = _state.Party.Members.Select(m => m.Name).ToList();
            names.Add("Back");
            var who = PromptNavigator.PromptChoice("Use on whom?", names, _state);
            if (who != "Back")
            {
                var target = _state.Party.Members.First(m => m.Name == who);
                if (ApplyConsumable(target, consumable))
                {
                    leader.Inventory.TryRemove(consumable.Id, 1);
                    AnsiConsole.MarkupLine($"[green]Used {consumable.Name} on {target.Name}.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]That had no effect.[/]");
                }
                AnsiConsole.MarkupLine("Press any key to continue...");
                InputWaiter.WaitForAny(_state.InputMode);
            }
        }
        else if (action == "Examine")
        {
            AnsiConsole.MarkupLine("[bold]" + selected.Name + "[/]");
            AnsiConsole.MarkupLine("Type: " + selected.ItemType);
            AnsiConsole.MarkupLine("Rarity: " + selected.Rarity);
            if (selected is IEquipment eqi)
            {
                AnsiConsole.MarkupLine($"Required Level: {eqi.RequiredLevel}");
                var cls = (eqi.AllowedClasses?.Any() == true) ? string.Join(", ", eqi.AllowedClasses) : "Any";
                AnsiConsole.MarkupLine($"Allowed Classes: {Markup.Escape(cls)}");
                if (eqi.RequirementsOptional)
                    AnsiConsole.MarkupLine("[grey]Requirements are optional for this item.[/]");
            }
            if (!string.IsNullOrWhiteSpace(selected.Description))
                AnsiConsole.MarkupLine(Markup.Escape(selected.Description));
            AnsiConsole.MarkupLine("Press any key to continue...");
            InputWaiter.WaitForAny(_state.InputMode);
        }
        _uiInitialized = false; // redraw cleanly
    }

    private void ShowEquipmentMenu()
    {
        // Switch to full-screen equipment UI with actor cycling
        ShowEquipmentScreen();
        _uiInitialized = false; // redraw cleanly
    }

    // Full-screen equipment screen with keyboard/controller navigation and quick actor cycling
    private void ShowEquipmentScreen()
    {
        var members = _state.Party.Members.ToList();
        if (members.Count == 0) return;
        int memberIndex = members.FindIndex(m => m.Id == _state.Party.Leader.Id);
        if (memberIndex < 0) memberIndex = 0;
        var slots = System.Enum.GetValues<EquipmentSlot>().ToList();
        int slotIndex = 0;

        while (true)
        {
            var actor = members[memberIndex] as ActorBase;
            if (actor is null)
            {
                AnsiConsole.MarkupLine("[red]Invalid actor.[/]");
                return;
            }

            // Render screen
            Console.Clear();
            AnsiConsole.Write(new Rule($"[bold cyan]Equipment[/] — {actor.Name} (Lv {actor.Level} {actor.EffectiveClass})").RuleStyle("cyan"));
            AnsiConsole.WriteLine();

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Slot");
            table.AddColumn("Equipped Item");

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var equipped = actor.GetEquipped(slot);
                var isSel = i == slotIndex;
                string slotLabel = isSel ? $"[bold yellow]{slot}[/]" : slot.ToString();
                string itemLabel = equipped != null ? Markup.Escape(equipped.Name) : "[grey]Empty[/]";
                table.AddRow(slotLabel, itemLabel);
            }
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            // Controls
            if (_state.InputMode == InputMode.Controller)
            {
                AnsiConsole.MarkupLine($"{ControllerUi.MovePad(_state.ControllerType)} Move  {ControllerUi.Confirm(_state.ControllerType)} Equip  {ControllerUi.Cancel(_state.ControllerType)} Back  {ControllerUi.Help(_state.ControllerType)} Prev  {ControllerUi.Log(_state.ControllerType)} Next");
            }
            else
            {
                AnsiConsole.MarkupLine("[cyan]W/S[/] Move  [red]SPACE/ENTER[/] Equip  [grey]ESC[/] Back  [grey]H[/] Prev  [grey]L[/] Next  [grey]U[/] Unequip");
            }

            var input = InputManager.ReadNextAction(_state.InputMode);
            if (input == InputAction.MoveUp)
            {
                slotIndex = (slotIndex - 1 + slots.Count) % slots.Count;
                continue;
            }
            if (input == InputAction.MoveDown)
            {
                slotIndex = (slotIndex + 1) % slots.Count;
                continue;
            }
            if (input == InputAction.Help) // Use as Prev Actor (LB/Options)
            {
                memberIndex = (memberIndex - 1 + members.Count) % members.Count;
                continue;
            }
            if (input == InputAction.Log) // Use as Next Actor (RB)
            {
                memberIndex = (memberIndex + 1) % members.Count;
                continue;
            }
            if (input == InputAction.Menu)
            {
                // Back
                return;
            }
            if (input == InputAction.Interact)
            {
                var targetSlot = slots[slotIndex];
                // Build candidate list from inventory for this slot
                var candidates = actor.Inventory.Slots
                    .Where(s => s.Item is IEquipment)
                    .Select(s => s.Item as IEquipment)
                    .Where(e => e != null && e.Slot == targetSlot)
                    .Cast<IEquipment>()
                    .ToList();

                var menu = new List<string>();
                if (candidates.Count > 0)
                    menu.AddRange(candidates.Select(c => c.Name));
                if (actor.GetEquipped(targetSlot) != null)
                    menu.Add("Unequip");
                menu.Add("Back");

                var picked = PromptNavigator.PromptChoice("Choose item to equip:", menu, _state);
                if (picked == "Back") continue;
                if (picked == "Unequip")
                {
                    if (actor.TryUnequip(targetSlot, out var removed))
                    {
                        if (removed != null && !actor.Inventory.TryAdd(removed, 1))
                            AddLog($"No space for {removed.Name}. It was dropped.");
                        AddLog($"Unequipped {removed?.Name ?? "item"} from {targetSlot}.");
                    }
                    continue;
                }
                var equipItem = candidates.FirstOrDefault(c => c.Name == picked);
                if (equipItem != null)
                {
                    if (actor.TryEquip(equipItem, out var replaced))
                    {
                        // Remove from inventory and return replaced
                        actor.Inventory.TryRemove(equipItem.Id, 1);
                        if (replaced != null && !actor.Inventory.TryAdd(replaced, 1))
                            AddLog($"No space for {replaced.Name}. It was dropped.");
                        AddLog($"Equipped {equipItem.Name}.");
                    }
                    else
                    {
                        AddLog("Cannot equip that.");
                    }
                }
            }

            // Keyboard-only unequip shortcut
            if (_state.InputMode == InputMode.Keyboard)
            {
                if (Console.KeyAvailable)
                {
                    var k = Console.ReadKey(true).Key;
                    if (k == ConsoleKey.U)
                    {
                        var targetSlot = slots[slotIndex];
                        if (actor.TryUnequip(targetSlot, out var removed))
                        {
                            if (removed != null && !actor.Inventory.TryAdd(removed, 1))
                                AddLog($"No space for {removed.Name}. It was dropped.");
                            AddLog($"Unequipped {removed?.Name ?? "item"} from {targetSlot}.");
                        }
                    }
                    else if (k == ConsoleKey.Escape)
                    {
                        return;
                    }
                }
            }
        }
    }

    private void UsePortal(IMap currentMap, IMapObject portal)
    {
        if (_state.CurrentWorld == null || _mapRegistry == null)
        {
            AddLog("Portal system not initialized.");
            return;
        }
        var exitName = portal.Name;
        Console.Clear();
        AnsiConsole.MarkupLine($"[cyan]Traveling via {exitName}...[/]");
        var (destMap, toX, toY) = _mapRegistry.UsePortal(currentMap, exitName);
        if (destMap == null)
        {
            AddLog("The portal doesn't seem to lead anywhere.");
            return;
        }
        _state.SetMap(destMap);

        // Teleport the party to destination
        if (_state.Party.Leader is ActorBase leader)
        {
            leader.Teleport(toX, toY);
            foreach (var m in _state.Party.Members)
            {
                if (m.Id == leader.Id) continue;
                if (m is ActorBase ab) ab.Teleport(toX, toY);
            }
        }

        // IMPORTANT: rebind movement validators to the new map so movement works after transition
        foreach (var m in _state.Party.Members)
        {
            m.SetMovementValidator((x, y) => destMap.InBounds(x, y) && destMap.GetTile(x, y).TileType == MapTileType.Floor);
        }

        // Recreate living world manager targeting the new map to keep NPC/enemy AI active
        if (_state.CurrentWorld is World world)
        {
            _worldManager = new LivingWorldManager(destMap, _state.Party, new RandomSource(world.Seed), _state.Difficulty);
            _worldManager.EnsureMovementControllersForExistingEntities();
        }

        _uiInitialized = false;
    }

    private void AddLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _log.Add(message);
        if (_log.Count > _config.LogMaxEntries)
            _log.RemoveRange(0, _log.Count - _config.LogMaxEntries);
    }

    private bool TryMoveParty(int dx, int dy)
    {
        var leader = _state.Party.Leader;
        bool moved = leader.TryMove(dx, dy);
        if (!moved) return false;
        foreach (var member in _state.Party.Members)
        {
            if (member.Id == leader.Id) continue;
            member.TryMove(dx, dy);
        }
        return true;
    }

    private void PlacePartyAtFirstFloor(IMap map)
    {
        // Prefer center of the first room if available, else first floor tile.
        int toX = map.Width / 2, toY = map.Height / 2;
        if (map.Rooms.Count > 0)
        {
            var room = map.Rooms[0];
            toX = room.X + Math.Max(0, room.Width / 2);
            toY = room.Y + Math.Max(0, room.Height / 2);
        }
        else
        {
            bool found = false;
            for (int y = 0; y < map.Height && !found; y++)
            {
                for (int x = 0; x < map.Width && !found; x++)
                {
                    if (map.GetTile(x, y).TileType == MapTileType.Floor)
                    {
                        toX = x; toY = y; found = true;
                    }
                }
            }
        }

        foreach (var m in _state.Party.Members)
        {
            if (m is ActorBase ab) ab.Teleport(toX, toY);
            m.SetMovementValidator((x, y) => map.InBounds(x, y) && map.GetTile(x, y).TileType == MapTileType.Floor);
        }
    }

    private void PopulateMap(IMap map, int enemyCount, int npcCount, int itemCount)
    {
        var pop = new MapContentPopulator();
        var cfg = new MapContentConfig
        {
            EnemyCount = enemyCount,
            NpcCount = npcCount,
            ItemCount = itemCount,
            MinDistanceFromLeader = 3,
            Seed = _state.CurrentWorld is World w ? w.Seed : (int?)null
        };
        pop.Populate(map, _state.Party, cfg);
    }

    private void RegeneratePartyResources(double mpPercentPerStep, double tpPercentPerStep)
    {
        foreach (var m in _state.Party.Members)
        {
            HealPercent(m, StatType.Mana, (float)mpPercentPerStep);
            HealPercent(m, StatType.Technical, (float)tpPercentPerStep);
        }
    }

    private void PickupItem(IMap map, List<IMapObject> items)
    {
        if (items == null || items.Count == 0) return;
        var leader = _state.Party.Leader;
        foreach (var obj in items.ToList())
        {
            if (obj is MapItemObject itemObj)
            {
                if (leader.Inventory.TryAdd(itemObj.Item, 1))
                {
                    AddLog($"Picked up {itemObj.Item.Name}.");
                    map.RemoveObject(obj.Id);
                }
                else
                {
                    AddLog("Inventory full.");
                }
            }
        }
    }

    private void ShowWorldMapMenu()
    {
        if (_state.CurrentWorld == null || _mapRegistry == null)
        {
            AnsiConsole.MarkupLine("[red]No world map available.[/]");
            return;
        }

        var mapNames = _mapRegistry.GetAllMapNames().ToList();
        if (mapNames.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No maps discovered yet.[/]");
            return;
        }

        var choices = new List<string>(mapNames) { "Back" };
        var choice = PromptNavigator.PromptChoice("[cyan]World Map[/]", choices, _state);
        if (choice != "Back")
        {
            AnsiConsole.MarkupLine($"[green]Selected: {choice}[/]");
        }
    }

    private void ShowHelpScreen()
    {
        Console.Clear();
        var panel = new Panel(
            "[bold cyan]Game Controls[/]\n\n" +
            "[yellow]Arrow Keys[/] - Move party\n" +
            "[yellow]I[/] - Open inventory\n" +
            "[yellow]P[/] - Party menu\n" +
            "[yellow]M[/] - World map\n" +
            "[yellow]Space[/] - Interact/Context action\n" +
            "[yellow]S[/] - Search room\n" +
            "[yellow]H[/] - This help screen\n" +
            "[yellow]L[/] - Toggle combat log\n" +
            "[yellow]Esc[/] - Return to main menu\n\n" +
            "[dim]Press any key to continue...[/]"
        );
        panel.Header = new PanelHeader("[bold]Help[/]");
        AnsiConsole.Write(panel);
        Console.ReadKey(true);
        _uiInitialized = false;
    }

    private void ShowPartyStats()
    {
        var choices = _state.Party.Members.Select(m => m.Name).ToList();
        choices.Add("Back");
        var choice = PromptNavigator.PromptChoice("View stats for:", choices, _state);
        if (choice != "Back")
        {
            var member = _state.Party.Members.First(m => m.Name == choice);
            Console.Clear();
            var table = new Table();
            table.AddColumn("Stat");
            table.AddColumn("Value");
            
            foreach (var statType in Enum.GetValues<StatType>())
            {
                var val = member.GetStat(statType);
                table.AddRow(statType.ToString(), $"{val.Current:F1}/{val.Modified:F1}");
            }
            
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey(true);
        }
        _uiInitialized = false;
    }

    private void ChangeLeader()
    {
        var choices = _state.Party.Members.Select(m => m.Name).ToList();
        choices.Add("Back");
        var choice = PromptNavigator.PromptChoice("Choose new leader:", choices, _state);
        if (choice != "Back")
        {
            var newLeader = _state.Party.Members.First(m => m.Name == choice);
            _state.Party.SetLeader(newLeader.Id);
            AddLog($"{newLeader.Name} is now the party leader.");
            AnsiConsole.MarkupLine($"[green]{newLeader.Name} is now the leader![/]");
        }
        _uiInitialized = false;
    }

    private void GiveStartingWeapons()
    {
        foreach (var member in _state.Party.Members)
        {
            if (member is ActorBase actorBase && actorBase.GetEquipped(EquipmentSlot.MainHand) == null)
            {
                var weaponType = ClassWeaponMappings.GetWeaponTypeForClass(member.EffectiveClass);
                var weapon = new Weapon("Starter " + weaponType, weaponType, RarityTier.Common);
                actorBase.TryEquip(weapon, out _);
            }
        }
    }

    private bool ConfirmExitToMenu()
    {
        var confirm = AnsiConsole.Confirm("Exit to main menu? (Progress will be lost)");
        return confirm;
    }

    private void HealPercent(IActor actor, StatType stat, float percent)
    {
        if (percent <= 0) return;
        var statValue = actor.GetStat(stat);
        if (statValue is StatValue sv)
        {
            var max = sv.Modified;
            var current = sv.Current;
            var healAmount = max * percent;
            sv.ApplyDelta(healAmount);
        }
    }

    private bool ApplyConsumable(IActor target, ConsumableItem item)
    {
        if (item == null) return false;
        
        bool applied = false;
        
        // Handle full resource restoration
        if (item.RestoresAllResources)
        {
            var hp = target.GetStat(StatType.Health);
            if (hp is StatValue hpSv) hpSv.ApplyDelta(hpSv.Modified - hpSv.Current);
            
            var mp = target.GetStat(StatType.Mana);
            if (mp is StatValue mpSv) mpSv.ApplyDelta(mpSv.Modified - mpSv.Current);
            
            var tp = target.GetStat(StatType.Technical);
            if (tp is StatValue tpSv) tpSv.ApplyDelta(tpSv.Modified - tpSv.Current);
            
            applied = true;
        }
        // Handle primary resource restoration
        else if (item.PrimaryResource.HasValue)
        {
            var resourceStat = item.PrimaryResource.Value switch
            {
                ResourceType.Health => StatType.Health,
                ResourceType.Mana => StatType.Mana,
                ResourceType.Stamina => StatType.Technical,
                _ => StatType.Health
            };
            
            var stat = target.GetStat(resourceStat);
            if (stat is StatValue sv)
            {
                double restoreAmount;
                
                if (item.IsPercentageBased)
                {
                    restoreAmount = sv.Modified * item.RestorePercentage;
                }
                else
                {
                    restoreAmount = item.RestoreAmount;
                }
                
                sv.ApplyDelta(restoreAmount);
                applied = true;
            }
        }
        
        // Handle status effect removal if applicable
        if (item.RemovesStatusEffects)
        {
            // TODO: Implement status effect removal when status effect system is added
            applied = true;
        }
        
        return applied;
    }
}

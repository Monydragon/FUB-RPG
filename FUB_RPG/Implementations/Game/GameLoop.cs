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
using Fub.Implementations.Abilities;
using Fub.Interfaces.Abilities;
using Fub.Interfaces.Combat;
using Fub.Implementations.Map;
using Fub.Interfaces.Config;
using Fub.Implementations.Config;
using Fub.Interfaces.Items;
using Fub.Implementations.Items;

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

    // Viewport config moved to _config
    // private const int MaxViewportWidth = 80;
    // private const int MaxViewportHeight = 35;
    // private const int MinViewportWidth = 15;
    // private const int MinViewportHeight = 10;

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

        // Use EnhancedMapGenerator for better variety
        var enhancedGenerator = new EnhancedMapGenerator();
        var world = new World("Adventure Realm");
        _state.SetWorld(world);

        // Initialize map registry for endless generation
        _mapRegistry = new MapRegistry(world, enhancedGenerator);

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

        _state.SetPhase(GamePhase.Exploring);
    }

    private void SeedItemDatabase()
    {
        // Minimal shop assortment
        _itemDb.Register(new Fub.Implementations.Items.SimpleItem("Potion", ItemType.Consumable, RarityTier.Common, stackable: true, description: "Heals a small amount."), 15);
        _itemDb.Register(new Fub.Implementations.Items.SimpleItem("Herb", ItemType.Consumable, RarityTier.Common, stackable: true, description: "Restores a little MP."), 10);
        _itemDb.Register(new Fub.Implementations.Items.SimpleItem("Scroll", ItemType.Consumable, RarityTier.Uncommon, stackable: false, description: "A mysterious spell scroll."), 40);
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

            var menu = richShop.Inventory.Select(e => $"Buy {e.item.Name} ({e.price}g)").ToList();
            menu.Add("Leave");
            var pick = PromptNavigator.PromptChoice($"[bold]{shop.Name}[/]", menu, _state);
            if (pick == "Leave") return;
            var match = richShop.Inventory.FirstOrDefault(e => pick.Contains(e.item.Name));
            if (match.item == null)
            {
                AddLog("Item not found.");
                return;
            }

            // Check capacity before charging or removing stock
            var leader = _state.Party.Leader;
            if (!leader.Inventory.CanAdd(match.item, 1))
            {
                AddLog($"Can't carry {match.item.Name}. Inventory full.");
                return;
            }

            if (_state.Party.Gold < match.price || !_state.Party.TrySpendGold(match.price))
            {
                AddLog("Not enough gold.");
                return;
            }

            // Remove from shop stock and add to inventory
            if (!richShop.TryTakeItem(match.item.Name, out var purchased))
            {
                AddLog("That item is no longer available.");
                // Refund gold
                _state.Party.AddGold(match.price);
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
        var item = new Fub.Implementations.Items.SimpleItem(choice.name, ItemType.Consumable, RarityTier.Common, stackable: false);
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

    private void ShowVictoryResults(ICombatSession session, List<IActor> enemyActors, Dictionary<Guid,int> preLevels)
    {
        // Compute total EXP from enemies
        long totalExp = enemyActors.Sum(e => (long)(50 * System.Math.Pow(e.Level, 1.5)));

        // Build rewards per ally and apply experience
        var xpCalculator = new Fub.Implementations.Progression.ExperienceCalculator();
        var victoryScreen = new Fub.Implementations.Combat.VictoryScreen(xpCalculator);

        var rewardsList = new List<Fub.Implementations.Combat.VictoryRewards>();

        foreach (var ally in _state.Party.Members)
        {
            var job = ally.EffectiveClass;
            int before = preLevels.TryGetValue(ally.Id, out var lvl) ? lvl : ally.JobSystem.GetJobLevel(job).Level;

            // Capture pre-battle stat snapshot
            var preStats = ally.AllStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Modified);

            // Capture pre-known abilities if the actor has an ability book
            HashSet<Guid> preAbilities = new();
            if (ally is Fub.Interfaces.Abilities.IHasAbilityBook hasBook)
            {
                preAbilities = hasBook.AbilityBook.KnownAbilities.Select(a => a.Id).ToHashSet();
            }

            // Apply EXP to the job system (this should update level/experience)
            ally.JobSystem.AddExperience(job, totalExp);

            int after = ally.JobSystem.GetJobLevel(job).Level;
            int levelsGained = Math.Max(0, after - before);

            // Compute stat changes (compare modified values)
            var statChanges = new List<Fub.Implementations.Combat.VictoryStatChange>();
            foreach (var kv in ally.AllStats)
            {
                var type = kv.Key;
                var newVal = kv.Value.Modified;
                preStats.TryGetValue(type, out var oldVal);
                if (newVal != oldVal)
                {
                    statChanges.Add(new Fub.Implementations.Combat.VictoryStatChange { Stat = type, OldValue = oldVal, NewValue = newVal });
                }
            }

            // Compute newly learned abilities (difference between post and pre sets)
            var learned = new List<string>();
            if (ally is Fub.Interfaces.Abilities.IHasAbilityBook hasBookAfter)
            {
                foreach (var abil in hasBookAfter.AbilityBook.KnownAbilities)
                {
                    if (!preAbilities.Contains(abil.Id))
                    {
                        learned.Add(abil.Name);
                    }
                }
            }

            var rewards = new Fub.Implementations.Combat.VictoryRewards
            {
                ExperienceGained = totalExp,
                GoldGained = 0,
                ItemsDropped = new List<IItem>(),
                OldLevel = before,
                NewLevel = after,
                LevelsGained = levelsGained,
                StatChanges = statChanges,
                LearnedAbilities = learned
            };
            rewardsList.Add(rewards);
        }

        // Show a single consolidated victory screen for the whole party
        victoryScreen.DisplayPartyVictory(_state.Party.Members.ToList(), rewardsList, animationSpeedMs: 30);

        // After showing party victory, mark UI for refresh and log victory
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold green]\ud83c\udf89 Victory! \ud83c\udf89[/]").RuleStyle("green"));
        AnsiConsole.WriteLine();
        AddLog("Victory!");

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
        var choices = new List<string> { "Inspect Member", "View Stats", "Change Leader", "Manage Equipment", "Settings", "Back" };
        var choice = PromptNavigator.PromptChoice("[bold cyan]Party Menu[/]", choices, _state);
        if (choice == "Inspect Member") InspectPartyMember();
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
                AnsiConsole.MarkupLine("Tier: " + w.Tier);
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

    private Weapon CreateStarterWeaponForClass(ActorClass cls)
    {
        var (name, type, dmgType) = ClassWeaponMappings.GetStarterSpec(cls);
        return new Weapon(name, type, dmgType,
            _config.StartingWeaponMinDamage,
            _config.StartingWeaponMaxDamage,
            _config.StartingWeaponAttackSpeed,
            RarityTier.Common,
            EquipmentSlot.MainHand,
            requiredLevel: 1,
            allowedClasses: new[] { cls },
            statRequirements: null,
            tier: _config.StartingWeaponTier);
    }


    private void ShowHelpScreen()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]Help & Controls[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();
        var controlsTable = new Table().Border(TableBorder.Rounded);
        controlsTable.AddColumn("[bold]Action[/]");
        controlsTable.AddColumn("[bold]Key[/]");
        controlsTable.AddRow("Move", "WASD or Arrow Keys");
        controlsTable.AddRow("Interact/Confirm", "Space");
        controlsTable.AddRow("Search", "R");
        controlsTable.AddRow("Inventory", "I");
        controlsTable.AddRow("Party Menu", "P");
        controlsTable.AddRow("World Map", "M");
        controlsTable.AddRow("Help", "H");
        controlsTable.AddRow("Menu", "ESC");
        controlsTable.AddRow("Toggle Log", "L");
        AnsiConsole.Write(controlsTable);
        AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
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
        if (_state.Party.Leader is ActorBase leader)
            leader.Teleport(toX, toY);
        foreach (var member in _state.Party.Members)
        {
            if (member.Id == _state.Party.Leader.Id) continue;
            ((ActorBase)member).Teleport(toX, toY);
        }
        
        // FIX: Reset movement validators for all party members on the new map
        foreach (var member in _state.Party.Members)
            member.SetMovementValidator((x, y) => destMap.InBounds(x, y) && destMap.GetTile(x, y).TileType == MapTileType.Floor);
        
        PopulateMap(destMap, 0, 2, 3);
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
        int newX = leader.X + dx;
        int newY = leader.Y + dy;
        if (_state.CurrentMap == null) return false;
        if (!_state.CurrentMap.InBounds(newX, newY)) return false;
        if (_state.CurrentMap.GetTile(newX, newY).TileType != MapTileType.Floor) return false;
        foreach (var member in _state.Party.Members) member.TryMove(dx, dy);
        return true;
    }

    private void PopulateMap(IMap map, int enemyCount, int npcCount, int itemCount)
    {
        var populator = new MapContentPopulator();
        var cfg = new MapContentConfig
        {
            EnemyCount = enemyCount,
            NpcCount = npcCount,
            ItemCount = itemCount,
            MinDistanceFromLeader = 5
        };
        populator.Populate(map, _state.Party, cfg);
    }

    private void RenderGameWithUi(IMap map, IActor leader)
    {
        Console.Clear();

        // MAP INFO: Display above the map
        var mapInfoText = $"[bold cyan]{map.Name}[/] [grey]|[/] [yellow]{map.Kind}[/] [grey]|[/] [grey]Theme: {map.Theme}[/] [grey]|[/] [gold1]Gold: {_state.Party.Gold}g[/]";
        AnsiConsole.MarkupLine(mapInfoText);
        AnsiConsole.WriteLine();

        // Render map
        var mapText = _renderer.RenderToString(map, _state.Party);
        AnsiConsole.Write(new Markup(mapText));

        // Spacer
        AnsiConsole.WriteLine();

        // PARTY INFO: Enhanced display with leader indicator
        var leaderIcon = "\u2605"; // Star symbol for leader
        var partyHeader = $"[bold cyan]Party[/] [grey]|[/] [yellow]Leader: {leaderIcon} {leader.Name}[/] [grey]|[/] [green]Members: {_state.Party.Members.Count}/{_state.Party.MaxSize}[/]";
        AnsiConsole.Write(new Rule(partyHeader).RuleStyle("grey"));

        // Render party horizontally using Panels within Columns
        var members = _state.Party.Members;
        int memberCount = Math.Max(1, members.Count);
        int usableWidth = Math.Max(40, Console.WindowWidth - 6);
        // Estimate per-member content width (account for panel borders and spacing)
        int perMember = Math.Clamp((usableWidth / memberCount) - 6, 12, 28);

        var cards = new List<Spectre.Console.Rendering.IRenderable>(memberCount);
        foreach (var m in members)
        {
            var hp = m.GetStat(StatType.Health);
            var mp = m.GetStat(StatType.Mana);
            var tp = m.GetStat(StatType.Technical);

            string body = string.Join('\n', new[]
            {
                Bar("HP", hp.Current, Math.Max(1.0, hp.Modified), perMember, "red"),
                Bar("MP", mp.Current, Math.Max(1.0, mp.Modified), perMember, "dodgerblue1"),
                Bar("TP", tp.Current, Math.Max(1.0, tp.Modified), perMember, "green")
            });

            var panel = new Panel(new Markup(body))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(m.Id == leader.Id ? Color.Yellow : Color.Grey),
                Expand = true
            };
            
            // Add leader indicator to header
            var leaderIndicator = m.Id == leader.Id ? $"[yellow]{leaderIcon}[/] " : "";
            panel.Header = new PanelHeader($"{leaderIndicator}[bold]{m.Name}[/] [grey](Lv.{m.Level} {m.EffectiveClass})[/]");
            cards.Add(panel);
        }
        AnsiConsole.Write(new Columns(cards));
        AnsiConsole.WriteLine();

        // Controls/context hints
        var actions = BuildContextActionsString(map, leader);
        AnsiConsole.MarkupLine("[bold yellow]Controls:[/] " + actions);

        // Log (collapsible)
        int rows = _logExpanded ? Math.Min(_config.LogMaxExpandedRows, _log.Count) : Math.Min(1, _log.Count);
        if (rows > 0)
        {
            var toShow = _log.Skip(Math.Max(0, _log.Count - rows)).ToList();
            foreach (var line in toShow)
                AnsiConsole.MarkupLine("[grey]-[/] " + line);
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]Press [yellow]H[/] for help. Toggle log with [grey]L[/].[/]");
        }
    }
}

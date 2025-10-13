using Fub.Enums;
using Fub.Implementations.Core;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Items;
using Fub.Interfaces.Map;

namespace Fub.Implementations.Map.Objects;

public sealed class MapInteractableObject : EntityBase, IMapObject
{
    public MapObjectKind ObjectKind => MapObjectKind.Interactable;
    public int X { get; }
    public int Y { get; }
    public IItem? Item => null;
    public IActor? Actor => null;

    // Category hint, e.g., "Chest" or "Shop"
    public string Category { get; }

    public MapInteractableObject(string name, string category, int x, int y) : base(name)
    {
        Category = category;
        X = x;
        Y = y;
    }
}


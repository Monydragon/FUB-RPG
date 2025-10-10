using System;
using System.Text;
using System.Linq;
using Fub.Enums;
using Fub.Implementations.Parties;
using Fub.Interfaces.Actors;
using Fub.Interfaces.Map;
using Fub.Interfaces.Parties;
using Fub.Interfaces.Player;
using Spectre.Console;

namespace Fub.Implementations.Rendering;

public sealed class MapRenderer
{
    public int ViewWidth { get; set; } = 9;  // odd for centering
    public int ViewHeight { get; set; } = 9; // odd for centering

    public string RenderToString(IMap map, IParty party)
    {
        var leader = party.Leader;
        int halfW = ViewWidth / 2;
        int halfH = ViewHeight / 2;
        int startX = Math.Max(0, leader.X - halfW);
        int startY = Math.Max(0, leader.Y - halfH);
        int endX = Math.Min(map.Width - 1, startX + ViewWidth - 1);
        int endY = Math.Min(map.Height - 1, startY + ViewHeight - 1);
        if (endX - startX + 1 < ViewWidth)
            startX = Math.Max(0, endX - ViewWidth + 1);
        if (endY - startY + 1 < ViewHeight)
            startY = Math.Max(0, endY - ViewHeight + 1);

        var sb = new StringBuilder();
        for (int y = startY; y <= endY; y++)
        {
            var line = new StringBuilder();
            for (int x = startX; x <= endX; x++)
            {
                line.Append(RenderCell(map, party, leader, x, y));
                if (x < endX) line.Append(' ');
            }
            sb.AppendLine(line.ToString());
        }
        return sb.ToString();
    }

    public void Render(IMap map, IParty party)
    {
        // Don't clear here; caller manages screen. Render map area only.
        var mapText = RenderToString(map, party);
        AnsiConsole.Write(new Markup(mapText));
    }

    private static string Box(string inner)
    {
        inner = inner.Length > 5 ? inner.Substring(0,5) : inner;
        int padLeft = (5 - inner.Length) / 2;
        int padRight = 5 - inner.Length - padLeft;
        var content = new string(' ', padLeft) + inner + new string(' ', padRight);
        return "[[" + content + "]]"; // no color
    }

    private static string BoxColored(string inner, string color)
    {
        inner = inner.Length > 5 ? inner.Substring(0,5) : inner;
        int padLeft = (5 - inner.Length) / 2;
        int padRight = 5 - inner.Length - padLeft;
        var content = new string(' ', padLeft) + inner + new string(' ', padRight);
        return $"[{color}][[{content}]][/]";
    }

    private string RenderCell(IMap map, IParty party, IActor leader, int x, int y)
    {
        if (leader.X == x && leader.Y == y)
            return BoxColored("P", "green");

        bool hasEnemy = map.Objects.Any(o => o.X == x && o.Y == y && o.ObjectKind == MapObjectKind.Enemy);
        bool hasNpc   = map.Objects.Any(o => o.X == x && o.Y == y && o.ObjectKind == MapObjectKind.Npc);
        bool hasItem  = map.Objects.Any(o => o.X == x && o.Y == y && o.ObjectKind == MapObjectKind.Item);
        bool hasPortal = map.Objects.Any(o => o.X == x && o.Y == y && o.ObjectKind == MapObjectKind.Portal);
        if (hasEnemy || hasNpc || hasItem || hasPortal)
        {
            var sbInner = new StringBuilder();
            if (hasEnemy) sbInner.Append('E');
            if (hasNpc)   sbInner.Append('N');
            if (hasItem)  sbInner.Append('I');
            if (hasPortal) sbInner.Append('>');
            // Priority color Enemy > Portal > NPC > Item
            var color = hasEnemy ? "red" : hasPortal ? "violet" : hasNpc ? "blue" : "yellow";
            return BoxColored(sbInner.ToString(), color);
        }

        return map.GetTile(x, y).TileType switch
        {
            MapTileType.Wall => BoxColored("#####", "grey"),
            MapTileType.DoorClosed => BoxColored("+", "yellow"),
            MapTileType.DoorOpen => BoxColored("/", "yellow"),
            MapTileType.Water => BoxColored("~~~~~", "blue"),
            MapTileType.Lava => BoxColored("~~~~~", "red"),
            _ => BoxColored("", "grey")
        };
    }

    // Backwards compatibility
    public void Render(IMap map, IPlayer player)
    {
        var fauxParty = new Party((IActor)player);
        Render(map, fauxParty);
    }
}

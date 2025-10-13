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
        var mapText = RenderToString(map, party);
        AnsiConsole.Write(new Markup(mapText));
    }

    private static string BoxColored(string inner, string color)
    {
        inner = inner.Length > 5 ? inner.Substring(0,5) : inner;
        int padLeft = (5 - inner.Length) / 2;
        int padRight = 5 - inner.Length - padLeft;
        var content = new string(' ', padLeft) + inner + new string(' ', padRight);
        return $"[{color}][[{content}]][/]";
    }

    // Allow rich markup inside the box; when centered, add left padding to center the visible content
    private static string BoxMarkup(string innerMarkup, int visibleLen, bool center)
    {
        int padLeft = center ? (5 - visibleLen) / 2 : 0;
        int padRight = Math.Max(0, 5 - visibleLen - padLeft);
        return "[[" + new string(' ', padLeft) + innerMarkup + new string(' ', padRight) + "]]";
    }

    private static string ColorForChar(char ch)
    {
        return ch switch
        {
            'P' => "green",
            'E' => "red",
            'N' => "dodgerblue1",
            'I' => "yellow1",
            'C' => "orange1",
            'S' => "springgreen1",
            '>' => "violet",
            _ => "white"
        };
    }

    private string RenderCell(IMap map, IParty party, IActor leader, int x, int y)
    {
        if (leader.X == x && leader.Y == y)
        {
            return BoxMarkup("[green]P[/]", 1, center: true);
        }

        var objs = map.Objects.Where(o => o.X == x && o.Y == y).ToList();
        if (objs.Count > 0)
        {
            var chars = new StringBuilder();
            int visible = 0;
            void add(char c)
            {
                if (visible >= 5) return;
                string color = ColorForChar(c);
                chars.Append('[').Append(color).Append(']').Append(c).Append("[/]");
                visible += 1;
            }

            // Order by importance for rendering
            foreach (var o in objs.Where(o => o.ObjectKind == MapObjectKind.Enemy)) add('E');
            foreach (var o in objs.Where(o => o.ObjectKind == MapObjectKind.Npc)) add('N');
            foreach (var o in objs.Where(o => o.ObjectKind == MapObjectKind.Item)) add('I');
            foreach (var o in objs.Where(o => o.ObjectKind == MapObjectKind.Interactable))
            {
                // Try to pick a letter based on name/category
                char c = 'C';
                var name = o.Name?.ToLowerInvariant() ?? string.Empty;
                if (name.Contains("shop")) c = 'S';
                else if (name.Contains("chest")) c = 'C';
                add(c);
            }
            foreach (var o in objs.Where(o => o.ObjectKind == MapObjectKind.Portal)) add('>');
            return BoxMarkup(chars.ToString(), visible, center: false);
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

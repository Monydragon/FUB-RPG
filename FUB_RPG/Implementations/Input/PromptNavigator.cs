using System;
using System.Collections.Generic;
using System.Linq;
using Fub.Enums;
using Fub.Implementations.Game;
using Spectre.Console;

namespace Fub.Implementations.Input;

public static class PromptNavigator
{
    public static InputMode DefaultInputMode { get; set; } = InputMode.Keyboard;
    public static ControllerType DefaultControllerType { get; set; } = ControllerType.Unknown;

    public static string PromptChoice(string title, IList<string> choices, GameState state)
        => PromptChoice(title, choices, state.InputMode, state.ControllerType);

    public static T PromptChoice<T>(string title, IList<T> choices, GameState state) where T : notnull
        => PromptChoice(title, choices, state.InputMode, state.ControllerType);

    public static string PromptChoice(string title, IList<string> choices, InputMode mode, ControllerType controllerType)
        => PromptChoice(title, choices, mode, controllerType, renderBackground: null);

    public static T PromptChoice<T>(string title, IList<T> choices, InputMode mode, ControllerType controllerType) where T : notnull
        => PromptChoice(title, choices, mode, controllerType, renderBackground: null);

    // New overloads that allow passing a background renderer for controller UI
    public static string PromptChoice(string title, IList<string> choices, InputMode mode, ControllerType controllerType, Action? renderBackground)
    {
        if (mode == InputMode.Keyboard)
        {
            int reserved = 6;
            int minRows = 6;
            int viewport = Math.Max(minRows, Console.WindowHeight - reserved);
            int page = Math.Max(3, Math.Min(choices.Count, viewport));
            return AnsiConsole.Prompt(new SelectionPrompt<string>().Title(title).PageSize(page).AddChoices(choices));
        }
        return PromptChoiceController(title, choices, controllerType, renderBackground);
    }

    public static T PromptChoice<T>(string title, IList<T> choices, InputMode mode, ControllerType controllerType, Action? renderBackground) where T : notnull
    {
        if (mode == InputMode.Keyboard)
        {
            int reserved = 6;
            int minRows = 6;
            int viewport = Math.Max(minRows, Console.WindowHeight - reserved);
            int page = Math.Max(3, Math.Min(choices.Count, viewport));
            return AnsiConsole.Prompt(new SelectionPrompt<T>().Title(title).PageSize(page).AddChoices(choices));
        }
        var labels = choices.Select(c => c.ToString() ?? string.Empty).ToList();
        var pickedLabel = PromptChoiceController(title, labels, controllerType, renderBackground);
        int idx = labels.FindIndex(l => l == pickedLabel);
        return idx >= 0 ? choices[idx] : choices.First();
    }

    private static string PromptChoiceController(string title, IList<string> choices, ControllerType type)
        => PromptChoiceController(title, choices, type, renderBackground: null);

    private static string PromptChoiceController(string title, IList<string> choices, ControllerType type, Action? renderBackground)
    {
        if (choices.Count == 0)
            return string.Empty;

        int index = 0;     // selected index in full list
        int top = 0;       // start row of viewport in full list

        while (true)
        {
            AnsiConsole.Clear();

            // Allow caller to render surrounding UI (e.g., combat status, details)
            renderBackground?.Invoke();

            // Header
            if (!string.IsNullOrWhiteSpace(title))
            {
                AnsiConsole.Write(new Rule($"[bold cyan]{title}[/]").RuleStyle("cyan"));
                AnsiConsole.WriteLine();
            }

            // Compute viewport size based on console height (reserve header/footer)
            int reserved = 6; // room for header spacing + controls + indicators
            int minRows = 6;  // ensure minimally usable window
            int window = Math.Min(choices.Count, Math.Max(minRows, Console.WindowHeight - reserved));

            // Clamp top so that [top, top+window) is within bounds
            if (top < 0) top = 0;
            if (top > Math.Max(0, choices.Count - window)) top = Math.Max(0, choices.Count - window);
            // Ensure index is visible; adjust top accordingly
            if (index < top) top = index;
            if (index >= top + window) top = index - window + 1;

            // Up indicator if scrolled
            if (top > 0)
                AnsiConsole.MarkupLine("[grey]▲ more...[/]");

            // Render visible slice
            var table = new Table();
            table.Border(TableBorder.None);
            table.ShowHeaders = false;
            table.AddColumn(new TableColumn(string.Empty));
            table.AddColumn(new TableColumn(string.Empty));
            for (int i = 0; i < window; i++)
            {
                int j = top + i;
                bool sel = (j == index);
                string pointer = sel ? "[yellow]\u27a4[/]" : " ";
                string label = sel ? $"[bold]{choices[j]}[/]" : choices[j];
                table.AddRow(pointer, label);
            }
            AnsiConsole.Write(table);

            // Down indicator if more items below
            if (top + window < choices.Count)
                AnsiConsole.MarkupLine("[grey]▼ more...[/]");

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"{ControllerUi.MovePad(type)} Move  {ControllerUi.Confirm(type)} Select  {ControllerUi.Cancel(type)} Back  [grey]Left/Right: Page[/]");

            var action = InputManager.ReadNextAction(InputMode.Controller);
            switch (action)
            {
                case InputAction.MoveUp:
                    index = (index - 1 + choices.Count) % choices.Count;
                    if (index < top) top = index; // scroll up
                    break;
                case InputAction.MoveDown:
                    index = (index + 1) % choices.Count;
                    if (index >= top + window) top = index - window + 1; // scroll down
                    break;
                case InputAction.MoveLeft: // page up
                    index = Math.Max(0, index - window);
                    top = Math.Max(0, top - window);
                    break;
                case InputAction.MoveRight: // page down
                    index = Math.Min(choices.Count - 1, index + window);
                    top = Math.Min(Math.Max(0, choices.Count - window), top + window);
                    break;
                case InputAction.Interact: // confirm
                    return choices[index];
                case InputAction.Search:   // treat B/Cancel as go back if possible
                case InputAction.Menu:
                    {
                        int backIdx = IndexOfInsensitive(choices, "Back");
                        if 
                            (backIdx >= 0) return choices[backIdx];
                        break;
                    }
            }
        }
    }

    private static int IndexOfInsensitive(IList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}

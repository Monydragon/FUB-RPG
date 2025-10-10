using Fub.Enums;

namespace Fub.Implementations.Input;

public static class ControllerUi
{
    public static string Confirm(ControllerType t) => t switch
    {
        ControllerType.PlayStation => "[red]✕[/]", // Cross
        ControllerType.Switch => "[red]A[/]",
        _ => "[red]A[/]" // Xbox default
    };

    public static string Cancel(ControllerType t) => t switch
    {
        ControllerType.PlayStation => "[grey]◯[/]",
        ControllerType.Switch => "[grey]B[/]",
        _ => "[grey]B[/]"
    };

    public static string Inventory(ControllerType t) => t switch
    {
        ControllerType.PlayStation => "[green]□[/]", // Square
        ControllerType.Switch => "[green]Y[/]",
        _ => "[green]X[/]"
    };

    public static string Party(ControllerType t) => t switch
    {
        ControllerType.PlayStation => "[magenta]△[/]", // Triangle
        ControllerType.Switch => "[magenta]X[/]",
        _ => "[magenta]Y[/]"
    };

    public static string Menu(ControllerType t) => t switch
    {
        ControllerType.PlayStation => "[yellow]Share[/]", // Back-equivalent
        ControllerType.Switch => "[yellow]-[/]",
        _ => "[yellow]View[/]"
    };

    public static string Help(ControllerType t) => t switch
    {
        ControllerType.PlayStation => "[grey]Options[/]",
        ControllerType.Switch => "[grey]+[/]",
        _ => "[grey]Start[/]"
    };

    public static string MovePad(ControllerType t) => "[cyan]D-Pad/LS[/]"; // common

    // New: log toggle button label
    public static string Log(ControllerType t) => t switch
    {
        ControllerType.PlayStation => "[grey]R1[/]",
        ControllerType.Switch => "[grey]R[/]",
        _ => "[grey]RB[/]"
    };
}

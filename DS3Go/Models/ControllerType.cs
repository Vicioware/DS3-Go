namespace DS3Go.Models;

public enum ControllerType
{
    Original,
    KnownClone,
    PossibleClone,
    Unknown
}

public static class ControllerTypeExtensions
{
    public static string DisplayName(this ControllerType type) => type switch
    {
        ControllerType.Original => "Original",
        ControllerType.KnownClone => "Clon Conocido",
        ControllerType.PossibleClone => "Posible Clon",
        ControllerType.Unknown => "Desconocido",
        _ => type.ToString()
    };
}

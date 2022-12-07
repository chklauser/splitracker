using System;

namespace Splitracker.Domain;

[Flags]
public enum CharacterPermissions : int
{
    None = 0,
    ViewInfo = 1,
    ViewStats = 2,
    EditStats = 4,
    RemoveFromGroup = 8,
    InteractOnTimeline = 16,
}
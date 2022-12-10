using System;

namespace Splitracker.Domain;

[Flags]
public enum CharacterPermissions : int
{
    None = 0,
    /// <summary>
    /// Name, Avatar
    /// </summary>
    ViewInfo = 1,
    ViewStats = 2,
    /// <summary>
    /// Add/remove points
    /// </summary>
    EditResources = 4,
    /// <summary>
    /// Changes character values (name, pool size, actions, etc.)
    /// </summary>
    EditStats = 8,
    RemoveFromGroup = 16,
    InteractOnTimeline = 32,
}
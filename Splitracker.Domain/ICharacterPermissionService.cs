using System.Security.Claims;

namespace Splitracker.Domain;

public interface ICharacterPermissionService
{
    CharacterPermissions InTheContextOfGroup(Character character, Group group);
}
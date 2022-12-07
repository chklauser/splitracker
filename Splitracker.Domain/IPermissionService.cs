using System.Security.Claims;

namespace Splitracker.Domain;

public interface IPermissionService
{
    CharacterPermissions InTheContextOfGroup(Character character, Group group);
}
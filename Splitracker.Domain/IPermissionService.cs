using System.Security.Claims;

namespace Splitracker.Domain;

public interface IPermissionService
{
    CharacterPermissions InTheContextOf(Character character, Group group);
    CharacterPermissions InTheContextOf(Character character, Timeline timeline);
}
using System.Security.Claims;
using System.Threading.Tasks;

namespace Splitracker.Domain;

public interface IUserRepository
{
    Task<string> GetUserIdAsync(ClaimsPrincipal principal);
}
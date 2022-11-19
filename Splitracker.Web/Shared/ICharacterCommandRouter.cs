using System.Threading.Tasks;
using Splitracker.Domain.Commands;

namespace Splitracker.Web.Shared;

public interface ICharacterCommandRouter
{
    Task ApplyAsync(ICharacterCommand command);
}
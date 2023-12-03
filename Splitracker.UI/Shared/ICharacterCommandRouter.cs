using System.Threading.Tasks;
using Splitracker.Domain.Commands;

namespace Splitracker.UI.Shared;

public interface ICharacterCommandRouter
{
    Task ApplyAsync(ICharacterCommand command);
}
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Splitracker.Domain;

namespace Splitracker.Web.Shared.Timelines;

public interface ITimelineDispatcher
{
    Task<IEnumerable<Character>> SearchCharactersAsync(string searchTerm, CancellationToken cancellationToken);
}
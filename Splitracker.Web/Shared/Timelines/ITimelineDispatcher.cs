using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Splitracker.Domain;
using Splitracker.Domain.Commands;

namespace Splitracker.Web.Shared.Timelines;

public interface ITimelineDispatcher
{
    Task<IEnumerable<Character>> SearchCharactersAsync(string searchTerm, CancellationToken cancellationToken);

    Task ApplyCommandAsync(TimelineCommand command);
}
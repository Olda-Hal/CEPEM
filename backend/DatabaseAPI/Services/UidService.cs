using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;

namespace DatabaseAPI.Services;

public interface IUidService
{
    /// <summary>
    /// Returns the lowest unused numeric UID (gap-filling). Callers must
    /// still handle an insert collision: two concurrent allocations can
    /// pick the same candidate. A unique index on Persons.UID is the
    /// complete fix - pending an audit of existing data for duplicates.
    /// </summary>
    Task<string> GetNextAvailableUidAsync();
}

public class UidService : IUidService
{
    private readonly DatabaseContext _context;

    public UidService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<string> GetNextAvailableUidAsync()
    {
        var existingUids = await _context.Persons
            .Select(p => p.UID)
            .Where(uid => !string.IsNullOrWhiteSpace(uid))
            .ToListAsync();

        var numericUids = existingUids
            .Where(uid => int.TryParse(uid, out _))
            .Select(int.Parse)
            .OrderBy(uid => uid)
            .ToList();

        var nextUid = 1;
        foreach (var existingUid in numericUids)
        {
            if (existingUid == nextUid)
            {
                nextUid++;
            }
            else if (existingUid > nextUid)
            {
                break;
            }
        }

        return nextUid.ToString();
    }
}

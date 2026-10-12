using InventStock.Application.Common;
using InventStock.Application.Interfaces;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Services;

internal static class CounterAuthorization
{
    public static async Task<Guid> RequireAsync(AppDbContext db, ICurrentUserService user, CancellationToken ct, bool adminOnly = false)
    {
        var actor = user.UserId ?? throw new UnauthorizedAccessException();
        if (!await db.Users.AnyAsync(x => x.Id == actor && x.IsActive, ct) ||
            !await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                    where ur.UserId == actor && (r.Name == RoleNames.Admin || (!adminOnly && r.Name == RoleNames.Staff)) select ur).AnyAsync(ct))
            throw new UnauthorizedAccessException();
        return actor;
    }
}

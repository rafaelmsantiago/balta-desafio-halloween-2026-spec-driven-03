using Encurtador.Application.Links;
using Microsoft.EntityFrameworkCore;

namespace Encurtador.Infrastructure.Persistence;

internal sealed class EfLinkRepository(LinksDbContext db) : ILinkRepository
{
    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        db.Links.AnyAsync(l => l.Code == code, ct);

    public async Task<AddLinkResult> AddAsync(Link link, CancellationToken ct)
    {
        db.Links.Add(link);
        try
        {
            await db.SaveChangesAsync(ct);
            return AddLinkResult.Added;
        }
        catch (DbUpdateException ex) when (UniqueViolation.IsUniqueViolation(ex))
        {
            // Sem isso, um retry no mesmo escopo reenviaria este insert.
            db.Entry(link).State = EntityState.Detached;
            return AddLinkResult.DuplicateCode;
        }
    }

    public Task<Link?> GetByCodeAsync(string code, CancellationToken ct) =>
        db.Links.AsNoTracking().SingleOrDefaultAsync(l => l.Code == code, ct);

    public async Task<bool> TryRegisterAccessAsync(string code, DateTime nowUtc, CancellationToken ct)
    {
        // Um único UPDATE: o contador nunca é lido e regravado pela aplicação (D2).
        var affected = await db.Links
            .Where(l => l.Code == code && (l.ExpiresAt == null || l.ExpiresAt > nowUtc))
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.AccessCount, l => l.AccessCount + 1), ct);

        return affected == 1;
    }
}

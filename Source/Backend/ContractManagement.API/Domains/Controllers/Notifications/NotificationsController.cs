using System.Text;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Notifications;
using ContractManagement.Filter;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Domains.Controllers.Notifications;

[ApiController]
[Route("api/notifications")]
[SessionAuthorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly DbDtctechContext _db;
    private readonly IReadOnlyList<INotificationResourcePolicy> _policies;
    public NotificationsController(DbDtctechContext db, IEnumerable<INotificationResourcePolicy> policies)
    {
        _db = db;
        _policies = policies.ToList();
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? cursor, [FromQuery] int limit = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 50 || !TryDecodeCursor(cursor, out var position)
            || status is not (null or "all" or "read" or "unread"))
            return BadRequest(new { code = "InvalidNotificationPage", message = "Trang thông báo không hợp lệ." });
        var query = VisibleRows();
        if (status == "read") query = query.Where(x => x.ReadAtUtc != null);
        if (status == "unread") query = query.Where(x => x.ReadAtUtc == null);
        if (position.HasValue)
        {
            var (created, id) = position.Value;
            query = query.Where(x => x.CreatedAtUtc < created
                || (x.CreatedAtUtc == created && x.NotificationRecipientId < id));
        }
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.NotificationRecipientId)
            .Join(_db.TblNotifications.AsNoTracking(),
                recipient => recipient.NotificationId,
                notification => notification.NotificationId,
                (recipient, notification) => new { recipient, notification })
            .Take(limit + 1)
            .Select(x => new NotificationItem(
                x.recipient.NotificationRecipientId, x.recipient.NotificationId,
                x.notification.Title, x.notification.Body, x.notification.SubjectType,
                x.notification.SubjectId, x.notification.ActionKey,
                x.recipient.CreatedAtUtc, x.recipient.ReadAtUtc))
            .ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveAt(limit);
        var last = rows.LastOrDefault();
        return Ok(new { items = rows,
            nextCursor = hasMore && last is not null
                ? EncodeCursor(last.CreatedAtUtc, last.NotificationRecipientId) : null });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken) =>
        Ok(new { count = await VisibleRows().CountAsync(x => x.ReadAtUtc == null, cancellationToken) });

    [HttpPatch("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        var row = await VisibleRows().SingleOrDefaultAsync(
            x => x.NotificationRecipientId == id, cancellationToken);
        if (row is null) return NotFound();
        if (row.ReadAtUtc is null)
        {
            // Conditional update makes concurrent mark-read idempotent and preserves the first timestamp.
            await _db.TblNotificationRecipients
                .Where(x => x.NotificationRecipientId == id && x.ReadAtUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReadAtUtc, DateTime.UtcNow), cancellationToken);
        }
        return NoContent();
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var ids = VisibleRows().Where(x => x.ReadAtUtc == null).Select(x => x.NotificationRecipientId);
        var changed = await _db.TblNotificationRecipients
            .Where(x => ids.Contains(x.NotificationRecipientId) && x.ReadAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReadAtUtc, DateTime.UtcNow), cancellationToken);
        return Ok(new { changed });
    }

    private IQueryable<TblNotificationRecipient> VisibleRows()
    {
        var employee = EmployeeAuthorizationContext.GetEmployee(HttpContext)!;
        var allowed = _policies.Select(policy => policy.VisibleNotificationIds(employee)).ToList();
        if (allowed.Count == 0)
            return _db.TblNotificationRecipients.AsNoTracking().Where(x => false);
        var ids = allowed.Aggregate((left, right) => left.Union(right));
        return _db.TblNotificationRecipients.AsNoTracking().Where(x =>
            x.EmployeeId == employee.EmployeeId && ids.Contains(x.NotificationId));
    }

    private static string EncodeCursor(DateTime createdAt, long id) =>
        Convert.ToBase64String(Encoding.ASCII.GetBytes($"{createdAt.Ticks}:{id}"));

    private static bool TryDecodeCursor(string? cursor, out (DateTime Created, long Id)? position)
    {
        position = null;
        if (string.IsNullOrWhiteSpace(cursor)) return true;
        try
        {
            var fields = Encoding.ASCII.GetString(Convert.FromBase64String(cursor)).Split(':');
            if (fields.Length != 2 || !long.TryParse(fields[0], out var ticks)
                || !long.TryParse(fields[1], out var id) || id <= 0) return false;
            position = (new DateTime(ticks, DateTimeKind.Utc), id);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private sealed record NotificationItem(long NotificationRecipientId, long NotificationId,
        string Title, string? Body, string SubjectType, int SubjectId, string? ActionKey,
        DateTime CreatedAtUtc, DateTime? ReadAtUtc);
}

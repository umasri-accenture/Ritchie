using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Richie.Application.Abstractions;
using Richie.Application.Assets;
using Richie.Application.Authentication;
using Richie.Domain.Assets;
using Richie.Domain.Auditing;
using Richie.Domain.Notifications;
using Richie.Infrastructure.Auditing;
using Richie.Infrastructure.Notifications;
using Richie.Infrastructure.Persistence;

namespace Richie.Infrastructure.Assets;

public sealed class SipService : ISipService
{
    private const string Module = "Assets";

    private readonly IAppDbContextFactory _factory;
    private readonly IUserSession _session;
    private readonly IClock _clock;

    public SipService(IAppDbContextFactory factory, IUserSession session, IClock clock)
    {
        _factory = factory;
        _session = session;
        _clock = clock;
    }

    private Guid UserId => _session.UserId ?? throw new InvalidOperationException("No authenticated user.");

    public SipScheduleDto? GetSchedule(Guid assetId)
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        SipSchedule? s = db.SipSchedules.AsNoTracking()
            .FirstOrDefault(x => x.AssetId == assetId && x.UserId == userId);
        return s is null
            ? null
            : new SipScheduleDto(s.IsEnabled, s.Amount, s.DayOfMonth, s.Frequency, s.StartDate, s.NextRunDateUtc, s.LastRunUtc);
    }

    public void SaveSchedule(Guid assetId, SipScheduleInput input)
    {
        Guid userId = UserId;
        DateTime now = _clock.UtcNow;

        using RichieDbContext db = _factory.Create();
        Asset? asset = db.Assets.FirstOrDefault(a => a.Id == assetId && a.UserId == userId);
        if (asset is null)
            return;

        SipSchedule? schedule = db.SipSchedules.FirstOrDefault(x => x.AssetId == assetId);
        bool isNew = schedule is null;
        schedule ??= new SipSchedule { Id = Guid.NewGuid(), AssetId = assetId, UserId = userId, CreatedUtc = now };

        schedule.IsEnabled = input.IsEnabled;
        schedule.Amount = input.Amount;
        schedule.DayOfMonth = Math.Clamp(input.DayOfMonth, 1, 28);
        schedule.Frequency = input.Frequency;
        schedule.StartDate = input.StartDate;
        schedule.UpdatedUtc = now;

        DateTime from = input.StartDate.Date > now.Date ? input.StartDate.Date : now.Date;
        schedule.NextRunDateUtc = ComputeNextRun(from, schedule.DayOfMonth);

        if (isNew)
            db.SipSchedules.Add(schedule);

        AuditWriter.Add(db, userId, now, Module, isNew ? AuditAction.Create : AuditAction.Update,
            nameof(SipSchedule), schedule.Id,
            $"{(input.IsEnabled ? "Enabled" : "Disabled")} SIP for '{asset.Name}'.");
        db.SaveChanges();
    }

    public IReadOnlyList<DateTime> GetUpcomingInstallments(Guid assetId, int count = 4)
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        SipSchedule? s = db.SipSchedules.AsNoTracking()
            .FirstOrDefault(x => x.AssetId == assetId && x.UserId == userId);
        if (s is null || !s.IsEnabled)
            return [];

        var dates = new List<DateTime>(count);
        DateTime d = s.NextRunDateUtc;
        for (int i = 0; i < count; i++)
        {
            dates.Add(d);
            d = d.AddMonths((int)s.Frequency);
        }
        return dates;
    }

    public IReadOnlyList<UpcomingSipDto> GetUpcomingSips(int withinDays = 30)
    {
        Guid userId = UserId;
        DateTime cutoff = _clock.UtcNow.AddDays(withinDays);

        using RichieDbContext db = _factory.Create();
        return (from s in db.SipSchedules.AsNoTracking()
                join a in db.Assets.AsNoTracking() on s.AssetId equals a.Id
                where s.UserId == userId && s.IsEnabled && s.NextRunDateUtc <= cutoff
                orderby s.NextRunDateUtc
                select new UpcomingSipDto(a.Id, a.Name, s.Amount, s.NextRunDateUtc, s.Frequency))
            .ToList();
    }

    public IReadOnlyList<SipContributionDto> GetHistory(Guid assetId)
    {
        Guid userId = UserId;
        using RichieDbContext db = _factory.Create();
        bool owns = db.Assets.Any(a => a.Id == assetId && a.UserId == userId);
        if (!owns)
            return [];

        return db.SipContributions.AsNoTracking()
            .Where(c => c.AssetId == assetId)
            .OrderByDescending(c => c.DateUtc)
            .Select(c => new SipContributionDto(c.DateUtc, c.Amount))
            .ToList();
    }

    public int ProcessDueSips(DateTime nowUtc)
    {
        using RichieDbContext db = _factory.Create();
        List<SipSchedule> due = db.SipSchedules
            .Where(s => s.IsEnabled && s.NextRunDateUtc <= nowUtc)
            .ToList();

        int posted = 0;
        foreach (SipSchedule schedule in due)
        {
            Asset? asset = db.Assets.FirstOrDefault(a => a.Id == schedule.AssetId);
            if (asset is null)
                continue;

            while (schedule.NextRunDateUtc <= nowUtc)
            {
                asset.InvestedAmount += schedule.Amount;
                asset.CurrentValue += schedule.Amount;
                asset.UpdatedUtc = nowUtc;

                db.SipContributions.Add(new SipContribution
                {
                    Id = Guid.NewGuid(),
                    AssetId = asset.Id,
                    SipScheduleId = schedule.Id,
                    DateUtc = schedule.NextRunDateUtc,
                    Amount = schedule.Amount
                });

                string amount = Richie.Application.Common.CurrencyFormatter.Format(schedule.Amount);
                NotificationWriter.Add(db, schedule.UserId, nowUtc, NotificationType.SipPosted,
                    "SIP invested", $"{amount} was added to '{asset.Name}'.");
                AuditWriter.Add(db, schedule.UserId, nowUtc, Module, AuditAction.Update, nameof(Asset), asset.Id,
                    $"SIP auto-posted {amount} to '{asset.Name}'.");

                schedule.LastRunUtc = nowUtc;
                schedule.NextRunDateUtc = schedule.NextRunDateUtc.AddMonths((int)schedule.Frequency);
                posted++;
            }
        }

        db.SaveChanges();
        return posted;
    }

    private static DateTime ComputeNextRun(DateTime fromDateUtc, int day)
    {
        var candidate = new DateTime(fromDateUtc.Year, fromDateUtc.Month, day, 0, 0, 0, DateTimeKind.Utc);
        if (candidate < fromDateUtc.Date)
            candidate = candidate.AddMonths(1);
        return candidate;
    }
}

using FluentAssertions;
using GaiaSkyline.Application.Admin;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Infrastructure.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class ManualSyncReminderServiceTests
{
    private static ManualSyncItem Item(bool overdue) =>
        new("GS-0001", "Guest", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 4), ManualSyncAction.BlockInHostify,
            new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc), overdue);

    [Fact]
    public async Task Sends_one_email_when_an_item_is_overdue()
    {
        var email = new RecordingEmailSender();
        var service = Build([Item(overdue: true), Item(overdue: false)], email);

        await service.SendDueRemindersAsync(CancellationToken.None);

        email.Sent.Should().ContainSingle();
        email.Sent[0].ToAddress.Should().Be("owner@test");
        email.Sent[0].Subject.Should().Contain("Hostify");
    }

    [Fact]
    public async Task Stays_silent_when_outstanding_but_nothing_overdue()
    {
        var email = new RecordingEmailSender();
        var service = Build([Item(overdue: false)], email);

        await service.SendDueRemindersAsync(CancellationToken.None);

        email.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Stays_silent_when_there_is_nothing_to_do()
    {
        var email = new RecordingEmailSender();
        var service = Build([], email);

        await service.SendDueRemindersAsync(CancellationToken.None);

        email.Sent.Should().BeEmpty();
    }

    private static ManualSyncReminderService Build(IReadOnlyList<ManualSyncItem> items, IEmailSender email) =>
        new(new FakeDashboard(items), email,
            TestOptions.Snapshot(new EmailOptions { OwnerAddress = "owner@test", FromName = "Gaia Skyline" }),
            NullLogger<ManualSyncReminderService>.Instance);

    private sealed class FakeDashboard(IReadOnlyList<ManualSyncItem> items) : IDashboardService
    {
        public Task<IReadOnlyList<ManualSyncItem>> GetOutstandingManualSyncAsync(CancellationToken cancellationToken) =>
            Task.FromResult(items);

        public Task<DashboardSummary> GetAsync(DateOnly month, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task MarkSyncedAsync(string reference, string? note, Guid? actorUserId, string? actorIp, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}

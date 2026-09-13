using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OrderFulfillment.Application.Interfaces;
using OrderFulfillment.Domain.Common;
using OrderFulfillment.Domain.Entities;
using OrderFulfillment.Infrastructure.Outbox;

namespace OrderFulfillment.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ConvertDomainEventsToOutboxMessages();

        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ConvertDomainEventsToOutboxMessages()
    {
        List<IDomainEvent> domainEvents = ExtractDomainEvents();

        if (!domainEvents.Any())
            return;

        List<OutboxMessage> outboxMessages = domainEvents.Select(MapToOutboxMessage).ToList();

        OutboxMessages.AddRange(outboxMessages);
    }

    private List<IDomainEvent> ExtractDomainEvents()
    {
        List<AggregateRoot> aggregateRoots = ChangeTracker.Entries<AggregateRoot>()
            .Select((EntityEntry<AggregateRoot> entry) => entry.Entity)
            .Where((AggregateRoot entity) => entity.DomainEvents.Any())
            .ToList();

        List<IDomainEvent> domainEvents = aggregateRoots
            .SelectMany((AggregateRoot entity) => entity.DomainEvents)
            .ToList();

        foreach (AggregateRoot aggregate in aggregateRoots)
        {
            aggregate.ClearDomainEvents();
        }

        return domainEvents;
    }

    private static OutboxMessage MapToOutboxMessage(IDomainEvent domainEvent)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOnUtc = DateTime.UtcNow,
            Type = domainEvent.GetType().Name,
            Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType())
        };
    }
}
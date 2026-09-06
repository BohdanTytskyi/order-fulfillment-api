using MediatR;
using OrderFulfillment.Domain.Common;

namespace OrderFulfillment.Application.Common.Events;

public interface IDomainEventNotification : INotification
{
    IDomainEvent DomainEvent { get; }
}

public record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : IDomainEventNotification
    where TDomainEvent : IDomainEvent
{
    IDomainEvent IDomainEventNotification.DomainEvent => DomainEvent;
}

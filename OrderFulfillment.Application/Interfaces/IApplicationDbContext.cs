using Microsoft.EntityFrameworkCore;
using OrderFulfillment.Domain.Entities;

namespace OrderFulfillment.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Product> Products { get; }
    DbSet<Order> Orders { get; }
}

namespace OrderFulfillment.Application.Common.Exceptions;

public abstract class NotFoundException : Exception
{
    protected NotFoundException(string message) : base(message)
    {
    }
}

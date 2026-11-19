namespace KaryaJayaFootwear.Models;

public sealed class InsufficientStockException : Exception
{
    public InsufficientStockException(string message) : base(message)
    {
    }

    public InsufficientStockException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

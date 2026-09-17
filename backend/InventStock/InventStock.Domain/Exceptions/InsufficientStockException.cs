namespace InventStock.Domain.Exceptions;

public sealed class InsufficientStockException : DomainException
{
    public InsufficientStockException(string partNumber, int available, int requested)
        : base($"Insufficient stock for part '{partNumber}'. Available: {available}, requested: {requested}.")
    {
        PartNumber = partNumber;
        Available = available;
        Requested = requested;
    }

    public string PartNumber { get; }

    public int Available { get; }

    public int Requested { get; }
}

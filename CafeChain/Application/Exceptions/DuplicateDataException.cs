namespace CafeChain.Application.Exceptions;

public sealed class DuplicateDataException : Exception
{
    public DuplicateDataException(string message, string? field = null, Exception? innerException = null)
        : base(message, innerException) => Field = field;

    public string? Field { get; }
}

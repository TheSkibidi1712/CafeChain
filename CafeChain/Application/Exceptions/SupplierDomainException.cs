namespace CafeChain.Application.Exceptions
{
    public sealed class SupplierDomainException : InvalidOperationException
    {
        public SupplierDomainException(string code, string message, object? data = null)
            : base(message)
        {
            Code = code;
            DataPayload = data;
        }

        public string Code { get; }
        public object? DataPayload { get; }
    }
}

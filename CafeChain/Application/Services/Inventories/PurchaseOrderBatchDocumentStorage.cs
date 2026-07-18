using CafeChain.Application.Interfaces.Inventories;

namespace CafeChain.Application.Services.Inventories;

public sealed class PurchaseOrderBatchDocumentStorage : IPurchaseOrderBatchDocumentStorage
{
    private readonly string _root;

    public PurchaseOrderBatchDocumentStorage(IConfiguration configuration)
    {
        var configured = configuration["Procurement:PurchaseOrderDocumentStorageRoot"];
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CafeChain", "ProcurementDocuments")
            : Path.GetFullPath(configured);
    }

    public async Task SaveAsync(string storageReference, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var path = Resolve(storageReference);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, content.ToArray(), cancellationToken);
            File.Move(temporaryPath, path, false);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public async Task<byte[]?> ReadAsync(string storageReference, CancellationToken cancellationToken = default)
    {
        var path = Resolve(storageReference);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }

    public Task DeleteAsync(string storageReference, CancellationToken cancellationToken = default)
    {
        var path = Resolve(storageReference);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string storageReference)
    {
        if (string.IsNullOrWhiteSpace(storageReference) || Path.IsPathRooted(storageReference))
            throw new InvalidOperationException("Storage reference không hợp lệ.");
        var root = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, storageReference.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Storage reference nằm ngoài thư mục dữ liệu ứng dụng.");
        return path;
    }
}

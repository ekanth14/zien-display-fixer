namespace ZienDisplayFixer.Core.Interfaces;

public interface IEdidReader
{
    Task<byte[]?> ReadRawEdidAsync(string displayId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, byte[]>> ReadAllEdidsAsync(CancellationToken ct = default);
}

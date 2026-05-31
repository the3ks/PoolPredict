namespace PoolPredict.Api.Domain.Common;

public static class Ids
{
    public static Guid NewId() => Guid.CreateVersion7();
}

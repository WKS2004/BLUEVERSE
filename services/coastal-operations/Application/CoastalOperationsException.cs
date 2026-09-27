namespace Blueverse.CoastalOperations.Application;

public sealed class CoastalOperationsException(int statusCode, string code, string title, string detail)
    : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public string Title { get; } = title;
}

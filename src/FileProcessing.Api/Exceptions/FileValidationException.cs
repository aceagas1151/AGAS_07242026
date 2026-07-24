namespace FileProcessing.Api.Exceptions;

public class FileValidationException : Exception
{
    public int StatusCode { get; }

    public FileValidationException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}

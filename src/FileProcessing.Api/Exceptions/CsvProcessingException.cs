namespace FileProcessing.Api.Exceptions;

public class CsvProcessingException : Exception
{
    public CsvProcessingException(string message) : base(message)
    {
    }
}

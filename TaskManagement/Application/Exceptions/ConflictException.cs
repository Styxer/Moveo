namespace Application.Exceptions
{
    // Exception for conflicts, like creating a resource that already exists
    public class ConflictException : ApplicationException
    {
        public ConflictException(string message) : base(message)
        {
        }

        public ConflictException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
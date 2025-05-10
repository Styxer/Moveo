namespace Application.Exceptions
{
    // Base class for application-specific exceptions
    public abstract class ApplicationException : Exception
    {
        protected ApplicationException(string message) : base(message)
        {
        }

        protected ApplicationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
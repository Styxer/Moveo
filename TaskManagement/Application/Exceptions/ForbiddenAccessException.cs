namespace Application.Exceptions
{
    // Exception for when a user attempts to access a resource they don't have permission for
    public class ForbiddenAccessException(string message) : ApplicationException(message)
    {
        public ForbiddenAccessException() : this("Access to the requested resource is forbidden.")
        {
        }
    }
}
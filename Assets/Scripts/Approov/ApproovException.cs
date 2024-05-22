using System;

namespace Approov {
    /*
    *   Approov SDK exceptions
    */
    public class ApproovException : Exception
    {
        public bool ShouldRetry;
        public new string Message;
        public ApproovException()
        {
            ShouldRetry = false;
            Message = "ApproovException: Unknown Error.";
        }

        public ApproovException(string message) : base(message)
        {
            ShouldRetry = false;
            Message = message;
        }

        public ApproovException(string message, bool shouldRetry) : base(message)
        {
            ShouldRetry = shouldRetry;
            Message = message;
        }
    } // ApproovException class
    // initialization failure
    public class InitializationFailureException : ApproovException
    {
        public InitializationFailureException(string message) : base(message)
        {
            ShouldRetry = false;
            Message = message;
        }
        public InitializationFailureException(string message, bool shouldRetry) : base(message)
        {
            ShouldRetry = shouldRetry;
            Message = message;
        }
    }
    // configuration failure
    public class ConfigurationFailureException : ApproovException
    {
        public ConfigurationFailureException(string message) : base(message)
        {
            ShouldRetry = false;
            Message = message;
        }
        public ConfigurationFailureException(string message, bool shouldRetry) : base(message)
        {
            ShouldRetry = shouldRetry;
            Message = message;
        }
    }
    // pinning error
    public class PinningErrorException : ApproovException
    {
        public PinningErrorException(string message) : base(message)
        {
            ShouldRetry = false;
            Message = message;
        }
        public PinningErrorException(string message, bool shouldRetry) : base(message)
        {
            ShouldRetry = shouldRetry;
            Message = message;
        }
    }
    // networking error
    public class NetworkingErrorException : ApproovException
    {
        public NetworkingErrorException(string message) : base(message)
        {
            ShouldRetry = false;
            Message = message;
        }
        public NetworkingErrorException(string message, bool shouldRetry) : base(message)
        {
            ShouldRetry = shouldRetry;
            Message = message;
        }
    }
    // permanent error
    public class PermanentException : ApproovException
    {
        public PermanentException(string message) : base(message)
        {
            ShouldRetry = false;
            Message = message;
        }
        public PermanentException(string message, bool shouldRetry) : base(message)
        {
            ShouldRetry = shouldRetry;
            Message = message;
        }
    }
    // rejection error
    public class RejectionException : ApproovException
    {
        public string ARC;
        public string RejectionReasons;
        public RejectionException(string message, string arc, string rejectionReasons)
        {
            ShouldRetry = false;
            Message = message;
            ARC = arc;
            RejectionReasons = rejectionReasons;
        }
    }
}// namespace
using System;

namespace Approov {
    /*
    *   Approov SDK exceptions
    */
    public class ApproovException : Exception
    {
        public bool ShouldRetry;
        public ApproovException()
            : this("ApproovException: Unknown Error.")
        {
        }

        public ApproovException(string message) : base(message)
        {
            ShouldRetry = false;
        }

        public ApproovException(string message, bool shouldRetry) : base(message)
        {
            ShouldRetry = shouldRetry;
        }
    } // ApproovException class
    // initialization failure
    public class InitializationFailureException : ApproovException
    {
        public InitializationFailureException(string message)
            : base(message)
        {
        }
        public InitializationFailureException(string message, bool shouldRetry)
            : base(message, shouldRetry)
        {
        }
    }
    // configuration failure
    public class ConfigurationFailureException : ApproovException
    {
        public ConfigurationFailureException(string message)
            : base(message)
        {
        }
        public ConfigurationFailureException(string message, bool shouldRetry)
            : base(message, shouldRetry)
        {
        }
    }
    // pinning error
    public class PinningErrorException : ApproovException
    {
        public PinningErrorException(string message)
            : base(message)
        {
        }
        public PinningErrorException(string message, bool shouldRetry)
            : base(message, shouldRetry)
        {
        }
    }
    // networking error
    public class NetworkingErrorException : ApproovException
    {
        public NetworkingErrorException(string message)
            : base(message)
        {
        }
        public NetworkingErrorException(string message, bool shouldRetry)
            : base(message, shouldRetry)
        {
        }
    }
    // permanent error
    public class PermanentException : ApproovException
    {
        public PermanentException(string message)
            : base(message)
        {
        }
        public PermanentException(string message, bool shouldRetry)
            : base(message, shouldRetry)
        {
        }
    }
    // rejection error
    public class RejectionException : ApproovException
    {
        public string ARC;
        public string RejectionReasons;
        public RejectionException(string message, string arc, string rejectionReasons)
            : base(message)
        {
            ShouldRetry = false;
            ARC = arc;
            RejectionReasons = rejectionReasons;
        }
    }
}// namespace

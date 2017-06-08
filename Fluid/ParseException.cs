using System;

namespace Fluid
{
    public class ParseException : Exception
    {
        //
        // Summary:
        //     Initializes a new instance of the System.Exception class.
        public ParseException() : base() { }
        //
        // Summary:
        //     Initializes a new instance of the System.Exception class with a specified error
        //     message.
        //
        // Parameters:
        //   message:
        //     The message that describes the error.
        public ParseException(string message) : base(message) { }
        //
        // Summary:
        //     Initializes a new instance of the System.Exception class with a specified error
        //     message and a reference to the inner exception that is the cause of this exception.
        //
        // Parameters:
        //   message:
        //     The error message that explains the reason for the exception.
        //
        //   innerException:
        //     The exception that is the cause of the current exception, or a null reference
        //     (Nothing in Visual Basic) if no inner exception is specified.
        public ParseException(string message, Exception innerException) : base(message, innerException) { }
    }
}

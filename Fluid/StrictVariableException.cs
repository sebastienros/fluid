using System;
using System.Collections.Generic;

namespace Fluid
{
    /// <summary>
    /// Exception thrown when StrictVariables is enabled and undefined template variables are accessed.
    /// </summary>
    public class StrictVariableException : InvalidOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StrictVariableException"/> class.
        /// </summary>
        /// <param name="missingVariables">The list of missing variable paths.</param>
        public StrictVariableException(IReadOnlyList<string> missingVariables)
            : base()
        {
            MissingVariables = missingVariables ?? throw new ArgumentNullException(nameof(missingVariables));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StrictVariableException"/> class.
        /// </summary>
        /// <param name="missingVariables">The list of missing variable paths.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public StrictVariableException(IReadOnlyList<string> missingVariables, Exception innerException)
            : base(null, innerException)
        {
            MissingVariables = missingVariables ?? throw new ArgumentNullException(nameof(missingVariables));
        }

        /// <summary>
        /// Gets the collection of missing variable paths.
        /// </summary>
        public IReadOnlyList<string> MissingVariables { get; }

        /// <summary>
        /// Gets a message that describes the current exception.
        /// </summary>
        public override string Message =>
            $"The following variables were not found: {string.Join(", ", MissingVariables)}";
    }
}

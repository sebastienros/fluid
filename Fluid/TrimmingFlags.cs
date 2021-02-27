using System;

namespace Fluid
{
    [Flags]
    public enum TrimmingFlags
    {
        /// <summary>
        /// Default. Tags and outputs are not trimmed unless the '-' is set on the delimiter
        /// </summary>
        None,
        
        /// <summary>
        /// Strip blank characters (including , \t, and \r) from the left of tags ({% %}) until \n (exclusive).
        /// </summary>
        TagLeft,
        
        /// <summary>
        /// Strip blank characters (including , \t, and \r) from the right of tags ({% %}) until \n (inclusive).
        /// </summary>
        TagRight,
        
        /// <summary>
        /// Strip blank characters (including , \t, and \r) from the left of values ({{ }}) until \n (exclusive).
        /// </summary>
        OutputLeft,
        
        /// <summary>
        /// Strip blank characters (including , \t, and \r) from the right of values ({{ }}) until \n (inclusive).
        /// </summary>
        OutputRight
    }
}

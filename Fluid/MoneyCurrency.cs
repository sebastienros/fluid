namespace Fluid
{
    /// <summary>
    /// Describes a currency used by the money filters.
    /// </summary>
    public sealed class MoneyCurrency
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MoneyCurrency"/> class.
        /// </summary>
        /// <param name="code">The ISO 4217 code of the currency, e.g. <c>USD</c>.</param>
        /// <param name="symbol">The symbol of the currency, e.g. <c>$</c>. When <c>null</c> the code is used.</param>
        /// <param name="decimalDigits">The number of decimal digits the currency is expressed with.</param>
        public MoneyCurrency(string code, string symbol = null, int decimalDigits = 2)
        {
            if (code is null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(code));
            }

#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfNegative(decimalDigits);
#else
            if (decimalDigits < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(decimalDigits));
            }
#endif

            Code = code;
            Symbol = symbol ?? code;
            DecimalDigits = decimalDigits;
        }

        /// <summary>
        /// Gets the ISO 4217 code of the currency.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the symbol of the currency.
        /// </summary>
        public string Symbol { get; }

        /// <summary>
        /// Gets the number of decimal digits the currency is expressed with.
        /// </summary>
        public int DecimalDigits { get; }
    }
}

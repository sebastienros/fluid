using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    /// <summary>
    /// Provides information about a parent tablerow loop.
    /// </summary>
    public sealed class TableRowLoopValue : FluidValue
    {
        private int _length;
        private int _row;
        private int _col;
        private int _cols;
        private int _index;

        public TableRowLoopValue(int length, int cols)
        {
            _length = length;
            _row = 1;
            _col = 1;
            _cols = cols;
            _index = 0;
        }

        /// <summary>
        /// The total number of iterations in the loop.
        /// </summary>
        public int Length => _length;

        /// <summary>
        /// The 1-based index of the current column.
        /// </summary>
        public int Col => _col;

        /// <summary>
        /// The 0-based index of the current column.
        /// </summary>
        public int Col0 => _col - 1;

        /// <summary>
        /// The 1-based index of current row.
        /// </summary>
        public int Row => _row;

        /// <summary>
        /// The 1-based index of the current iteration.
        /// </summary>
        public int Index => _index + 1;

        /// <summary>
        /// The 0-based index of the current iteration.
        /// </summary>
        public int Index0 => _index;

        /// <summary>
        /// The 1-based index of the current iteration, in reverse order.
        /// </summary>
        public int RIndex => _length - _index;

        /// <summary>
        /// The 0-based index of the current iteration, in reverse order.
        /// </summary>
        public int RIndex0 => _length - _index - 1;

        /// <summary>
        /// Returns true if the current iteration is the first.
        /// </summary>
        public bool First => _index == 0;

        /// <summary>
        /// Returns true if the current iteration is the last.
        /// </summary>
        public bool Last => _index == _length - 1;

        /// <summary>
        /// Returns true if the current column is the first in the row.
        /// </summary>
        public bool ColFirst => _col == 1;

        /// <summary>
        /// Returns true if the current column is the last in the row.
        /// </summary>
        public bool ColLast => _col == _cols;

        internal void Increment()
        {
            _index++;

            if (_col == _cols)
            {
                _col = 1;
                _row++;
            }
            else
            {
                _col++;
            }
        }

        public override FluidValues Type => FluidValues.Dictionary;

        public override bool Equals(FluidValue other)
        {
            return false;
        }

        public override bool ToBooleanValue()
        {
            return false;
        }

        public override decimal ToNumberValue()
        {
            return _length;
        }

        public override object ToObjectValue()
        {
            return null;
        }

        public override string ToStringValue()
        {
            return "tablerowloop";
        }

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            return name switch
            {
                "length" => NumberValue.Create(Length),
                "col" => NumberValue.Create(Col),
                "col0" => NumberValue.Create(Col0),
                "row" => NumberValue.Create(Row),
                "index" => NumberValue.Create(Index),
                "index0" => NumberValue.Create(Index0),
                "rindex" => NumberValue.Create(RIndex),
                "rindex0" => NumberValue.Create(RIndex0),
                "first" => BooleanValue.Create(First),
                "last" => BooleanValue.Create(Last),
                "col_first" => BooleanValue.Create(ColFirst),
                "col_last" => BooleanValue.Create(ColLast),
                _ => NilValue.Instance,
            };
        }

        public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, CultureInfo cultureInfo)
        {
            return default;
        }
    }
}

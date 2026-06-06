using System.Globalization;

namespace tool_r1ng.Utilities;

public static class ExpressionEvaluator
{
    public static bool TryEvaluate(string expression, out double value)
    {
        value = 0;

        try
        {
            var parser = new Parser(expression);
            value = parser.Parse();
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
        catch
        {
            return false;
        }
    }

    private sealed class Parser
    {
        private readonly string _expression;
        private int _position;

        public Parser(string expression)
        {
            _expression = expression;
        }

        public double Parse()
        {
            var value = ParseExpression();
            SkipWhiteSpace();

            if (_position != _expression.Length)
            {
                throw new FormatException("Unexpected characters at the end of the expression.");
            }

            return value;
        }

        private double ParseExpression()
        {
            var value = ParseTerm();

            while (true)
            {
                SkipWhiteSpace();

                if (Match('+'))
                {
                    value += ParseTerm();
                }
                else if (Match('-'))
                {
                    value -= ParseTerm();
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseTerm()
        {
            var value = ParseFactor();

            while (true)
            {
                SkipWhiteSpace();

                if (Match('*'))
                {
                    value *= ParseFactor();
                }
                else if (Match('/'))
                {
                    value /= ParseFactor();
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseFactor()
        {
            SkipWhiteSpace();

            if (Match('+'))
            {
                return ParseFactor();
            }

            if (Match('-'))
            {
                return -ParseFactor();
            }

            if (Match('('))
            {
                var value = ParseExpression();
                if (!Match(')'))
                {
                    throw new FormatException("Missing closing parenthesis.");
                }

                return value;
            }

            return ParseNumber();
        }

        private double ParseNumber()
        {
            SkipWhiteSpace();
            var start = _position;

            while (_position < _expression.Length
                   && (char.IsDigit(_expression[_position]) || _expression[_position] == '.'))
            {
                _position++;
            }

            if (start == _position)
            {
                throw new FormatException("Number expected.");
            }

            var slice = _expression[start.._position];
            return double.Parse(slice, CultureInfo.InvariantCulture);
        }

        private bool Match(char expected)
        {
            SkipWhiteSpace();

            if (_position >= _expression.Length || _expression[_position] != expected)
            {
                return false;
            }

            _position++;
            return true;
        }

        private void SkipWhiteSpace()
        {
            while (_position < _expression.Length && char.IsWhiteSpace(_expression[_position]))
            {
                _position++;
            }
        }
    }
}

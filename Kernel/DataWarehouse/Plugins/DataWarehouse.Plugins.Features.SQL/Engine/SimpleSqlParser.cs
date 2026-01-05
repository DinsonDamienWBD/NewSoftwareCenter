using System.Text;

namespace DataWarehouse.Plugins.Features.SQL.Engine
{
    /// <summary>
    /// A robust Recursive Descent SQL Parser.
    /// Converts raw SQL strings into a sanitized AST (Abstract Syntax Tree) safe for execution.
    /// Replaces the legacy string-split logic.
    /// </summary>
    public class SimpleSqlParser
    {
        /// <summary>
        /// Represents a parsed SQL Query.
        /// </summary>
        public class ParsedQuery
        {
            public List<string> SelectFields { get; set; } = new();
            public string TableName { get; set; } = string.Empty;
            public List<WhereClause> Conditions { get; set; } = new();
            public int Limit { get; set; } = 100;
        }

        /// <summary>
        /// Represents a single WHERE condition.
        /// </summary>
        public class WhereClause
        {
            public string Field { get; set; } = string.Empty;
            public string Operator { get; set; } = "=";
            public string Value { get; set; } = string.Empty;
            public string Logic { get; set; } = "AND"; // AND/OR
        }

        private readonly string _sql;
        private int _pos;

        public SimpleSqlParser(string sql)
        {
            _sql = sql.Trim();
            _pos = 0;
        }

        /// <summary>
        /// Parses the SQL string into a ParsedQuery object.
        /// </summary>
        public ParsedQuery Parse()
        {
            var result = new ParsedQuery();

            // 1. SELECT
            Expect("SELECT");
            result.SelectFields = ParseColumns();

            // 2. FROM
            Expect("FROM");
            result.TableName = ParseIdentifier();

            // 3. WHERE (Optional)
            if (Match("WHERE"))
            {
                result.Conditions = ParseConditions();
            }

            // 4. LIMIT (Optional)
            if (Match("LIMIT"))
            {
                result.Limit = int.Parse(ParseToken());
            }

            return result;
        }

        private List<string> ParseColumns()
        {
            var cols = new List<string>();
            while (true)
            {
                cols.Add(ParseIdentifier());
                if (!Match(",")) break;
            }
            return cols;
        }

        private List<WhereClause> ParseConditions()
        {
            var conditions = new List<WhereClause>();
            string logic = "AND"; // Default for first

            while (true)
            {
                var clause = new WhereClause { Logic = logic };
                clause.Field = ParseIdentifier();
                clause.Operator = ParseOperator();
                clause.Value = ParseValue();
                conditions.Add(clause);

                if (Match("AND")) logic = "AND";
                else if (Match("OR")) logic = "OR";
                else break;
            }
            return conditions;
        }

        // --- Low Level Tokenizer ---

        private string ParseIdentifier()
        {
            SkipWhitespace();
            if (_pos >= _sql.Length) return "*"; // Default

            if (_sql[_pos] == '*')
            {
                _pos++;
                return "*";
            }

            // Handle quoted identifiers "Column"
            if (_sql[_pos] == '"')
            {
                _pos++;
                int start = _pos;
                while (_pos < _sql.Length && _sql[_pos] != '"') _pos++;
                string id = _sql.Substring(start, _pos - start);
                _pos++; // Skip closing quote
                return id;
            }

            // Handle standard text
            var sb = new StringBuilder();
            while (_pos < _sql.Length && (char.IsLetterOrDigit(_sql[_pos]) || _sql[_pos] == '_'))
            {
                sb.Append(_sql[_pos]);
                _pos++;
            }
            return sb.ToString();
        }

        private string ParseOperator()
        {
            SkipWhitespace();
            string[] ops = { ">=", "<=", "<>", "!=", "=", ">", "<", "LIKE", "ILIKE" };

            foreach (var op in ops)
            {
                if (IsNext(op))
                {
                    _pos += op.Length;
                    return op;
                }
            }
            throw new Exception($"Syntax Error: Expected operator at position {_pos}");
        }

        private string ParseValue()
        {
            SkipWhitespace();

            // Strings 'value'
            if (_sql[_pos] == '\'')
            {
                _pos++;
                int start = _pos;
                while (_pos < _sql.Length && _sql[_pos] != '\'') _pos++;
                string val = _sql.Substring(start, _pos - start);
                _pos++;
                return val;
            }

            // Numbers
            var sb = new StringBuilder();
            while (_pos < _sql.Length && (char.IsDigit(_sql[_pos]) || _sql[_pos] == '.'))
            {
                sb.Append(_sql[_pos]);
                _pos++;
            }
            return sb.ToString();
        }

        private void Expect(string token)
        {
            if (!Match(token)) throw new Exception($"Syntax Error: Expected '{token}' at position {_pos}");
        }

        private bool Match(string token)
        {
            SkipWhitespace();
            if (IsNext(token))
            {
                _pos += token.Length;
                return true;
            }
            return false;
        }

        private bool IsNext(string token)
        {
            if (_pos + token.Length > _sql.Length) return false;
            var sub = _sql.Substring(_pos, token.Length);
            return sub.Equals(token, StringComparison.OrdinalIgnoreCase);
        }

        private string ParseToken()
        {
            SkipWhitespace();
            int start = _pos;
            while (_pos < _sql.Length && !char.IsWhiteSpace(_sql[_pos])) _pos++;
            return _sql.Substring(start, _pos - start);
        }

        private void SkipWhitespace()
        {
            while (_pos < _sql.Length && char.IsWhiteSpace(_sql[_pos])) _pos++;
        }
    }
}
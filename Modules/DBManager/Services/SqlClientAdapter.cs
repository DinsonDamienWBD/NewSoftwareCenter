using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SoftwareCenter.Module.DBManager.Services
{
    /// <summary>
    /// Lightweight adapter to centralize usage of Microsoft.Data.SqlClient for DBManager.
    /// Replace direct SqlClient usage in DBManager with this adapter to simplify migration.
    /// </summary>
    public class SqlClientAdapter
    {
        private readonly string _connectionString;

        public SqlClientAdapter(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<int> ExecuteNonQueryAsync(string sql, params SqlParameter[] parameters)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            if (parameters != null && parameters.Length > 0) cmd.Parameters.AddRange(parameters);
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<object> ExecuteScalarAsync(string sql, params SqlParameter[] parameters)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            if (parameters != null && parameters.Length > 0) cmd.Parameters.AddRange(parameters);
            return await cmd.ExecuteScalarAsync();
        }

        public async Task FillDataTableAsync(DataTable table, string sql, params SqlParameter[] parameters)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            if (parameters != null && parameters.Length > 0) cmd.Parameters.AddRange(parameters);
            await using var reader = await cmd.ExecuteReaderAsync();
            table.Load(reader);
        }
    }
}

using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SoftwareCenter.Module.DBManager.Services
{
    /// <summary>
    /// Example DB service demonstrating usage of SqlClientAdapter.
    /// Replace legacy call-sites with this service where appropriate.
    /// </summary>
    public class ExampleDbService
    {
        private readonly SqlClientAdapter _adapter;

        public ExampleDbService(SqlClientAdapter adapter)
        {
            _adapter = adapter;
        }

        public async Task<int> InsertUserAsync(string username, string email)
        {
            var sql = "INSERT INTO Users (Username, Email) VALUES (@Username, @Email);";
            var parameters = new SqlParameter[]
            {
                new SqlParameter("@Username", SqlDbType.NVarChar) { Value = username },
                new SqlParameter("@Email", SqlDbType.NVarChar) { Value = email }
            };

            return await _adapter.ExecuteNonQueryAsync(sql, parameters);
        }

        public async Task<DataTable> GetUsersAsync()
        {
            var table = new DataTable();
            var sql = "SELECT Id, Username, Email FROM Users";
            await _adapter.FillDataTableAsync(table, sql);
            return table;
        }
    }
}

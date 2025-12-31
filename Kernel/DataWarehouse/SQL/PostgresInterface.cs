using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using DataWarehouse.Contracts;
using Microsoft.Extensions.Logging;

namespace DataWarehouse.SQL
{
    /// <summary>
    /// Postgres
    /// </summary>
    public partial class PostgresInterface : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Any, 5432);
        // [FIX CS1061] Depend on IQueryableIndex explicitly
        private readonly IQueryableIndex _index;
        private readonly ILogger _logger;
        private bool _running;
        private readonly CancellationTokenSource _cts = new();

        // Regex for SQL parsing
        private static readonly Regex SqlParser = MySQLParserRegex();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="index"></param>
        /// <param name="logger"></param>
        /// <exception cref="ArgumentException"></exception>
        public PostgresInterface(IMetadataIndex index, ILogger logger)
        {
            if (index is not IQueryableIndex qi)
                throw new ArgumentException("Index must support IQueryableIndex for SQL support.");
            _index = qi;
            _logger = logger;
        }

        /// <summary>
        /// Start
        /// </summary>
        public void Start()
        {
            _listener.Start();
            _running = true;
            _ = AcceptLoop();
            _logger.LogInformation("[SQL] PGWire Listener started on port 5432.");
        }

        private async Task AcceptLoop()
        {
            while (_running && !_cts.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    _ = HandleSession(client);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task HandleSession(TcpClient client)
        {
            using var stream = client.GetStream();
            using var writer = new PgPacketWriter(stream);
            try
            {
                // 1. Handshake (StartupMessage)
                // Read Length (Int32)
                var lenBytes = new byte[4];
                await stream.ReadExactlyAsync(lenBytes, 0, 4);

                // Read Protocol Version / Request Code
                var codeBytes = new byte[4];
                await stream.ReadExactlyAsync(codeBytes, 0, 4);
                int code = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(codeBytes));

                if (code == 80877103) // SSL Request
                {
                    await stream.WriteAsync("N"u8.ToArray()); // No SSL
                    // Client will restart with normal startup
                    await HandleSession(client);
                    return;
                }

                // Skip remaining startup params for this Kernel version
                int len = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenBytes));
                if (len > 8)
                {
                    var garbage = new byte[len - 8];
                    await stream.ReadExactlyAsync(garbage, 0, len - 8);
                }

                // Auth OK
                await writer.WritePacketAsync('R', w => w.WriteInt32(0));

                // KeyData (Mock PID/Secret)
                await writer.WritePacketAsync('K', w => { w.WriteInt32(1234); w.WriteInt32(5678); });

                // ReadyForQuery
                await writer.WriteReadyAsync();

                // 2. Query Loop
                var buffer = new byte[1];
                while (client.Connected)
                {
                    int read = await stream.ReadAsync(buffer);
                    if (read == 0) break;
                    char msgType = (char)buffer[0];

                    if (msgType == 'Q') // Simple Query
                    {
                        var lenHeader = new byte[4];
                        await stream.ReadExactlyAsync(lenHeader, 0, 4);
                        int bodyLen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenHeader)) - 4;

                        var sqlBytes = new byte[bodyLen];
                        await stream.ReadExactlyAsync(sqlBytes, 0, bodyLen);
                        string sql = Encoding.UTF8.GetString(sqlBytes).TrimEnd('\0');

                        // [FIX CA1873] Structured Logging
                        _logger.LogInformation("[SQL] Executing: {Sql}", sql);

                        await ProcessQuery(sql, writer);
                    }
                    else if (msgType == 'X') break; // Terminate
                }
            }
            catch (Exception ex)
            {
                // [FIX CA1873] Structured Logging
                _logger.LogError(ex, "[SQL] Session Error");
            }
        }

        private async Task ProcessQuery(string sql, PgPacketWriter writer)
        {
            var match = SqlParser.Match(sql);
            if (!match.Success)
            {
                await writer.WriteErrorAsync("Syntax Error", "Could not parse SQL");
                await writer.WriteReadyAsync();
                return;
            }

            var query = new CompositeQuery();
            if (match.Groups["field"].Success)
            {
                string val = match.Groups["val"].Value.Trim('\'');
                // [FIX CA1862] Use OrdinalIgnoreCase
                string opRaw = match.Groups["op"].Value;
                string op = opRaw.Equals("LIKE", StringComparison.OrdinalIgnoreCase) ? "CONTAINS" : opRaw;

                query.Filters.Add(new QueryFilter { Field = match.Groups["field"].Value, Operator = op, Value = val });
            }

            var ids = await _index.ExecuteQueryAsync(query, 50);

            // T: Row Description
            await writer.WritePacketAsync('T', w =>
            {
                w.WriteInt16(3); // 3 Columns now
                w.WriteColumnDef("Id", 25);      // TEXT
                w.WriteColumnDef("Summary", 25); // TEXT
                w.WriteColumnDef("Tier", 25);    // TEXT
            });

            // D: Data Rows
            foreach (var id in ids)
            {
                // RETRIEVAL UPGRADE: Fetch Real Data
                var manifest = await _index.GetManifestAsync(id);

                if (manifest != null)
                {
                    await writer.WritePacketAsync('D', w =>
                    {
                        w.WriteInt16(3);

                        // 1. ID
                        w.WriteField(manifest.Id);

                        // 2. Summary (Real Content)
                        // If null, we indicate it's raw data
                        var summary = !string.IsNullOrEmpty(manifest.ContentSummary)
                                      ? manifest.ContentSummary
                                      : "[Binary Content]";
                        w.WriteField(summary);

                        // 3. Tier (V3 Metadata)
                        // If we implemented the field in Manifest, use it. Otherwise placeholder.
                        // Assuming Manifest has 'CurrentTier' string from previous steps.
                        w.WriteField(manifest.CurrentTier ?? "Hot");
                    });
                }
            }

            // C: Command Complete
            await writer.WritePacketAsync('C', w => w.WriteCString($"SELECT {ids.Length}"));
            await writer.WriteReadyAsync();
        }

        /// <summary>
        /// Safely Dispose
        /// </summary>
        public void Dispose()
        {
            _running = false;
            _cts.Cancel();
            _listener.Stop();
            // [FIX CA1816]
            GC.SuppressFinalize(this);
        }

        [GeneratedRegex(@"SELECT\s+(?<cols>.*?)\s+FROM\s+(?<table>\w+)(\s+WHERE\s+(?<field>\w+)\s+(?<op>=|!=|LIKE|>|<)\s+(?<val>'.*?'|\d+))?", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
        private static partial Regex MySQLParserRegex();
    }

    /// <summary>
    /// Package writer
    /// </summary>
    /// <param name="stream"></param>
    public class PgPacketWriter(Stream stream) : IDisposable
    {
        private readonly MemoryStream _buffer = new();
        private readonly Stream _dest = stream;

        /// <summary>
        /// Write Int32
        /// </summary>
        /// <param name="value"></param>
        public void WriteInt32(int value) => _buffer.Write(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value)));

        /// <summary>
        /// Write Int16
        /// </summary>
        /// <param name="value"></param>
        public void WriteInt16(short value) => _buffer.Write(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value)));

        /// <summary>
        /// Write CString
        /// </summary>
        /// <param name="value"></param>
        public void WriteCString(string value) { var b = Encoding.UTF8.GetBytes(value); _buffer.Write(b); _buffer.WriteByte(0); }

        /// <summary>
        /// Write field
        /// </summary>
        /// <param name="val"></param>
        public void WriteField(string? val)
        {
            if (val == null) { WriteInt32(-1); return; }
            var b = Encoding.UTF8.GetBytes(val);
            WriteInt32(b.Length);
            _buffer.Write(b);
        }

        /// <summary>
        /// Write column definition
        /// </summary>
        /// <param name="name"></param>
        /// <param name="typeOid"></param>
        public void WriteColumnDef(string name, int typeOid)
        {
            WriteCString(name);
            WriteInt32(0); WriteInt16(0); WriteInt32(typeOid); WriteInt16(0); WriteInt32(-1); WriteInt16(0);
        }

        /// <summary>
        /// Write packet
        /// </summary>
        /// <param name="type"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        public async Task WritePacketAsync(char type, Action<PgPacketWriter> body)
        {
            _buffer.SetLength(0);
            WriteInt32(0); // Reserve Length
            body(this);

            var bytes = _buffer.ToArray();
            var len = bytes.Length;
            var lenBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(len));
            Buffer.BlockCopy(lenBytes, 0, bytes, 0, 4);

            await _dest.WriteAsync(new[] { (byte)type });
            await _dest.WriteAsync(bytes);
        }

        /// <summary>
        /// Wite ready
        /// </summary>
        /// <returns></returns>
        public async Task WriteReadyAsync() => await WritePacketAsync('Z', w => w.WriteByte((byte)'I'));

        /// <summary>
        /// Write error
        /// </summary>
        /// <param name="code"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public async Task WriteErrorAsync(string code, string msg) => await WritePacketAsync('E', w => { w.WriteByte((byte)'S'); w.WriteCString("ERROR"); w.WriteByte((byte)'M'); w.WriteCString(msg); w.WriteByte(0); });

        /// <summary>
        /// Write byte
        /// </summary>
        /// <param name="b"></param>
        public void WriteByte(byte b) => _buffer.WriteByte(b);

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose() { _buffer.Dispose(); GC.SuppressFinalize(this); }
    }
}
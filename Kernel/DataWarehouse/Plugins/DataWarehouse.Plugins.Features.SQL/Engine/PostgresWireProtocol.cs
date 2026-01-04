using DataWarehouse.Plugins.Features.SQL.Services;
using DataWarehouse.SDK.Contracts;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DataWarehouse.Plugins.Features.SQL.Engine
{
    /// <summary>
    /// Postgres wire protocol
    /// </summary>
    public class PostgresWireProtocol(PostgresInterface pgInterface, IKernelContext context) : IDisposable
    {
        private readonly PostgresInterface _pgInterface = pgInterface;
        private readonly IKernelContext _context = context;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;

        // Constants
        private const int SSL_REQUEST_CODE = 80877103;
        private const int PROTOCOL_VERSION_3 = 196608; // 3.0

        /// <summary>
        /// Start
        /// </summary>
        /// <param name="port"></param>
        public void Start(int port = 5432)
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _context.LogInfo($"[PGWire] Listening on port {port}");

            _ = Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                        _ = ProcessClientAsync(client, _cts.Token);
                    }
                    catch { break; }
                }
            }, _cts.Token);
        }

        /// <summary>
        /// Process client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task ProcessClientAsync(TcpClient client, CancellationToken ct)
        {
            using var stream = client.GetStream();
            var writer = new PgPacketWriter(stream);

            try
            {
                // --- STEP 1: HANDSHAKE & STARTUP ---
                await HandleStartupAsync(stream, writer, ct);

                // --- STEP 2: QUERY LOOP ---
                var headerBuffer = new byte[5]; // Type (1) + Length (4)

                while (!ct.IsCancellationRequested)
                {
                    // Read Message Type
                    int bytesRead = await stream.ReadAsync(headerBuffer.AsMemory(0, 1), ct);
                    if (bytesRead == 0) break; // Client disconnected
                    char msgType = (char)headerBuffer[0];

                    // Read Message Length
                    await stream.ReadExactlyAsync(headerBuffer.AsMemory(1, 4), ct);
                    int length = BinaryPrimitives.ReadInt32BigEndian(headerBuffer.AsSpan(1)) - 4;

                    // Read Body
                    byte[] body = [];
                    if (length > 0)
                    {
                        body = new byte[length];
                        await ReadExactAsync(stream, body, length, ct);
                    }

                    string queryText = Encoding.UTF8.GetString(body).TrimEnd('\0');

                    switch (msgType)
                    {
                        case 'Q': // Simple Query
                            await ProcessQueryAsync(queryText, writer);
                            break;

                        case 'X': // Terminate
                            return;

                        default:
                            // For MVP, we ignore Sync (S), Flush (H), Describe (D) etc.
                            // But we must stay ready.
                            if (msgType != 'P') // Avoid log spam for Parse
                                await writer.WriteReadyAsync();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _context.LogError($"[PGWire] Error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Handles the official PostgreSQL 3.0 Startup Flow.
        /// Supports SSL Request (Reject) -> StartupMessage -> Auth -> BackendKey -> Ready.
        /// </summary>
        private static async Task HandleStartupAsync(NetworkStream stream, PgPacketWriter writer, CancellationToken ct)
        {
            var intBuffer = new byte[4];

            // 1. Read Length of first message
            await ReadExactAsync(stream, intBuffer, 4, ct);
            int length = BinaryPrimitives.ReadInt32BigEndian(intBuffer);

            // 2. Read Protocol/Request Code
            await ReadExactAsync(stream, intBuffer, 4, ct);
            int code = BinaryPrimitives.ReadInt32BigEndian(intBuffer);

            // 3. Check for SSL Request (Code: 80877103)
            if (code == SSL_REQUEST_CODE)
            {
                // Send 'N' (No SSL)
                await stream.WriteAsync("N"u8.ToArray(), ct);

                // Client will now send the REAL Startup Message immediately.
                // Read Length again.
                await ReadExactAsync(stream, intBuffer, 4, ct);
                length = BinaryPrimitives.ReadInt32BigEndian(intBuffer);

                // Read Protocol Version again.
                await ReadExactAsync(stream, intBuffer, 4, ct);
                code = BinaryPrimitives.ReadInt32BigEndian(intBuffer);
            }

            // 4. Check Protocol Version (3.0)
            if (code != PROTOCOL_VERSION_3)
            {
                await writer.WriteErrorAsync("FATAL", $"Unsupported Protocol Version: {code}");
                throw new InvalidOperationException("Unsupported PG Protocol");
            }

            // 5. Read Parameters (User, Database, etc.)
            // The remaining (length - 8) bytes are key/value pairs separated by nulls.
            int paramLen = length - 8;
            if (paramLen > 0)
            {
                var paramBytes = new byte[paramLen];
                await ReadExactAsync(stream, paramBytes, paramLen, ct);
                // We currently ignore the user/db values, acting as a promiscuous server.
                // In a real impl, we would parse these to check credentials.
            }

            // 6. Send AuthenticationOK (Type: 'R', Body: 0)
            await writer.WritePacketAsync('R', w => w.WriteInt32(0));

            // 7. Send ParameterStatus (Optional but polite)
            await SendParameterStatus(writer, "server_version", "14.0");
            await SendParameterStatus(writer, "client_encoding", "UTF8");
            await SendParameterStatus(writer, "DateStyle", "ISO");

            // 8. Send BackendKeyData (ProcessID + Secret Key)
            // Used for CANCEL commands. Random values for now.
            await writer.WritePacketAsync('K', w =>
            {
                w.WriteInt32(1234); // Process ID
                w.WriteInt32(5678); // Secret Key
            });

            // 9. Send ReadyForQuery (Type: 'Z', Body: 'I' for Idle)
            await writer.WriteReadyAsync();
        }

        private static async Task SendParameterStatus(PgPacketWriter writer, string key, string value)
        {
            await writer.WritePacketAsync('S', w =>
            {
                w.WriteCString(key);
                w.WriteCString(value);
            });
        }

        private async Task ProcessQueryAsync(string sql, PgPacketWriter writer)
        {
            // Execute
            var manifests = await _pgInterface.ExecuteQueryAsync(sql);
            var resultList = manifests.ToList();

            // Row Description (Header)
            await writer.WritePacketAsync('T', w =>
            {
                w.WriteInt16(3); // 3 Columns
                w.WriteColumnDef("id", 25);      // TEXT
                w.WriteColumnDef("summary", 25); // TEXT
                w.WriteColumnDef("tier", 25);    // TEXT
            });

            // Data Rows
            foreach (var m in resultList)
            {
                await writer.WritePacketAsync('D', w =>
                {
                    w.WriteInt16(3); // 3 Columns
                    w.WriteField(m.Id);
                    w.WriteField(!string.IsNullOrEmpty(m.ContentSummary) ? m.ContentSummary : "[Binary]");
                    w.WriteField(m.CurrentTier ?? "Warm");
                });
            }

            // Command Complete
            await writer.WritePacketAsync('C', w => w.WriteCString($"SELECT {resultList.Count}"));
            await writer.WriteReadyAsync();
        }

        private static async Task ReadExactAsync(Stream s, byte[] buffer, int count, CancellationToken ct)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await s.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), ct);
                if (read == 0) throw new EndOfStreamException();
                totalRead += read;
            }
        }

        //Safely dispose
        public void Dispose()
        {
            _cts?.Cancel();
            _listener?.Stop();
            GC.SuppressFinalize(this);
        }
    }
}
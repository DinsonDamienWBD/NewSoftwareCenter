using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace DataWarehouse.Plugins.Features.SQL.Engine
{
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

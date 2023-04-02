using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.ServerPing;

namespace ProjBobcat.DefaultComponent.Service;

public class ServerPingService : ProgressReportBase
{
    List<byte> _buffer;
    int _offset;
    NetworkStream _stream;

    public string Address { get; init; }
    public ushort Port { get; init; }
    public int VersionId { get; init; }

    public override string ToString()
    {
        return $"{Address}:{Port}";
    }

    public ServerPingResult Run()
    {
        return RunAsync().Result;
    }

    public async Task<ServerPingResult?> RunAsync()
    {
        using var client = new TcpClient
        {
            SendTimeout = 5000,
            ReceiveTimeout = 5000
        };
        var sw = new Stopwatch();
        var timeOut = TimeSpan.FromSeconds(3);
        using var cts = new CancellationTokenSource(timeOut);

        sw.Start();
        cts.CancelAfter(timeOut);

        try
        {
            await client.ConnectAsync(Address, Port, cts.Token);
        }
        catch (TaskCanceledException)
        {
            throw new OperationCanceledException($"服务器 {this} 连接失败，连接超时 ({timeOut.Seconds}s)。", cts.Token);
        }

        sw.Stop();

        InvokeStatusChangedEvent("正在连接到服务器...", 10);

        if (!client.Connected)
        {
            InvokeStatusChangedEvent("无法连接到服务器", 10);
            return null;
        }

        _buffer = new List<byte>();
        _stream = client.GetStream();

        InvokeStatusChangedEvent("发送请求...", 30);

        /*
         * Send a "Handshake" packet
         * http://wiki.vg/Server_List_Ping#Ping_Process
         */
        WriteVarInt(VersionId == 0 ? 47 : VersionId);
        WriteString(Address);
        WriteShort(Port);
        WriteVarInt(1);
        await Flush(0);

        /*
         * Send a "Status Request" packet
         * http://wiki.vg/Server_List_Ping#Ping_Process
         */
        await Flush(0);

        /*
         * If you are using a modded server then use a larger buffer to account, 
         * see link for explanation and a motd to HTML snippet
         * https://gist.github.com/csh/2480d14fbbb33b4bbae3#gistcomment-2672658
         */
        var batch = new byte[1024];
        await using var ms = new MemoryStream();
        var remaining = 0;
        var flag = false;

        var latency = sw.ElapsedMilliseconds;

        do
        {
            var readLength = await _stream.ReadAsync(batch.AsMemory());
            await ms.WriteAsync(batch.AsMemory(0, readLength), cts.Token);
            if (!flag)
            {
                var packetLength = ReadVarInt(ms.ToArray());
                remaining = packetLength - _offset;
                flag = true;
            }

            if (readLength == 0 && remaining != 0)
                continue;

            remaining -= readLength;
        } while (remaining > 0);

        var buffer = ms.ToArray();
        _offset = 0;
        var length = ReadVarInt(buffer);
        var packet = ReadVarInt(buffer);
        var jsonLength = ReadVarInt(buffer);

        InvokeStatusChangedEvent($"收到包 0x{packet:X2} ， 长度为 {length}", 80);

        var json = ReadString(buffer, jsonLength);
        var ping = JsonSerializer.Deserialize(json, PingPayloadContext.Default.PingPayload);

        if (ping == null)
            return null;

        return new ServerPingResult
        {
            Latency = latency,
            Response = ping
        };
    }

    #region Read/Write methods

    byte ReadByte(IReadOnlyList<byte> buffer)
    {
        var b = buffer[_offset];
        _offset += 1;
        return b;
    }

    byte[] Read(byte[] buffer, int length)
    {
        var data = new byte[length];
        Array.Copy(buffer, _offset, data, 0, length);
        _offset += length;
        return data;
    }

    int ReadVarInt(IReadOnlyList<byte> buffer)
    {
        var value = 0;
        var size = 0;
        int b;
        while (((b = ReadByte(buffer)) & 0x80) == 0x80)
        {
            value |= (b & 0x7F) << (size++ * 7);
            if (size > 5) throw new IOException("This VarInt is an imposter!");
        }

        return value | ((b & 0x7F) << (size * 7));
    }

    string ReadString(byte[] buffer, int length)
    {
        var data = Read(buffer, length);
        return Encoding.UTF8.GetString(data);
    }

    void WriteVarInt(int value)
    {
        while ((value & 128) != 0)
        {
            _buffer.Add((byte)((value & 127) | 128));
            value = (int)(uint)value >> 7;
        }

        _buffer.Add((byte)value);
    }

    void WriteShort(ushort value)
    {
        _buffer.AddRange(BitConverter.GetBytes(value));
    }

    void WriteString(string data)
    {
        var buffer = Encoding.UTF8.GetBytes(data);
        WriteVarInt(buffer.Length);
        _buffer.AddRange(buffer);
    }

    async Task Flush(int id = -1)
    {
        var buffer = _buffer.ToArray();
        _buffer.Clear();

        var add = 0;
        var packetData = new[] { (byte)0x00 };
        if (id >= 0)
        {
            WriteVarInt(id);
            packetData = _buffer.ToArray();
            add = packetData.Length;
            _buffer.Clear();
        }

        WriteVarInt(buffer.Length + add);
        var bufferLength = _buffer.ToArray();
        _buffer.Clear();

        await _stream.WriteAsync(bufferLength.AsMemory());
        await _stream.WriteAsync(packetData.AsMemory());
        await _stream.WriteAsync(buffer.AsMemory());
    }

    #endregion
}
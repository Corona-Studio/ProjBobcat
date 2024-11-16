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
    List<byte> _buffer = null!;
    int _offset;
    NetworkStream _stream = null!;

    public required string Address { get; init; }
    public required ushort Port { get; init; }
    public int VersionId { get; init; }

    public override string ToString()
    {
        return $"{this.Address}:{this.Port}";
    }

    public ServerPingResult? Run()
    {
        return this.RunAsync().Result;
    }

    public async Task<ServerPingResult?> RunAsync()
    {
        using var client = new TcpClient
        {
            SendTimeout = 5000,
            ReceiveTimeout = 5000
        };

        var timestamp = Stopwatch.GetTimestamp();
        var timeOut = TimeSpan.FromSeconds(3);
        using var cts = new CancellationTokenSource(timeOut);

        cts.CancelAfter(timeOut);

        try
        {
            await client.ConnectAsync(this.Address, this.Port, cts.Token);
        }
        catch (TaskCanceledException)
        {
            throw new OperationCanceledException($"服务器 {this} 连接失败，连接超时 ({timeOut.Seconds}s)。", cts.Token);
        }

        this.InvokeStatusChangedEvent("正在连接到服务器...", 10);

        if (!client.Connected)
        {
            this.InvokeStatusChangedEvent("无法连接到服务器", 10);
            return null;
        }

        this._buffer = [];
        this._stream = client.GetStream();

        this.InvokeStatusChangedEvent("发送请求...", 30);

        /*
         * Send a "Handshake" packet
         * http://wiki.vg/Server_List_Ping#Ping_Process
         */
        this.WriteVarInt(this.VersionId == 0 ? 47 : this.VersionId);
        this.WriteString(this.Address);
        this.WriteShort(this.Port);
        this.WriteVarInt(1);
        await this.Flush(0);

        /*
         * Send a "Status Request" packet
         * http://wiki.vg/Server_List_Ping#Ping_Process
         */
        await this.Flush(0);

        /*
         * If you are using a modded server then use a larger buffer to account,
         * see link for explanation and a motd to HTML snippet
         * https://gist.github.com/csh/2480d14fbbb33b4bbae3#gistcomment-2672658
         */
        var batch = new byte[1024];
        await using var ms = new MemoryStream();
        var remaining = 0;
        var flag = false;

        var latency = (long)Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

        do
        {
            var readLength = await this._stream.ReadAsync(batch.AsMemory(), cts.Token);
            await ms.WriteAsync(batch.AsMemory(0, readLength), cts.Token);
            if (!flag)
            {
                var packetLength = this.ReadVarInt(ms.ToArray());
                remaining = packetLength - this._offset;
                flag = true;
            }

            if (readLength == 0 && remaining != 0)
                continue;

            remaining -= readLength;
        } while (remaining > 0);

        var buffer = ms.ToArray();
        this._offset = 0;
        var length = this.ReadVarInt(buffer);
        var packet = this.ReadVarInt(buffer);
        var jsonLength = this.ReadVarInt(buffer);

        this.InvokeStatusChangedEvent($"收到包 0x{packet:X2} ， 长度为 {length}", 80);

        var json = this.ReadString(buffer, jsonLength);
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
        var b = buffer[this._offset];
        this._offset += 1;
        return b;
    }

    byte[] Read(byte[] buffer, int length)
    {
        var data = new byte[length];
        Array.Copy(buffer, this._offset, data, 0, length);
        this._offset += length;
        return data;
    }

    int ReadVarInt(IReadOnlyList<byte> buffer)
    {
        var value = 0;
        var size = 0;
        int b;
        while (((b = this.ReadByte(buffer)) & 0x80) == 0x80)
        {
            value |= (b & 0x7F) << (size++ * 7);
            if (size > 5) throw new IOException("This VarInt is an imposter!");
        }

        return value | ((b & 0x7F) << (size * 7));
    }

    string ReadString(byte[] buffer, int length)
    {
        var data = this.Read(buffer, length);
        return Encoding.UTF8.GetString(data);
    }

    void WriteVarInt(int value)
    {
        while ((value & 128) != 0)
        {
            this._buffer.Add((byte)((value & 127) | 128));
            value = (int)(uint)value >> 7;
        }

        this._buffer.Add((byte)value);
    }

    void WriteShort(ushort value)
    {
        this._buffer.AddRange(BitConverter.GetBytes(value));
    }

    void WriteString(string data)
    {
        var buffer = Encoding.UTF8.GetBytes(data);
        this.WriteVarInt(buffer.Length);
        this._buffer.AddRange(buffer);
    }

    async Task Flush(int id = -1)
    {
        var buffer = this._buffer.ToArray();
        this._buffer.Clear();

        var add = 0;
        var packetData = new[] { (byte)0x00 };
        if (id >= 0)
        {
            this.WriteVarInt(id);
            packetData = [.. this._buffer];
            add = packetData.Length;
            this._buffer.Clear();
        }

        this.WriteVarInt(buffer.Length + add);
        var bufferLength = this._buffer.ToArray();
        this._buffer.Clear();

        await this._stream.WriteAsync(bufferLength.AsMemory());
        await this._stream.WriteAsync(packetData.AsMemory());
        await this._stream.WriteAsync(buffer.AsMemory());
    }

    #endregion
}
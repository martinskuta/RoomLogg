using System.Buffers;
using HidSharp;

namespace RoomLogg.Device;

public class RoomLoggManager() : HidStreamManager(VendorId, ProductId)
{
    private const int VendorId = 0x0483; // VendorId for RoomLogg / Ambient WS-3000 / Fine Offset HP-3000
    private const int ProductId = 0x5750; // ProductId for RoomLogg / Ambient WS-3000 / Fine Offset HP-3000

    private const int ChannelCount = 8;
    private const int HidReportSize = 64;
    private const int QueryTimeoutMilliseconds = 2000;

    private const byte FrameStart = 0x7B; // '{'
    private const byte FrameEnd1 = 0x40; // '@'
    private const byte FrameEnd2 = 0x7D; // '}'

    public SensorReading[] ReadTemperatures()
    {
        return ExecuteOnStream(ReadTemperatures, []);
    }

    private static SensorReading[] ReadTemperatures(HidStream stream)
    {
        var frameBuffer = ArrayPool<byte>.Shared.Rent(HidReportSize);
        try
        {
            var frame = SendCommandAndReceiveFrame(stream, CmdReadSensorValues, frameBuffer);
            return frame.Length <= 0 ? [] : ParseSensorValues(frame);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(frameBuffer);
        }
    }

    private static SensorReading[] ParseSensorValues(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 27)
        {
            Console.Error.WriteLine("Invalid or unrecognized RoomLogg sensor frame.");
            return [];
        }

        var results = new SensorReading[ChannelCount];

        for (var channel = 1; channel <= ChannelCount; channel++)
        {
            var offset = 1 + (channel - 1) * 3;
            var tempMsb = frame[offset];
            var tempLsb = frame[offset + 1];
            var humidityByte = frame[offset + 2];

            var isTempDisconnected = (tempMsb == 0x7F && tempLsb == 0xFF) || (tempMsb == 0xFF && tempLsb == 0xFF);
            var isHumidityDisconnected = humidityByte == 0xFF;

            if (isTempDisconnected || isHumidityDisconnected)
            {
                results[channel - 1] = SensorReading.Disconnected(channel);
                continue;
            }

            var rawTemp = (short)((tempMsb << 8) | tempLsb);
            var temp = Temperature.FromDegreesCelsius(rawTemp / 10.0);
            var humidity = RelativeHumidity.FromPercent(humidityByte);

            if (temp.DegreesCelsius is < -40 or > 60 || humidity.Percent is < 0 or > 100)
            {
                results[channel - 1] = SensorReading.Disconnected(channel);
                continue;
            }

            var dewPoint = WeatherHelper.CalculateDewPoint(temp, humidity);
            var heatIndex = WeatherHelper.CalculateHeatIndex(temp, humidity);

            results[channel - 1] = SensorReading.Connected(channel, temp, humidity, dewPoint, heatIndex);
        }

        return results;
    }

    public bool SyncTime()
    {
        return ExecuteOnStream(static stream => SyncTime(stream), false);
    }

    private static bool SyncTime(HidStream stream)
    {
        SendSyncTimeCommand(stream);
        return true;
    }

    private static void SendSyncTimeCommand(HidStream stream)
    {
        var dateTime = DateTime.Now;
        var year = (ushort)dateTime.Year;
        var commandFrame = ArrayPool<byte>.Shared.Rent(13);
        try
        {
            commandFrame[0] = 0x00;
            commandFrame[1] = FrameStart;
            commandFrame[2] = CmdSyncTime;
            commandFrame[3] = (byte)(year >> 8);
            commandFrame[4] = (byte)(year & 0xFF);
            commandFrame[5] = (byte)dateTime.Month;
            commandFrame[6] = (byte)dateTime.Day;
            commandFrame[7] = (byte)dateTime.Hour;
            commandFrame[8] = (byte)dateTime.Minute;
            commandFrame[9] = (byte)dateTime.Second;
            commandFrame[10] = (byte)dateTime.DayOfWeek;
            commandFrame[11] = FrameEnd1;
            commandFrame[12] = FrameEnd2;

            stream.Write(commandFrame, 0, 13);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(commandFrame);
        }
    }


    private static bool TryExtractFrame(ReadOnlySpan<byte> rawBuffer, out ReadOnlySpan<byte> frame)
    {
        frame = default;
        if (rawBuffer.Length < 3)
            return false;

        var startIndex = rawBuffer[0] == FrameStart ? 0 : rawBuffer.Length > 1 && rawBuffer[1] == FrameStart ? 1 : -1;
        if (startIndex < 0)
            return false;

        for (var i = startIndex + 1; i < rawBuffer.Length - 1; i++)
            if (rawBuffer[i] == FrameEnd1 && rawBuffer[i + 1] == FrameEnd2)
            {
                frame = rawBuffer.Slice(startIndex, i - startIndex + 2);
                return true;
            }

        return false;
    }

    private static void SendCommand(HidStream stream, byte command)
    {
        var commandFrame = ArrayPool<byte>.Shared.Rent(5);
        try
        {
            commandFrame[0] = 0x00;
            commandFrame[1] = FrameStart;
            commandFrame[2] = command;
            commandFrame[3] = FrameEnd1;
            commandFrame[4] = FrameEnd2;
            stream.Write(commandFrame, 0, 5);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(commandFrame);
        }
    }

    private static ReadOnlySpan<byte> SendCommandAndReceiveFrame(HidStream stream, byte command, byte[] frameBuffer)
    {
        stream.ReadTimeout = QueryTimeoutMilliseconds;
        SendCommand(stream, command);

        try
        {
            var bytesRead = stream.Read(frameBuffer, 0, frameBuffer.Length);
            return bytesRead > 0 && TryExtractFrame(frameBuffer.AsSpan(0, bytesRead), out var frame)
                ? frame
                : ReadOnlySpan<byte>.Empty;
        }
        catch (TimeoutException)
        {
            return ReadOnlySpan<byte>.Empty;
        }
    }

    // Read / Query command IDs (0x7B <cmd> 0x40 0x7D)
    // ReSharper disable UnusedMember.Local
    private const byte CmdListHistoryFiles = 0x01;
    private const byte CmdReadHistoryFileChunk = 0x02;
    private const byte CmdReadSensorValues = 0x03;
    private const byte CmdReadConfiguration = 0x04;
    private const byte CmdReadCalibration = 0x05;
    private const byte CmdReadAlarmStatus = 0x06;
    private const byte CmdReadTemperatureAlarms = 0x08;
    private const byte CmdReadHumidityAlarms = 0x09;
    private const byte CmdReadLanguage = 0x0A;
    private const byte CmdReadArchiveInterval = 0x41;
    private const byte CmdReadMainScreenState = 0x51;
    private const byte CmdReadModelInfo = 0xFE;

    // Write / Action command IDs
    private const byte CmdWriteLanguage = 0x0B;
    private const byte CmdWriteConfiguration = 0x10;
    private const byte CmdWriteCalibration = 0x11;
    private const byte CmdWriteAlarmStatus = 0x12;
    private const byte CmdWriteAlarmChannelFlags = 0x13;
    private const byte CmdWriteTemperatureAlarms = 0x14;
    private const byte CmdWriteHumidityAlarms = 0x15;
    private const byte CmdDeleteHistoryFile = 0x20;
    private const byte CmdCommitChanges = 0x2F;
    private const byte CmdSyncTime = 0x30;

    private const byte CmdWriteArchiveInterval = 0x40;
    // ReSharper restore UnusedMember.Local
}
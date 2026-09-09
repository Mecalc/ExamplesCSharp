// -------------------------------------------------------------------------
// Copyright (c) MECALC Technologies. All rights reserved.
// -------------------------------------------------------------------------

using QClient.RestfulClient;
using QProtocol;
using QProtocol.DataStreaming.DataPackets;
using QProtocol.DataStreaming.Headers;
using QProtocol.GenericDefines;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using static System.FormattableString;


const string outputFileName = "#output_log.txt"; // Used to log the console output text.
const string gpggaFileName = "#gpgga_log.txt";
const string driftFileName = "#drift_log.txt";

Console.WriteLine("QClient Advanced - Save GPS Data to File");
Console.WriteLine("This example will demonstrate how to GPS Channel Data to a file.");
Console.WriteLine("Module and Channel setup should be made via QAcquire. Ensure a channel is enabled.");
Console.WriteLine(string.Empty);

var ipAddress = args[0];
var httpConnection = new RestfulInterface($"http://{ipAddress}:8080");
var streamingSetup = httpConnection.Get<DataStreamSetup>(EndPoints.DataStreamSetup);

// At this point a connection could be made, hence timestamp the files.
File.AppendAllText(gpggaFileName, $"{DateTime.Now:G} - New test started{Environment.NewLine}");
long intialOffset = long.MinValue;

using var tcpClient = new TcpClient(ipAddress, streamingSetup.TCPPort);
using var networkStreamer = tcpClient.GetStream();
using var binaryReader = new BinaryReader(networkStreamer);

// At this point QServer will start to package data and send it over your port.
// A Loop for a longer runtime
var stopwatch = Stopwatch.StartNew();
Console.WriteLine("Connected and Streaming Data:");
Console.WriteLine(string.Empty);
while (true)
{
    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.C)
    {
        break;
    }

    // The first step is to read the packet header, it consists of 32 bytes.
    // QProtocol has classes defined for all the Data Headers, packets and types.
    var packetHeader = new PacketHeader(binaryReader);

    // Multiple payload types exists, hence you'll need to take care that you can support them all or discard the data when not.
    // With this example only Payload type 0 will be shown, hence discard the data of others.
    if (packetHeader.PayloadType != 0)
    {
        binaryReader.ReadBytes((int)packetHeader.PayloadSize);
        continue;
    }

    // Following the Packet Header is the Payload.
    // The Payload contains all the data for a given time period.
    // It starts with a Generic Channel Header, followed by a Specific Channel Header (if needed), followed by the Data for the specific Channel.
    // This will repeat for all the channels in the Payload. Continue to process the payload for the give size.
    var bytesLeft = packetHeader.PayloadSize;
    if (bytesLeft == 0)
    {
        continue;
    }

    ulong firstAnalogTimestamp = 0;
    var ggaMessage = string.Empty;
    while (bytesLeft > 0)
    {
        if (bytesLeft < GenericChannelHeader.BinarySize)
        {
            throw new InvalidOperationException($"Invalid payload size. This will only happen when an invalid amount of data was copied from the streamer.");
        }

        // First read the Generic Header.
        var genericChannelHeader = new GenericChannelHeader(binaryReader);
        bytesLeft -= genericChannelHeader.GetBinarySize();

        // Then based on the channel type, a Channel Specific Header might follow.
        switch (genericChannelHeader.ChannelType)
        {
            // For all Analog Channels.
            case ChannelTypes.Analog:
                var analogChannelHeader = new AnalogChannelHeader(genericChannelHeader, binaryReader);
                bytesLeft -= analogChannelHeader.GetBinarySize();
                var analogDataPacket = new AnalogDataPacket(genericChannelHeader, analogChannelHeader, binaryReader);
                bytesLeft -= analogDataPacket.GetBinarySize();

                if (firstAnalogTimestamp == 0)
                {
                    firstAnalogTimestamp = genericChannelHeader.Timestamp;
                }
                break;

            // As with Analog Data, CAN FD has a Specific Header followed by the Data.
            case ChannelTypes.CanFd:
                var canFdChannelHeader = new CanFdChannelHeader(binaryReader);
                bytesLeft -= canFdChannelHeader.GetBinarySize();
                var canFdDataPacket = new CanFdDataPacket(genericChannelHeader, canFdChannelHeader, binaryReader);
                bytesLeft -= canFdDataPacket.GetBinarySize();
                break;

            // Tacho channels does not have a Specific Channel Header, hence we can copy the Packet directly.
            case ChannelTypes.Tacho:
                var tachoDataPacket = new TachoDataPacket(genericChannelHeader, binaryReader); 
                bytesLeft -= tachoDataPacket.GetBinarySize();
                break;

            // Now print and save the GPS packets.
            case ChannelTypes.Gps:
                var gpsChannelHeader = new GpsChannelHeader(binaryReader);
                bytesLeft -= gpsChannelHeader.GetBinarySize();
                var gpsChannelPacket = new GpsDataPacket(genericChannelHeader, gpsChannelHeader, binaryReader);
                bytesLeft -= gpsChannelPacket.GetBinarySize();

                var message = new StringBuilder();
                message.AppendLine($"BEGIN");
                message.AppendLine($"Api Timestamp: {genericChannelHeader.Timestamp}, UBlox Timestamp: {gpsChannelHeader.Timestamp}, Accuracy: {gpsChannelHeader.AccuracyInNanoSeconds} ns, Is Leap Seconds Valid: {gpsChannelHeader.IsLeapSecondsValid == 1}, Leap Seconds: {gpsChannelHeader.LeapSeconds}");
                message.Append(gpsChannelPacket.Message);
                message.AppendLine("END");

                var text = message.ToString();
                File.AppendAllText(outputFileName, text);

                var startIndex = text.IndexOf("$GPGGA");
                if (startIndex > 0)
                {
                    var endIndex = text.IndexOf("\r\n", startIndex); // Always \r\n for UBlock chip!
                    ggaMessage = text.Substring(startIndex + 7, endIndex - startIndex - 7);
                    File.AppendAllText(gpggaFileName, Invariant($"{genericChannelHeader.Timestamp},{gpsChannelHeader.Timestamp:R},{ggaMessage},{Environment.NewLine}"));
                    Console.WriteLine($"Api Timestamp: {genericChannelHeader.Timestamp}, UBlox Timestamp: {gpsChannelHeader.Timestamp}");
                }

                stopwatch.Restart();
                break;

            // Triggered channels will not be shown in this example.
            case ChannelTypes.TriggeredData:
            case ChannelTypes.TriggeredScope:
            default:
                throw new InvalidOperationException($"The channel type received in the stream is not supported by this example.");
        }
    }

    if (!string.IsNullOrEmpty(ggaMessage)
        && firstAnalogTimestamp != 0)
    {
        var partsOfGga = ggaMessage.Split(',');
        try
        {
            var epochMs = (long)firstAnalogTimestamp / 1_000_000;  // ns -> ms
            var remainderNs = (int)(firstAnalogTimestamp % 1_000_000);  // leftover sub-ms ns, if you need precision

            DateTimeOffset epochUtc = DateTimeOffset.FromUnixTimeMilliseconds(epochMs);

            // --- Parse GPGGA time string (HHMMSS.SS) ---
            int hh = int.Parse(partsOfGga[0].Substring(0, 2));
            int mm = int.Parse(partsOfGga[0].Substring(2, 2));
            double ss = double.Parse(partsOfGga[0].Substring(4)); // seconds.fraction

            var gpsUtc = new DateTimeOffset(
                epochUtc.Year, epochUtc.Month, epochUtc.Day,
                hh, mm, (int)ss,
                TimeSpan.Zero
            ).AddSeconds(ss - (int)ss); // add fractional seconds

            // --- Compare ---
            TimeSpan diff = epochUtc - gpsUtc;
            if (intialOffset == long.MinValue)
            {
                intialOffset = (long)diff.TotalMilliseconds;
            }

            var drift = intialOffset - (diff.TotalMilliseconds);
            Console.WriteLine($"System Time: {epochUtc:yyyy-MM-dd HH:mm:ss.fffffff} Z, GPS time: {gpsUtc:yyyy-MM-dd HH:mm:ss.fffffff} Z, Sat's: {partsOfGga[6]}, Drift: {drift:F0} milliseconds");
            File.AppendAllText(driftFileName, $"{epochUtc:yyyy-MM-dd HH:mm:ss.fffffff},{gpsUtc:yyyy-MM-dd HH:mm:ss.fffffff},{drift:F0},{ggaMessage},{Environment.NewLine}");
        }
        catch (Exception e)
        {
            Console.WriteLine("unable to parse GGA message");
            throw;
        }
    }

    if (stopwatch.ElapsedMilliseconds >= 1000)
    {
        Console.WriteLine(".");
        stopwatch.Restart();
    }
}

// Once you are done reading the data from the port remember to close it.
// This will instruct QServer to stop buffering data an flush the remaining data from the buffers.
// Hence be careful not to expect a continuous series of samples when opening and closing ports!
binaryReader.Close();
networkStreamer.Close();
tcpClient.Close();

Console.WriteLine("Press any key to close.");
Console.ReadKey();
﻿// -------------------------------------------------------------------------
// Copyright (c) MECALC Technologies. All rights reserved.
// -------------------------------------------------------------------------

using QClient.RestfulClient;
using QClientAdvancedStreamData;
using QProtocol;
using QProtocol.Advanced;
using QProtocol.DataStreaming.DataPackets;
using QProtocol.DataStreaming.Headers;
using QProtocol.GenericDefines;
using QProtocol.JsonProperties;
using System.Diagnostics;
using System.Net.Sockets;

Console.WriteLine("QClient Advanced - Stream Data");
Console.WriteLine("This example will demonstrate how to enable and stream data from Analog, Tacho and CAN FD Channels.");
Console.WriteLine("Master Sampling Rate and Module setup should be made via QAcquire. Ensure a few channels are enabled.");
Console.WriteLine(string.Empty);

// You can either specify the system IP here or in the console when the application runs.
string ipAddress = "";
if (string.IsNullOrEmpty(ipAddress))
{
    ipAddress = GetIpAndValidate.AskUser().ToString();
    GetIpAndValidate.PingIp(ipAddress);
}

// First create a new instance of the RestfulInterface class. This class provide methods to send Get, Put and Delete requests
// to QServer over a Http protocol.
var httpConnection = new RestfulInterface($"http://{ipAddress}:8080");

// Only the Data Streaming state will be shown in this example. Master Sampling Rate and Module setup should be done through QAcquire.
var itemList = Item.CreateList(httpConnection);

// To ensure the Data Stream is enabled on the channels:
var channelCount = 0;
foreach (var item in itemList)
{
    // Skip Non Channel types and Unsupported types too.
    if (item.ItemTypeIdentifier != (int)Types.ItemType.Channel
        || item.ItemNameIdentifier == (int)Types.ChannelType.Unsupported)
    {
        continue;
    }

    // This is a more generic way of setting the Data Streaming states. The Data Streaming state can also be set during the
    // Channel Settings setup as shown in previous examples.
    var itemOperationMode = item.GetItemOperationMode();
    if (Convert.ToInt32(itemOperationMode.Settings[0].Value) == 0)
    {
        Console.WriteLine($"Item {item.ItemId}, {item.ItemName} ({item.ItemNameIdentifier}) is disabled thus cannot stream data.");
        continue;
    }

    // Then enable the streaming state by fetching the settings.
    var itemSettings = item.GetItemSettings();

    // Convert the results from the Generic JSON class to the Data specific class.
    var dataState = Setting.ConvertTo<Data>(itemSettings.Data);

    // Set the streaming state.
    dataState.StreamingState = Generic.Status.Enabled;

    // Some systems might have internal storage, when available the setting will not be null.
    if (dataState.LocalStorage != null)
    {
        dataState.LocalStorage = Generic.Status.Disabled;
    }

    // Convert the specific class back to the generic class and Put the new settings to QServer.
    itemSettings.Data = Setting.ConvertFrom(dataState);
    item.PutItemSettings(itemSettings);
    channelCount++;

    Console.WriteLine($"Item {item.ItemId}, {item.ItemName} ({item.ItemNameIdentifier}) was enabled to stream data.");
}

if (channelCount == 0)
{
    Console.WriteLine($"No channels were enabled thus the application cannot stream data.");
    Environment.Exit(-1);
}

// Do not forget to Apply.
httpConnection.Put(EndPoints.SystemSettingsApply);

// To stream data we will need a new socket connection. QServer uses a separate TCP or Websocket port for it's data stream.
// Only the TCP port will be shown in this example. For Websocket the same structure can be used, its just the connection which will differ.
// QServer can stream to different ports hence query which one is available:
var streamingSetup = httpConnection.Get<DataStreamSetup>(EndPoints.DataStreamSetup);

Console.WriteLine($"Ready to stream data from http://{ipAddress}:{streamingSetup.TCPPort}. Press C to stop.");

using var tcpClient = new TcpClient(ipAddress, streamingSetup.TCPPort);
using var networkStreamer = tcpClient.GetStream();
var buffer = new byte[1024];

// A few variables for pushing updates to the console.
var timer = Stopwatch.StartNew();
var lastUpdate = 0l;
var packetsReceived = 0;

var analogDataPackets = new List<AnalogDataPacket>();
var canFdDataPackets = new List<CanFdDataPacket>();
var tachoDataPackets = new List<TachoDataPacket>();
var analogDataPacketCounter = 0l;
var canFdDataPacketCounter = 0l;
var tachoDataPacketCounter = 0l;

// At this point QServer will start to package data and send it over your port.
// A Loop for a longer runtime
Console.WriteLine(string.Empty);
Console.WriteLine("Timestamp:");
Console.WriteLine("Analog Channels:");
Console.WriteLine("CAN FD Channels:");
Console.WriteLine("Tacho Channels:");
while (true)
{
    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.C)
    {
        break;
    }

    if (networkStreamer.DataAvailable == false)
    {
        Thread.Sleep(1);
        continue;
    }

    // The first step is to read the packet header, it consists of 32 bytes.
    networkStreamer.ReadExactly(buffer, 0, (int)PacketHeader.BinarySize);


    // QProtocol has classes defined for all the Data Headers, packets and types.
    // You'll need to create a BinaryStreamer from your data buffer and feed it into the defined classes.
    PacketHeader packetHeader;
    using (var memoryStream = new MemoryStream(buffer))
    using (var packetHeaderReader = new BinaryReader(memoryStream))
    {
        packetHeader = new PacketHeader(packetHeaderReader);
        packetsReceived++;

        // Increase the buffer size if the payload is too big.
        if (buffer.Length < packetHeader.PayloadSize)
        {
            buffer = new byte[packetHeader.PayloadSize];
        }

        // Multiple payload types exists, hence you'll need to take care that you can support them all or discard the data when not.
        // With this example only Payload type 0 will be shown, hence discard the data of others.
        if (packetHeader.PayloadType != 0)
        {
            networkStreamer.ReadExactly(buffer, 0, (int)packetHeader.PayloadSize);
            continue;
        }
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

    networkStreamer.ReadExactly(buffer, 0, (int)bytesLeft);
    using (var memoryStream = new MemoryStream(buffer))
    using (var payloadReader = new BinaryReader(memoryStream))
    {
        while (bytesLeft > 0)
        {
            if (bytesLeft < GenericChannelHeader.BinarySize)
            {
                throw new InvalidOperationException($"Invalid payload size. This will only happen when an invalid amount of data was copied from the streamer.");
            }

            // First read the Generic Header.
            var genericChannelHeader = new GenericChannelHeader(payloadReader);
            bytesLeft -= genericChannelHeader.GetBinarySize();

            // Then based on the channel type, a Channel Specific Header might follow.
            switch (genericChannelHeader.ChannelType)
            {
                // For all Analog Channels.
                case ChannelTypes.Analog:
                    // Analog Channels will always have a Specific Header.
                    // It contains information like Data Integrity, minimum and maximum values.
                    var analogChannelHeader = new AnalogChannelHeader(genericChannelHeader, payloadReader);
                    bytesLeft -= analogChannelHeader.GetBinarySize();

                    // And last, read the Data Samples.
                    // QProtocol provides classes which combines the Headers and Data into one.
                    var analogDataPacket = new AnalogDataPacket(genericChannelHeader, analogChannelHeader, payloadReader);
                    bytesLeft -= analogDataPacket.GetBinarySize();

                    // Store the data in a buffer or save it locally. Just be careful not to stall the thread too much at this point.
                    analogDataPackets.Add(analogDataPacket);
                    break;

                // As with Analog Data, CAN FD has a Specific Header followed by the Data.
                case ChannelTypes.CanFd:
                    var canFdChannelHeader = new CanFdChannelHeader(payloadReader);
                    bytesLeft -= canFdChannelHeader.GetBinarySize();

                    var canFdDataPacket = new CanFdDataPacket(genericChannelHeader, canFdChannelHeader, payloadReader);
                    bytesLeft -= canFdDataPacket.GetBinarySize();

                    canFdDataPackets.Add(canFdDataPacket);
                    break;

                // Tacho channels does not have a Specific Channel Header, hence we can copy the Packet directly.
                case ChannelTypes.Tacho:
                    var tachoDataPacket = new TachoDataPacket(genericChannelHeader, payloadReader);
                    bytesLeft -= tachoDataPacket.GetBinarySize();

                    tachoDataPackets.Add(tachoDataPacket);
                    break;

                // Triggered channels will not be shown in this example.
                case ChannelTypes.TriggeredData:
                case ChannelTypes.TriggeredScope:
                default:
                    throw new InvalidOperationException($"The channel type received in the stream is not supported by this example.");
            }
        }
    }

    analogDataPacketCounter += analogDataPackets.Count;
    canFdDataPacketCounter += canFdDataPackets.Count;
    tachoDataPacketCounter += tachoDataPackets.Count;

    // Now that the entire payload has been read, print the data to the screen and start all over.
    if (timer.ElapsedMilliseconds > lastUpdate + 250)
    {
        lastUpdate = timer.ElapsedMilliseconds;
        var timeStampDateTime = DateTime.MinValue;

        if (analogDataPackets.Count > 0)
        {
            var timeStampEpoch = analogDataPackets.First().GenericChannelHeader.Timestamp;
            timeStampDateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(timeStampEpoch / 1000000))
                                              .DateTime;
        }

        Console.SetCursorPosition(0, Console.CursorTop - 4);
        Console.WriteLine($"Runtime: {timer.Elapsed:hh':'mm':'ss} - Packets received: {packetsReceived} - Timestamp: {timeStampDateTime}");
        Console.WriteLine($"Analog Channel Payloads: {analogDataPacketCounter}");
        Console.WriteLine($"CAN FD Channel Payloads: {canFdDataPacketCounter}");
        Console.WriteLine($"Tacho Channel Payloads: {tachoDataPacketCounter}");
    }

    analogDataPackets.Clear();
    canFdDataPackets.Clear();
    tachoDataPackets.Clear();
}

// Once you are done reading the data from the port remember to close it.
// This will instruct QServer to stop buffering data an flush the remaining data from the buffers.
// Hence be careful not to expect a continuous series of samples when opening and closing ports!
networkStreamer.Close();
tcpClient.Close();

Console.WriteLine("Thats it, press any key to exit.");
Console.ReadKey();

// -------------------------------------------------------------------------
// Copyright (c) Mecalc (Pty) Limited. All rights reserved.
// -------------------------------------------------------------------------
//
// NOTE: The code supplied here are not supported by Mecalc. Use it at own risk!
// -------------------------------------------------------------------------

using QClient.RestfulClient;
using QProtocol;
using QProtocol.Advanced;
using QProtocol.GenericDefines;
using QProtocol.InternalModules.ALO;
using System.Net.Sockets;

namespace AloDataStreaming;

internal class LoopData
{
    private readonly string ipAddress;
    private readonly List<Item> itemList;
    private readonly RestfulInterface restfulInterface;

    public LoopData(string ipAddress, List<Item> itemList, RestfulInterface restfulInterface)
    {
        this.ipAddress = ipAddress;
        this.itemList = itemList;
        this.restfulInterface = restfulInterface;
    }

    public void RunLoopExample()
    {
        // Change this value for the buffer size.
        uint blockSize = 1024; //1 < 1024 (default) < 6144000;

        // First the Module and Channel Setup
        var listOfAloModules = new List<ALO42S4Module>();
        var listOfAloChannels = new List<ALO42S4Channel>();
        foreach (var item in itemList)
        {
            switch (item)
            {
                // The Module Setup is basic, set the operation mode to the Arbitrary Waveform option which will configure the channels accordingly.
                // Then set the sampling rate, please take care that it is set correct  for your expected output frequency of the signal you intend to stream.
                case ALO42S4Module alo42S4Module:
                    listOfAloModules.Add(alo42S4Module);

                    alo42S4Module.PutItemOperationMode(ALO42S4Module.OperationMode.ArbitraryWaveform);
                    var newModuleSettings = alo42S4Module.GetItemSettings<ALO42S4Module.ArbitraryWaveformSettings>();
                    newModuleSettings.Settings.SampleRate = ALO42S4Module.SampleRate.MsrDivideBy4;
                    newModuleSettings.Settings.Grounding = ALO42S4Module.Grounding.Floating;
                    alo42S4Module.PutItemSettings(newModuleSettings);
                    break;

                // Channel Operation Mode can be set to Buffered if you would like to upload one block of samples and have the Module loop through this buffer indefinitely.
                case ALO42S4Channel alo42S4Channel:
                    listOfAloChannels.Add(alo42S4Channel);

                    alo42S4Channel.PutItemOperationMode(ALO42S4Channel.OperationMode.Buffered);
                    var channelSettings = alo42S4Channel.GetItemSettings<ALO42S4Channel.BufferedSettings>();
                    channelSettings.Settings.SampleCount = blockSize;
                    channelSettings.Settings.OutputVoltageLevel = ALO42S4Channel.OutputVoltageLevel._5V;
                    alo42S4Channel.PutItemSettings(channelSettings);
                    break;

                default:
                    continue;
            }
        }

        // Call the apply once after all the settings was set
        restfulInterface.Put(EndPoints.SystemSettingsApply);

        // Create the data block to be sent to the ALO.
        var sineWaveData = GenerateSineWave(blockSize);

        // This loop demonstrates how a user can start and stop the output on demand.
        while (true)
        {
            // Fill the buffers of each Channel with the data.
            listOfAloChannels.ForEach(channel =>
            {
                var portNumber = channel.GetPortNumber();
                SendData(ipAddress, portNumber, sineWaveData);
            });

            // Wait for user key press to start output
            Console.WriteLine("Press any key to enable output...");
            Console.ReadKey(true);

            // Key pressed, start the output.
            var startTime = restfulInterface.Put<AloOutputStartTime>(EndPoints.AloStartDataStreaming);
            Console.WriteLine("Output started...");
            Console.WriteLine();

            // Wait for user key press to signal cancellation
            Console.WriteLine("Press any key to stop output, or c to cancel...");
            var key = Console.ReadKey(true).Key;
            restfulInterface.Put(EndPoints.AloStopDataStreaming);

            // Check for C, else loop again.
            if (key == ConsoleKey.C)
            {
                break;
            }
        }
        
        listOfAloChannels.ForEach(channel => channel.ClearData());
    }

    /// <summary>
    /// This method is used to create a sampled sine wave that can be streamed to the ALO Module.
    /// This is only an example, use it at own risk.
    /// </summary>
    /// <param name="dataBlockSize">The data block size of the ALO Channel.</param>
    /// <returns>An array of sampled sine wave data.</returns>
    static float[] GenerateSineWave(uint dataBlockSize)
    {
        var wave = new float[dataBlockSize];
        var angleStep = 2.0 * Math.PI / dataBlockSize;

        for (var i = 0; i < dataBlockSize; i++)
        {
            wave[i] = (float)(5 * Math.Sin(angleStep * i));
        }

        return wave;
    }

    /// <summary>
    /// This method will send the analog data to the specified port.
    /// </summary>
    /// <param name="serverIp">The IP address of the QServer instance.</param>
    /// <param name="port">The port of the ALO Channel. This is obtained by calling the GET PortNumber endpoint on the ALO channel.</param>
    /// <param name="values">An array of floats to be sent to the ALO channel, this must match the given block size.</param>
    static void SendData(string serverIp, uint port, float[] values)
    {
        try
        {
            using TcpClient client = new();
            client.Connect(serverIp, (int)port);
            Console.WriteLine($"Connected to {serverIp}:{port}");

            using NetworkStream stream = client.GetStream();
            var buffer = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, buffer, 0, buffer.Length);
            stream.Write(buffer, 0, buffer.Length);
            Console.WriteLine($"Disconnected from {serverIp}:{port}");
        }
        catch (Exception ex)
        {
            // You may want to check token.IsCancellationRequested here to differentiate user cancellation from real errors
            Console.WriteLine($"Error on port {port}: {ex.Message}");
        }
    }
}

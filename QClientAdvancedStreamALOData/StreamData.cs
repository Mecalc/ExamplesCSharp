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

internal class StreamData
{
    private readonly string ipAddress;
    private readonly List<Item> itemList;
    private readonly RestfulInterface restfulInterface;

    public StreamData(string ipAddress, List<Item> itemList, RestfulInterface restfulInterface)
    {
        this.ipAddress = ipAddress;
        this.itemList = itemList;
        this.restfulInterface = restfulInterface;
    }

    public void RunStreamingExample()
    {
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

                // Channel Operation Mode should change to streaming after the Module has been set, for safety you can set it again.
                // For Streaming, set the TotalSampleCount to 0. You can choose what happens with the signal if the stream gets interrupted, for this example we will fade to 0.
                case ALO42S4Channel alo42S4Channel:
                    listOfAloChannels.Add(alo42S4Channel);

                    alo42S4Channel.PutItemOperationMode(ALO42S4Channel.OperationMode.Streaming);
                    var channelSettings = alo42S4Channel.GetItemSettings<ALO42S4Channel.StreamingSettings>();
                    channelSettings.Settings.Fallback = ALO42S4Channel.Fallback.Zero;
                    channelSettings.Settings.OutputVoltageLevel = ALO42S4Channel.OutputVoltageLevel._5V;
                    alo42S4Channel.PutItemSettings(channelSettings);
                    break;

                default:
                    continue;
            }
        }

        // Call the apply once after all the settings was set
        restfulInterface.Put(EndPoints.SystemSettingsApply);

        // QServer will specify a block size which you need to use for the streaming port.
        var blockSize = (int)listOfAloModules[0].GetBlockSize();
        if (blockSize == 0)
        {
            Console.WriteLine("Could not obtain a valid block size.");
            return;
        }

        // Create the data block to be sent to the ALO.
        var waveData = GenerateSineWave(blockSize * 32);
        var buffer = new byte[waveData.Length * sizeof(float)];
        Buffer.BlockCopy(waveData, 0, buffer, 0, buffer.Length);

        while (true)
        {
            // Create a task per channel for the data stream, each will independently send data to the DecaQ with a blocking TCP socket.
            var methodSource = new CancellationTokenSource();
            var taskSource = new CancellationTokenSource();

            var taskList = new List<Task>();
            foreach (var channel in listOfAloChannels)
            {
                var portNumber = channel.GetPortNumber();
                var task = new Task(() => SendData(ipAddress, portNumber, buffer, blockSize, methodSource.Token), taskSource.Token, TaskCreationOptions.LongRunning);
                task.Start();
                taskList.Add(task);
            }

            // Allow Buffers to fill and sockets to start blocking
            Thread.Sleep(1000);

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

            // Stop the ALO output for all Modules. This will also clear the buffers.
            restfulInterface.Put(EndPoints.AloStopDataStreaming);
            methodSource.Cancel();

            // Wait for all threads to complete
            if (Task.WaitAll(taskList.ToArray(), 5000) == false)
            {
                taskSource.Cancel();
            }

            // Check for C, else loop again.
            if (key == ConsoleKey.C)
            {
                break;
            }
        }

        // This is not need anymore since the stop endpoint will clear the buffers.
        // But should you want to ensure the buffers are empty or clear them if incorrect data was sent before, then this is how you'd do it.
        listOfAloChannels.ForEach(channel => channel.ClearData());
    }

    /// <summary>
    /// This method is used to create a sampled sine wave that can be streamed to the ALO Module.
    /// This is only an example, use it at own risk.
    /// </summary>
    /// <param name="dataBlockSize">The data block size of the ALO Channel.</param>
    /// <returns>An array of sampled sine wave data.</returns>
    static float[] GenerateSineWave(int dataBlockSize)
    {
        var wave = new float[dataBlockSize];
        var angleStep = 2.0 * Math.PI / dataBlockSize;

        for (var i = 0; i < dataBlockSize; i++)
        {
            wave[i] = (float)Math.Sin(angleStep * i);
        }

        return wave;
    }

    /// <summary>
    /// This method will send the analog data to the specified port.
    /// </summary>
    /// <param name="serverIp">The IP address of the QServer instance.</param>
    /// <param name="port">The port of the ALO Channel. This is obtained by calling the GET PortNumber endpoint on the ALO channel.</param>
    /// <param name="values">An array of floats to be sent to the ALO channel, this must match the given block size.</param>
    /// <param name="token">A cancellation token to stop the thread.</param>
    static void SendData(string serverIp, uint port, byte[] byteArray, int blockSize, CancellationToken token)
    {
        try
        {
            using TcpClient client = new();
            client.Connect(serverIp, (int)port);
            Console.WriteLine($"Connected to {serverIp}:{port}");

            using NetworkStream stream = client.GetStream();
            var index = 0;
            while (!token.IsCancellationRequested)
            {
                stream.Write(byteArray, index, blockSize);
                index += blockSize;
                if (index >= byteArray.Length - blockSize)
                {
                    index = 0;
                }   
            }

            Console.WriteLine($"Disconnected from {serverIp}:{port}");
        }
        catch (Exception ex)
        {
            // You may want to check token.IsCancellationRequested here to differentiate user cancellation from real errors
            Console.WriteLine($"Error on port {port}: {ex.Message}");
        }
    }
}

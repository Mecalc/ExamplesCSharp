// -------------------------------------------------------------------------
// Copyright (c) Mecalc (Pty) Limited. All rights reserved.
// -------------------------------------------------------------------------
// ALO Data Streaming Demo
// In this  example we will demonstrate how to stream data from an ALO device
// using the QProtocol library. Two modes of operation will be demonstrated,
// streaming and looping data. The streaming mode will continuously stream
// data from your pc to the ALO Module, while the looping mode will send a
// predefined set of data into a buffer and loop the buffered data to the ALO.
//
// You need two additional projects for this example to work, QProtocol and QClient.
// They should be placed one level above this project's directory.
// If they are not present, then you'll have to setup the project references manually.
//
// NOTE: The code supplied here are not supported by Mecalc. Use it at own risk!
// -------------------------------------------------------------------------

using QClient.RestfulClient;
using QProtocol;
using QProtocol.Advanced;
using QProtocol.Controllers;
using QProtocol.GenericDefines;

namespace AloDataStreaming;

public class Program
{
    static void Main()
    {
        Console.WriteLine("ALO data streaming demo");
        string ipAddress = "192.168.178.101";

        var restfulApi = new RestfulInterface($"http://{ipAddress}:8080");
        var pingResult = restfulApi.Get<InfoPing>(EndPoints.InfoPing);
        if (pingResult.Code == 0
            && pingResult.Message.Equals("System is operational"))
        {
            Console.WriteLine(pingResult.Message);
        }
        else
        {
            throw new ApplicationException("Unable to connect to QServer!");
        }

        var itemList = Item.CreateList(restfulApi);

        // For this example we will set the system to a MSR of 131072.
        var controller = (Controller)itemList.First();
        var enabledSettings = controller.GetItemSettings<Controller.EnabledSettings>();
        enabledSettings.Settings.MasterSamplingRate = Controller.MasterSamplingRate._192000Hz;
        controller.PutItemSettings(enabledSettings);

        // Select between stream or loop
        Console.Write("Would you like to stream data [0] or loop data [1]: ");
        var choice = Console.ReadKey().Key;
        Console.WriteLine();
        switch (choice)
        {
            case ConsoleKey.D0:
            case ConsoleKey.NumPad0:
                new StreamData(ipAddress, itemList, restfulApi).RunStreamingExample();
                break;

            case ConsoleKey.D1:
            case ConsoleKey.NumPad1:
                new LoopData(ipAddress, itemList, restfulApi).RunLoopExample();
                break;
            default:
                break;
        }

        Console.WriteLine("All threads stopped. Press any key to exit...");
        Console.ReadKey();
    }
}
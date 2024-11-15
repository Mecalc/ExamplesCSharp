// -------------------------------------------------------------------------
// Copyright (c) MECALC Technologies. All rights reserved.
// -------------------------------------------------------------------------

using QClient.RestfulClient;
using QClientBasics;
using QProtocol.Advanced;
using QProtocol.Controllers;
using QProtocol.GenericDefines;
using QProtocol.InternalModules.ICS;
using QProtocol.InternalModules.WSB;

Console.WriteLine("QClient Advanced - Configure Items");
Console.WriteLine("This example will demonstrate how to use the Advanced namespace of QProtocol and QClient to ease your integration efforts.");
Console.WriteLine("A connection to QServer will be established and the Items configured in an effortless way.");
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

// The Item class will be used here to unlock some additional build in functionality of the QProtocol library.
// There are several ways one can construct the Item class as described below:

// Since the Controller is the main Parent of the structure, you can instantiate an Item and cast it to the Controller Type.
// All Item types have be derived from the base Item class.
// This instantiation will query QServer and create the entire Item Tree for you. 
var contoller = (Controller)Item.Create(httpConnection);

// Another way is to create an Item List:
// The controller will then be the first entry of the list.
var itemList = Item.CreateList(httpConnection);
contoller = (Controller)itemList[0];

// To update the Master Sampling Rate of the system we can now use the build in functions to query the settings.
// You'll need to specify which type of settings the query needs to covert to, for the controller this will always be EnabledSettings.
var controllerSettings = contoller.GetItemSettings<Controller.EnabledSettings>();
controllerSettings.Settings.MasterSamplingRate = Controller.MasterSamplingRate._204800Hz;
contoller.PutItemSettings(controllerSettings);

// Now to enable all of the WSB and ICS Modules, and to change their sampling rates we can do the following.
// You'll have to update this list for specific builds that are not listed here. 
foreach (var item in itemList)
{
    switch (item)
    {
        case ICS425Module ics425Module:
            var ics425OperationMode = ics425Module.GetItemOperationMode();
            if (ics425OperationMode != ICS425Module.OperationMode.Enabled)
            {
                ics425Module.PutItemOperationMode(ICS425Module.OperationMode.Enabled);
            }

            var ics425Settings = ics425Module.GetItemSettings<ICS425Module.EnabledSettings>();
            ics425Settings.Settings.SampleRate = ICS425Module.SampleRate.MsrDivideBy2;
            ics425Settings.Settings.Grounding = ICS425Module.Grounding.Floating;
            ics425Module.PutItemSettings(ics425Settings);
            break;

        case WSB42X5Module wsb42x5Module:
            var wsb42x5OperaionMode = wsb42x5Module.GetItemOperationMode();
            if (wsb42x5OperaionMode != WSB42X5Module.OperationMode.Enabled)
            {
                wsb42x5Module.PutItemOperationMode(wsb42x5OperaionMode);
            }

            var wsb42x5Settings = wsb42x5Module.GetItemSettings<WSB42X5Module.EnabledSettings>();
            wsb42x5Settings.Settings.SampleRate = WSB42X5Module.SampleRate.MsrDivideBy1;
            wsb42x5Module.PutItemSettings(wsb42x5Settings);
            break;
    }
}

// To configure the Channels one can follow the same structure as above. Channels will likely have more than one Operation Mode hence take care
// on casting to the correct Settings Class Type. Let see how this can be done.
foreach (var item in itemList)
{
    switch (item)
    {
        case ICS425Channel ics425Channel:
            // Either set or get the Operation Mode, this will determine which Settings class to use for the Get Endpoint call.
            var ics425OperationMode = ICS425Channel.OperationMode.IcpInput;
            ics425Channel.PutItemOperationMode(ics425OperationMode);

            switch (ics425OperationMode)
            {
                case ICS425Channel.OperationMode.Disabled:
                    // No settings will be available for disabled channels, hence skip this part.
                    break;

                case ICS425Channel.OperationMode.VoltageInput:
                    var voltageInputSettings = ics425Channel.GetItemSettings<ICS425Channel.VoltageInputSettings>();
                    voltageInputSettings.Settings.VoltageRange = ICS425Channel.VoltageRange._1V;
                    voltageInputSettings.Settings.VoltageInputCoupling = ICS425Channel.VoltageInputCoupling.Dc;
                    voltageInputSettings.Settings.InputBiasing = ICS425Channel.InputBiasing.Differential;
                    ics425Channel.PutItemSettings(voltageInputSettings);
                    break;

                case ICS425Channel.OperationMode.IcpInput:
                    var icpInputSettings = ics425Channel.GetItemSettings<ICS425Channel.IcpInputSettings>();

                    // You can also create new instances of the Settings class should you want to recover settings from a config file.
                    icpInputSettings.Settings = new ICS425Channel.IcpInputSettings
                    {
                        VoltageRange = ICS425Channel.VoltageRange._100mV,
                        IcpInputCoupling = ICS425Channel.IcpInputCoupling.AcWith1HzFilter,
                        IcpInputCurrentSource = ICS425Channel.IcpInputCurrentSource._4mA,
                        InputBiasing = ICS425Channel.InputBiasing.SingleEnded,
                    };

                    ics425Channel.PutItemSettings(icpInputSettings);
                    break;
            }
            break;

        case WSB42X5Channel wsb42x5Channel:
            var wsb42x5OperationMode = wsb42x5Channel.GetItemOperationMode();
            switch (wsb42x5OperationMode)
            {
                case WSB42X5Channel.OperationMode.Disabled:
                    break;

                case WSB42X5Channel.OperationMode.VoltageInput:
                    var voltageInputSettings = wsb42x5Channel.GetItemSettings<WSB42X5Channel.VoltageInputSettings>();
                    voltageInputSettings.Settings.VoltageRange = WSB42X5Channel.VoltageRange._1V;
                    voltageInputSettings.Settings.VoltageInputCoupling = WSB42X5Channel.VoltageInputCoupling.Dc;
                    wsb42x5Channel.PutItemSettings(voltageInputSettings);
                    break;

                case WSB42X5Channel.OperationMode.IcpInput:
                    var icpInputSettings = wsb42x5Channel.GetItemSettings<WSB42X5Channel.IcpInputSettings>();
                    icpInputSettings.Settings.VoltageRange = WSB42X5Channel.VoltageRange._1V;
                    icpInputSettings.Settings.IcpInputCoupling = WSB42X5Channel.IcpInputCoupling.AcWith1HzFilter;
                    icpInputSettings.Settings.IcpInputCurrentSource = WSB42X5Channel.IcpInputCurrentSource._8mA;
                    wsb42x5Channel.PutItemSettings(icpInputSettings);
                    break;

                case WSB42X5Channel.OperationMode.WsbInputVoltageExcitation:
                    var voltageBridgeSettings = wsb42x5Channel.GetItemSettings<WSB42X5Channel.WsbInputVoltageExcitationSettings>();
                    voltageBridgeSettings.Settings.VoltageRange = WSB42X5Channel.VoltageRange._10mV;
                    voltageBridgeSettings.Settings.VoltageInputCoupling = WSB42X5Channel.VoltageInputCoupling.Dc;
                    voltageBridgeSettings.Settings.BridgeMode = WSB42X5Channel.BridgeMode.Full;
                    voltageBridgeSettings.Settings.ExcitationAmplitude = 5;
                    voltageBridgeSettings.Settings.ExcitationSensePoint = WSB42X5Channel.ExcitationSensePoint.External;
                    wsb42x5Channel.PutItemSettings(voltageBridgeSettings);
                    break;

                case WSB42X5Channel.OperationMode.WsbInputFourWireCurrentExcitation:
                    var fourWireCurrentBridgeSettings = wsb42x5Channel.GetItemSettings<WSB42X5Channel.WsbInputFourWireCurrentExcitationSettings>();
                    fourWireCurrentBridgeSettings.Settings.VoltageRange = WSB42X5Channel.VoltageRange._1V;
                    fourWireCurrentBridgeSettings.Settings.VoltageInputCoupling = WSB42X5Channel.VoltageInputCoupling.Ac;
                    fourWireCurrentBridgeSettings.Settings.FourWireCurrentSource = WSB42X5Channel.FourWireCurrentSource._12mA;
                    wsb42x5Channel.PutItemSettings(fourWireCurrentBridgeSettings);
                    break;

                case WSB42X5Channel.OperationMode.WsbInputTwoWireCurrentExcitation:
                    var twoWireCurrentBridgeSettings = wsb42x5Channel.GetItemSettings<WSB42X5Channel.WsbInputTwoWireCurrentExcitationSettings>();
                    twoWireCurrentBridgeSettings.Settings.VoltageRange = WSB42X5Channel.VoltageRange._1V;
                    twoWireCurrentBridgeSettings.Settings.IcpInputCoupling = WSB42X5Channel.IcpInputCoupling.Ac;
                    twoWireCurrentBridgeSettings.Settings.TwoWireCurrentSource = WSB42X5Channel.TwoWireCurrentSource._4mA;
                    wsb42x5Channel.PutItemSettings(twoWireCurrentBridgeSettings);
                    break;
            }
            break;
    }
}

// As always, Apply to sync the settings with the Hardware.
httpConnection.Put(EndPoints.SystemSettingsApply);

Console.WriteLine(string.Empty);
Console.WriteLine("Thats it, press any key to exit.");
Console.ReadKey();
Environment.Exit(0);

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
foreach (var item in itemList)
{
    switch (item)
    {
        case ICS421Module ics421Module:
            var ics421OperationMode = ics421Module.GetItemOperationMode();
            if (ics421OperationMode != ICS421Module.OperationMode.Enabled)
            {
                ics421Module.PutItemOperationMode(ICS421Module.OperationMode.Enabled);
            }

            var ics421Settings = ics421Module.GetItemSettings<ICS421Module.EnabledSettings>();
            ics421Settings.Settings.SampleRate = ICS421Module.SampleRate.MsrDivideBy2;
            ics421Settings.Settings.Grounding = ICS421Module.Grounding.Floating;
            ics421Module.PutItemSettings(ics421Settings);
            break;

        case WSB42X2Module wsb42x2Module:
            var wsb42x2OperaionMode = wsb42x2Module.GetItemOperationMode();
            if (wsb42x2OperaionMode != WSB42X2Module.OperationMode.Enabled)
            {
                wsb42x2Module.PutItemOperationMode(wsb42x2OperaionMode);
            }

            var wsb42x2Settings = wsb42x2Module.GetItemSettings<WSB42X2Module.EnabledSettings>();
            wsb42x2Settings.Settings.SampleRate = WSB42X2Module.SampleRate.MsrDivideBy1;
            wsb42x2Module.PutItemSettings(wsb42x2Settings);
            break;
    }
}

// To configure the Channels one can follow the same structure as above. Channels will likely have more than one Operation Mode hence take care
// on casting to the correct Settings Class Type. Let see how this can be done.
foreach (var item in itemList)
{
    switch (item)
    {
        case ICS421Channel ics421Channel:
            // Either set or get the Operation Mode, this will determine which Settings class to use for the Get Endpoint call.
            var newOperationMode = ICS421Channel.OperationMode.IcpInput;
            ics421Channel.PutItemOperationMode(newOperationMode);

            switch (newOperationMode)
            {
                case ICS421Channel.OperationMode.Disabled:
                    // No settings will be available for disabled channels, hence skip this part.
                    break;

                case ICS421Channel.OperationMode.VoltageInput:
                    var voltageInputSettings = ics421Channel.GetItemSettings<ICS421Channel.VoltageInputSettings>();
                    voltageInputSettings.Settings.VoltageRange = ICS421Channel.VoltageRange._1V;
                    voltageInputSettings.Settings.VoltageInputCoupling = ICS421Channel.VoltageInputCoupling.Dc;
                    voltageInputSettings.Settings.InputBiasing = ICS421Channel.InputBiasing.Differential;
                    ics421Channel.PutItemSettings(voltageInputSettings);
                    break;

                case ICS421Channel.OperationMode.IcpInput:
                    var icpInputSettings = ics421Channel.GetItemSettings<ICS421Channel.IcpInputSettings>();

                    // You can also create new instances of the Settings class should you want to recover settings from a config file.
                    icpInputSettings.Settings = new ICS421Channel.IcpInputSettings
                    {
                        VoltageRange = ICS421Channel.VoltageRange._100mV,
                        IcpInputCoupling = ICS421Channel.IcpInputCoupling.AcWith1HzFilter,
                        IcpInputCurrentSource = ICS421Channel.IcpInputCurrentSource._4mA,
                        InputBiasing = ICS421Channel.InputBiasing.SingleEnded,
                    };

                    ics421Channel.PutItemSettings(icpInputSettings);
                    break;
            }
            break;

        case WSB42X2Channel wsb42x2Channel:
            var wsb42x2OperationMode = wsb42x2Channel.GetItemOperationMode();
            switch (wsb42x2OperationMode)
            {
                case WSB42X2Channel.OperationMode.Disabled:
                    break;

                case WSB42X2Channel.OperationMode.VoltageInput:
                    var voltageInputSettings = wsb42x2Channel.GetItemSettings<WSB42X2Channel.VoltageInputSettings>();
                    voltageInputSettings.Settings.VoltageRange = WSB42X2Channel.VoltageRange._1V;
                    voltageInputSettings.Settings.VoltageInputCoupling = WSB42X2Channel.VoltageInputCoupling.Dc;
                    wsb42x2Channel.PutItemSettings(voltageInputSettings);
                    break;

                case WSB42X2Channel.OperationMode.IcpInput:
                    var icpInputSettings = wsb42x2Channel.GetItemSettings<WSB42X2Channel.IcpInputSettings>();
                    icpInputSettings.Settings.VoltageRange = WSB42X2Channel.VoltageRange._1V;
                    icpInputSettings.Settings.IcpInputCoupling = WSB42X2Channel.IcpInputCoupling.AcWith1HzFilter;
                    icpInputSettings.Settings.IcpInputCurrentSource = WSB42X2Channel.IcpInputCurrentSource._8mA;
                    wsb42x2Channel.PutItemSettings(icpInputSettings);
                    break;

                case WSB42X2Channel.OperationMode.WsbInputVoltageExcitation:
                    var voltageBridgeSettings = wsb42x2Channel.GetItemSettings<WSB42X2Channel.WsbInputVoltageExcitationSettings>();
                    voltageBridgeSettings.Settings.VoltageRange = WSB42X2Channel.VoltageRange._10mV;
                    voltageBridgeSettings.Settings.VoltageInputCoupling = WSB42X2Channel.VoltageInputCoupling.Dc;
                    voltageBridgeSettings.Settings.BridgeMode = WSB42X2Channel.BridgeMode.Full;
                    voltageBridgeSettings.Settings.ExcitationAmplitude = 5;
                    voltageBridgeSettings.Settings.ExcitationSensePoint = WSB42X2Channel.ExcitationSensePoint.External;
                    wsb42x2Channel.PutItemSettings(voltageBridgeSettings);
                    break;

                case WSB42X2Channel.OperationMode.WsbInputFourWireCurrentExcitation:
                    var fourWireCurrentBridgeSettings = wsb42x2Channel.GetItemSettings<WSB42X2Channel.WsbInputFourWireCurrentExcitationSettings>();
                    fourWireCurrentBridgeSettings.Settings.VoltageRange = WSB42X2Channel.VoltageRange._1V;
                    fourWireCurrentBridgeSettings.Settings.VoltageInputCoupling = WSB42X2Channel.VoltageInputCoupling.Ac;
                    fourWireCurrentBridgeSettings.Settings.FourWireCurrentSource = WSB42X2Channel.FourWireCurrentSource._12mA;
                    wsb42x2Channel.PutItemSettings(fourWireCurrentBridgeSettings);
                    break;

                case WSB42X2Channel.OperationMode.WsbInputTwoWireCurrentExcitation:
                    var twoWireCurrentBridgeSettings = wsb42x2Channel.GetItemSettings<WSB42X2Channel.WsbInputTwoWireCurrentExcitationSettings>();
                    twoWireCurrentBridgeSettings.Settings.VoltageRange = WSB42X2Channel.VoltageRange._1V;
                    twoWireCurrentBridgeSettings.Settings.IcpInputCoupling = WSB42X2Channel.IcpInputCoupling.Ac;
                    twoWireCurrentBridgeSettings.Settings.TwoWireCurrentSource = WSB42X2Channel.TwoWireCurrentSource._4mA;
                    wsb42x2Channel.PutItemSettings(twoWireCurrentBridgeSettings);
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

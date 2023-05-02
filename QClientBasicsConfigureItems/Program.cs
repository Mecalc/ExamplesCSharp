// -------------------------------------------------------------------------
// Copyright (c) Mecalc (Pty) Limited. All rights reserved.
// -------------------------------------------------------------------------

using QClient.RestfulClient;
using QClientBasics;
using QProtocol;
using QProtocol.GenericDefines;
using QProtocol.Interfaces;
using QProtocol.InternalModules.ICS;
using QProtocol.JsonProperties;

Console.WriteLine("QClient Basics - Configure Items");
Console.WriteLine("This example will demonstrate some of the helpful features of the QProtocol and QClient libraries.");
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

// Start by fetching the /item/list/ which we can use to discover the available items.
// Serialization and deserialization of the requests are made easy with the pre constructed classes in the QProtocol Library.
var itemList = httpConnection.Get<List<ItemInfo>>(EndPoints.ItemList);
if (itemList.Count == 0)
{
    Console.WriteLine("Unable to fetch Item Info List.");
    Environment.Exit(-1);
}

Console.WriteLine(string.Empty);
foreach (var item in itemList)
{
    Console.WriteLine($"Found Item {item.ItemId}: {item.ItemName} (ID: {item.ItemNameIdentifier})" +
        $" of type {item.ItemType} (ID: {item.ItemTypeIdentifier})");
}

// Build a list of ICS42 Modules and Channels.
// QProtocol has all the types defined in the GenericDefines namespace, no need to lookup the values from the manual.
var icsModules = itemList.Where(item => item.ItemTypeIdentifier == (int)Types.ItemType.Module)
                         .Where(item => item.ItemNameIdentifier == (int)Types.ModuleType.ICS421)
                         .ToList();

var icsChannel = itemList.Where(item => item.ItemTypeIdentifier == (uint)Types.ItemType.Channel)
                         .Where(item => item.ItemNameIdentifier == (int)Types.ChannelType.ICS421)
                         .ToList();

// Read the Operation Mode from the Module to ensure they are enabled.
foreach (var item in icsModules)
{
    // Use the pre-defined classes to cast the response to.
    var operationMode = httpConnection.Get<ItemOperationMode>(EndPoints.ItemOperationMode, HttpParameter.ItemId(item.ItemId))!;

    // To help with Setting casting, use the ConvertTo and ConvertFrom methods on the Setting class.
    // Every Module and Channel has their Settings and Operation Modes defined as sub classes.
    // Since the OperationMode was requested above, one can cast the response to the Item Specific Operation Mode class.
    var operationModeSettings = Setting.ConvertTo<ICS421Module.ICS421ModuleOperationMode>(operationMode.Settings);

    // Now the settings are defined as standard DotNet objects, you can change it without having to use lookup tables.
    operationModeSettings.OperationMode = ICS421Module.OperationMode.Enabled;

    // To update QServer, cast the settings back to the generic class and Put it to the Server.
    operationMode.Settings = Setting.ConvertFrom(operationModeSettings);
    httpConnection.Put(EndPoints.ItemOperationMode, operationMode, HttpParameter.ItemId(item.ItemId));
}

// Now update the sample rate and other settings of interest.
foreach (var item in icsModules)
{
    // As with Operation Modes, settings has classes predefined for all endpoints.
    var moduleSettings = httpConnection.Get<ItemSettings>(EndPoints.ItemSettings, HttpParameter.ItemId(item.ItemId))!;

    // Since the Module has been set to Enabled Operation mode, one need to cast the response to the corresponding 
    // class, the Class name will match the set Operation Mode with Settings appended.
    var enabledSettings = Setting.ConvertTo<ICS421Module.EnabledSettings>(moduleSettings.Settings);

    enabledSettings.SampleRate = ICS421Module.SampleRate.MsrDivideBy2;
    enabledSettings.Grounding = ICS421Module.Grounding.Floating;

    moduleSettings.Settings = Setting.ConvertFrom(enabledSettings);
    httpConnection.Put(EndPoints.ItemSettings, moduleSettings, HttpParameter.ItemId(item.ItemId));
}

// Lets enable ICP Mode on all the channels too.
foreach (var item in icsChannel)
{
    var channelOperationMode = httpConnection.Get<ItemOperationMode>(EndPoints.ItemOperationMode, HttpParameter.ItemId(item.ItemId));
    var operationModeSettings = Setting.ConvertTo<ICS421Channel.ICS421ChannelOperationMode>(channelOperationMode.Settings);
    operationModeSettings.OperationMode = ICS421Channel.OperationMode.IcpInput;

    channelOperationMode.Settings = Setting.ConvertFrom(operationModeSettings);
    httpConnection.Put(EndPoints.ItemOperationMode, channelOperationMode, HttpParameter.ItemId(item.ItemId));

    // Settings are allowed to change directly after an Operation Mode change.
    var channelSettings = httpConnection.Get<ItemSettings>(EndPoints.ItemSettings, HttpParameter.ItemId(item.ItemId));
    var icpInputSettings = Setting.ConvertTo<ICS421Channel.IcpInputSettings>(channelSettings.Settings);
    icpInputSettings.VoltageRange = ICS421Channel.VoltageRange._1V;
    icpInputSettings.IcpInputCoupling = ICS421Channel.IcpInputCoupling.AcWith1HzFilter;
    icpInputSettings.IcpInputCurrentSource = ICS421Channel.IcpInputCurrentSource._4mA;
    icpInputSettings.InputBiasing = ICS421Channel.InputBiasing.SingleEnded;

    channelSettings.Settings = Setting.ConvertFrom(icpInputSettings);
    httpConnection.Put(EndPoints.ItemSettings, channelSettings, HttpParameter.ItemId(item.ItemId));
}

// Now sync the updated settings with the Hardware.
httpConnection.Put(EndPoints.SystemSettingsApply);

Console.WriteLine(string.Empty);
Console.WriteLine("Thats it, press any key to exit.");
Console.ReadKey();
Environment.Exit(0);

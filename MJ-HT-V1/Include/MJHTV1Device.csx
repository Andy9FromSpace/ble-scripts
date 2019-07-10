#r "C:\Program Files (x86)\Windows Kits\10\UnionMetadata\10.0.17763.0\Windows.winmd"

#load "..\..\Include\Ble.csx"

using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.Storage.Streams;

struct MJHTV1Status
{
    internal byte BatteryLevel { get; set; }
    internal string FirmwareVersion { get; set; }
}

struct MJHTV1SensorData
{
    internal float Temperature { get; set; }
    internal float AirHumidity { get; set; }
}

async Task<MJHTV1Status> GetMJHTV1StatusAsync(string deviceId)
{
    var device = await ConnectToDeviceAsync(deviceId);
 
	var batteryService = await GetServiceAsync(device, new Guid("0000180f-0000-1000-8000-00805f9b34fb"));
    var batteryCharacteristic = await GetCharacteristicAsync(batteryService, new Guid("00002a19-0000-1000-8000-00805f9b34fb"));
    var battery = await ReadCharacteristicAsync(batteryCharacteristic);

	var infoService = await GetServiceAsync(device, new Guid("0000180a-0000-1000-8000-00805f9b34fb"));
    var infoCharacteristic = await GetCharacteristicAsync(infoService, new Guid("00002a26-0000-1000-8000-00805f9b34fb"));
	var firmwareVersion = await ReadCharacteristicAsync(infoCharacteristic);

    return new MJHTV1Status()
    {
        BatteryLevel = battery[0],
        FirmwareVersion = Encoding.ASCII.GetString(firmwareVersion).TrimEnd(new char[] { (char)0 })
    };
}

async Task<MJHTV1SensorData> GetMJHTV1SensorDataAsync(string deviceId)
{
    var device = await ConnectToDeviceAsync(deviceId);
    var service = await GetServiceAsync(device, new Guid("226c0000-6476-4566-7562-66734470666d"));
    var sensorDataCharacteristic = await GetCharacteristicAsync(service, new Guid("226caa55-6476-4566-7562-66734470666d"));

	var manualResetEvent = new ManualResetEvent(false);
	var mjhtv1SensorData = new MJHTV1SensorData();
	
	await RegisterCharacteristicNotificationHandler(sensorDataCharacteristic, (sender, args) => 
	{
		var reader = DataReader.FromBuffer(args.CharacteristicValue);
		byte[] data = new byte[reader.UnconsumedBufferLength];
		reader.ReadBytes(data);
		var dataString = Encoding.ASCII.GetString(data).TrimEnd(new char[] { (char)0 });
		var match = Regex.Match(dataString, @"T=([\d\.]*)\s+?H=([\d\.]*)");
		if (match.Success)
		{
			mjhtv1SensorData.Temperature = float.Parse(match.Groups[1].Captures[0].Value);
			mjhtv1SensorData.AirHumidity = float.Parse(match.Groups[2].Captures[0].Value);
		}

		manualResetEvent.Set();
	});
	
	manualResetEvent.WaitOne();
	
	return mjhtv1SensorData;
}

// Copyright (c) 2025 HonuaIO
// Sample IoT device simulator for testing Azure IoT Hub integration
// Install: dotnet add package Microsoft.Azure.Devices.Client

using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;

namespace Honua.Examples.AzureIoT;

/// <summary>
/// Simulates IoT devices sending telemetry to Azure IoT Hub
/// </summary>
public class DeviceSimulator
{
    private readonly string _deviceId;
    private readonly DeviceClient _deviceClient;
    private readonly Random _random = new();

    public DeviceSimulator(string deviceId, string iotHubConnectionString)
    {
        _deviceId = deviceId;
        _deviceClient = DeviceClient.CreateFromConnectionString(
            iotHubConnectionString,
            TransportType.Mqtt);
    }

    /// <summary>
    /// Start sending temperature/humidity telemetry
    /// </summary>
    public async Task RunTemperatureSensorAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"Starting temperature sensor simulation for device: {_deviceId}");

        while (!ct.IsCancellationRequested)
        {
            var temperature = 20 + _random.NextDouble() * 10; // 20-30°C
            var humidity = 40 + _random.NextDouble() * 40;    // 40-80%

            var telemetry = new
            {
                temperature,
                humidity,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            var messageBody = JsonSerializer.Serialize(telemetry);
            var message = new Message(Encoding.UTF8.GetBytes(messageBody))
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8"
            };

            // Add custom properties for tenant mapping
            message.Properties.Add("tenantId", "acme-corp");
            message.Properties.Add("sensorType", "environmental");

            await _deviceClient.SendEventAsync(message, ct);

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sent: Temp={temperature:F1}°C, Humidity={humidity:F1}%");

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    /// <summary>
    /// Start sending pressure telemetry
    /// </summary>
    public async Task RunPressureSensorAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"Starting pressure sensor simulation for device: {_deviceId}");

        while (!ct.IsCancellationRequested)
        {
            var pressure = 1000 + _random.NextDouble() * 50; // 1000-1050 hPa

            var telemetry = new
            {
                pressure,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            var messageBody = JsonSerializer.Serialize(telemetry);
            var message = new Message(Encoding.UTF8.GetBytes(messageBody))
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8"
            };

            await _deviceClient.SendEventAsync(message, ct);

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sent: Pressure={pressure:F1} hPa");

            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        }
    }

    /// <summary>
    /// Start sending smart meter telemetry
    /// </summary>
    public async Task RunSmartMeterAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"Starting smart meter simulation for device: {_deviceId}");
        double totalEnergy = 0;

        while (!ct.IsCancellationRequested)
        {
            var power = 100 + _random.NextDouble() * 400; // 100-500W
            var energyIncrement = power / 3600000; // Convert to kWh per second
            totalEnergy += energyIncrement;

            var telemetry = new
            {
                power,
                energy = totalEnergy,
                voltage = 230 + _random.NextDouble() * 10, // 230-240V
                current = power / 230,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            var messageBody = JsonSerializer.Serialize(telemetry);
            var message = new Message(Encoding.UTF8.GetBytes(messageBody))
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8"
            };

            await _deviceClient.SendEventAsync(message, ct);

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sent: Power={power:F0}W, Energy={totalEnergy:F3}kWh");

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }

    /// <summary>
    /// Start sending water quality telemetry
    /// </summary>
    public async Task RunWaterQualitySensorAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"Starting water quality sensor simulation for device: {_deviceId}");

        while (!ct.IsCancellationRequested)
        {
            var telemetry = new
            {
                ph = 6.5 + _random.NextDouble() * 2, // 6.5-8.5 pH
                turbidity = _random.NextDouble() * 5, // 0-5 NTU
                dissolvedOxygen = 5 + _random.NextDouble() * 5, // 5-10 mg/L
                temperature = 15 + _random.NextDouble() * 10, // 15-25°C
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            var messageBody = JsonSerializer.Serialize(telemetry);
            var message = new Message(Encoding.UTF8.GetBytes(messageBody))
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8"
            };

            message.Properties.Add("location", "river-station-1");

            await _deviceClient.SendEventAsync(message, ct);

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sent: pH={telemetry.ph:F2}, DO={telemetry.dissolvedOxygen:F1}mg/L");

            await Task.Delay(TimeSpan.FromSeconds(15), ct);
        }
    }

    public async Task CloseAsync()
    {
        await _deviceClient.CloseAsync();
        _deviceClient.Dispose();
    }
}

// Example usage
public class Program
{
    public static async Task Main(string[] args)
    {
        // Get connection string from environment or args
        var connectionString = Environment.GetEnvironmentVariable("DEVICE_CONNECTION_STRING")
            ?? "HostName=your-hub.azure-devices.net;DeviceId=temp-sensor-001;SharedAccessKey=...";

        var deviceId = "temp-sensor-001";

        var simulator = new DeviceSimulator(deviceId, connectionString);

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            // Run temperature sensor
            await simulator.RunTemperatureSensorAsync(cts.Token);
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Simulation stopped.");
        }
        finally
        {
            await simulator.CloseAsync();
        }
    }
}

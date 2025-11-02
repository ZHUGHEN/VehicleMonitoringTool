using System;
using System.Threading;
using System.Threading.Tasks;
using CarTelemetry.Core.Obd;

namespace Elm327.Tester;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚗 ELM327 OBD-II Adapter Tester");
        Console.WriteLine("================================");
        Console.WriteLine();

        // Get COM port from user
        Console.Write("Enter COM port (e.g., COM3): ");
        var comPort = Console.ReadLine();
        
        if (string.IsNullOrWhiteSpace(comPort))
        {
            Console.WriteLine("❌ Invalid COM port. Exiting...");
            return;
        }

        // Create and connect to adapter
        Console.WriteLine($"🔌 Connecting to {comPort}...");
        
        try
        {
            var adapter = new Elm327Adapter(comPort);
            await adapter.ConnectAsync(CancellationToken.None);
            Console.WriteLine("✅ Connected successfully!");
            Console.WriteLine();

            // Run interactive command loop
            await RunCommandLoop(adapter);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Connection failed: {ex.Message}");
            Console.WriteLine("💡 Make sure:");
            Console.WriteLine("   - OBD cable is plugged into vehicle");
            Console.WriteLine("   - Vehicle ignition is ON");
            Console.WriteLine("   - Correct COM port selected");
            Console.WriteLine("   - No other programs using the port");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static async Task RunCommandLoop(IObdAdapter adapter)
    {
        Console.WriteLine("📡 OBD-II Command Tester");
        Console.WriteLine("------------------------");
        Console.WriteLine("💡 Useful commands to try:");
        Console.WriteLine("   ATZ     - Reset adapter");
        Console.WriteLine("   ATE0    - Turn echo off");
        Console.WriteLine("   ATI     - Adapter info");
        Console.WriteLine("   0100    - Supported PIDs");
        Console.WriteLine("   010C    - Engine RPM");
        Console.WriteLine("   010D    - Vehicle speed");
        Console.WriteLine("   0105    - Coolant temperature");
        Console.WriteLine("   03      - Read stored DTCs");
        Console.WriteLine("   07      - Read pending DTCs");
        Console.WriteLine("   0A      - Read permanent DTCs");
        Console.WriteLine("   04      - Clear DTCs");
        Console.WriteLine("   exit    - Quit");
        Console.WriteLine();

        while (true)
        {
            Console.Write("OBD> ");
            var command = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrWhiteSpace(command))
                continue;
                
            if (command.ToLower() == "exit")
            {
                Console.WriteLine("👋 Goodbye!");
                break;
            }

            try
            {
                Console.Write("⏳ Sending... ");
                var response = await adapter.SendRawAsync(command, CancellationToken.None);
                Console.WriteLine($"✅ Response: {response}");
                
                // Try to interpret common responses
                InterpretResponse(command, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
            
            Console.WriteLine();
        }
    }

    static void InterpretResponse(string command, string response)
    {
        var cmd = command.ToUpper().Trim();
        var resp = response.ToUpper().Trim();

        switch (cmd)
        {
            case "010C": // RPM
                if (TryParseRpm(resp, out var rpm))
                    Console.WriteLine($"   🔧 Engine RPM: {rpm:F0}");
                break;
                
            case "010D": // Speed
                if (TryParseSpeed(resp, out var speed))
                    Console.WriteLine($"   🏃 Vehicle Speed: {speed} km/h ({speed * 0.621371:F1} mph)");
                break;
                
            case "0105": // Coolant temp
                if (TryParseCoolantTemp(resp, out var temp))
                    Console.WriteLine($"   🌡️ Coolant Temperature: {temp}°C ({temp * 9/5 + 32:F1}°F)");
                break;
                
            case "03":
            case "07": 
            case "0A": // DTC commands
                if (resp.Contains("43") || resp.Contains("47") || resp.Contains("4A"))
                    Console.WriteLine($"   🔍 DTCs found in response - use your main app to decode!");
                else if (resp.Contains("NO DATA"))
                    Console.WriteLine($"   ✅ No DTCs found");
                break;
        }
    }

    static bool TryParseRpm(string response, out double rpm)
    {
        rpm = 0;
        // Expected format: "41 0C XX XX" where XXXX is RPM * 4
        var parts = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 4 && parts[0] == "41" && parts[1] == "0C")
        {
            if (byte.TryParse(parts[2], System.Globalization.NumberStyles.HexNumber, null, out var high) &&
                byte.TryParse(parts[3], System.Globalization.NumberStyles.HexNumber, null, out var low))
            {
                rpm = ((high << 8) | low) / 4.0;
                return true;
            }
        }
        return false;
    }

    static bool TryParseSpeed(string response, out int speed)
    {
        speed = 0;
        // Expected format: "41 0D XX" where XX is speed in km/h
        var parts = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 && parts[0] == "41" && parts[1] == "0D")
        {
            if (byte.TryParse(parts[2], System.Globalization.NumberStyles.HexNumber, null, out var speedByte))
            {
                speed = speedByte;
                return true;
            }
        }
        return false;
    }

    static bool TryParseCoolantTemp(string response, out int temp)
    {
        temp = 0;
        // Expected format: "41 05 XX" where XX is temp + 40
        var parts = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 && parts[0] == "41" && parts[1] == "05")
        {
            if (byte.TryParse(parts[2], System.Globalization.NumberStyles.HexNumber, null, out var tempByte))
            {
                temp = tempByte - 40;
                return true;
            }
        }
        return false;
    }
}
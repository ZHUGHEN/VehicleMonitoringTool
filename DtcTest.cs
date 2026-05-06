using System;

namespace CarTelemetry.Core.Obd
{
    class DtcTest
    {
        static void Main()
        {
            TestDtcDecoding("47 04 20 00 00 00", "P0420 (original)");
            TestDtcDecoding("47 04 20", "P0420 (simplified)"); 
            TestDtcDecoding("4A 04 42 00 00 00", "P0442 (original)");
            TestDtcDecoding("4A 04 42", "P0442 (simplified)");
        }

        static void TestDtcDecoding(string raw, string description)
        {
            Console.WriteLine($"\n=== Testing: {description} ===");
            Console.WriteLine($"Raw input: '{raw}'");
            
            var hex = CleanToHex(raw);
            Console.WriteLine($"Cleaned hex: '{hex}'");
            
            var hdrIdx = hex.IndexOf("43", StringComparison.OrdinalIgnoreCase);
            if (hdrIdx < 0) hdrIdx = hex.IndexOf("47", StringComparison.OrdinalIgnoreCase);
            if (hdrIdx < 0) hdrIdx = hex.IndexOf("4A", StringComparison.OrdinalIgnoreCase);
            
            Console.WriteLine($"Header found at index: {hdrIdx}");
            
            int pos = hdrIdx + 2;
            Console.WriteLine($"Position after header: {pos}");
            
            var remainingLength = hex.Length - pos;
            Console.WriteLine($"Remaining length: {remainingLength}");
            Console.WriteLine($"Remaining length % 4: {remainingLength % 4}");
            
            if ((remainingLength % 4) != 0 && remainingLength >= 2)
            {
                Console.WriteLine("Skipping 2 chars (1 byte)");
                pos += 2;
            }
            
            Console.WriteLine($"Final position: {pos}");
            
            while (pos + 4 <= hex.Length)
            {
                var aHex = hex.Substring(pos, 2);
                var bHex = hex.Substring(pos + 2, 2);
                Console.WriteLine($"Reading DTC bytes: {aHex} {bHex}");
                
                var a = Convert.ToByte(aHex, 16);
                var b = Convert.ToByte(bHex, 16);
                Console.WriteLine($"Byte values: a=0x{a:X2}, b=0x{b:X2}");
                
                var (system, code) = DecodeDtc(a, b);
                Console.WriteLine($"Decoded: {code}");
                
                pos += 4;
            }
        }
        
        static (string system, string code) DecodeDtc(byte a, byte b)
        {
            string system = (a >> 6) switch { 0 => "P", 1 => "C", 2 => "B", _ => "U" };
            int d1 = (a >> 4) & 0x3;
            int d2 = a & 0xF;
            int d3 = (b >> 4) & 0xF;
            int d4 = b & 0xF;
            string code = $"{system}{d1:X}{d2:X}{d3:X}{d4:X}";
            return (system, code);
        }
        
        static string CleanToHex(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
                if (Uri.IsHexDigit(c)) sb.Append(c);
            return sb.ToString().ToUpperInvariant();
        }
    }
}


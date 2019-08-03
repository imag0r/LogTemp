using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using InfluxDB.LineProtocol.Client;
using InfluxDB.LineProtocol.Payload;

namespace LogTemp
{
    class Program
    {
        static void Main(string[] args)
        {
            var lpc = new LineProtocolClient(new Uri("http://localhost:8086"), "server_health");
            
            var measurement = GetTemperatures()
                                .Concat(GetFanSpeeds())
                                .ToDictionary(
                                    kvp => kvp.Key,
                                    kvp => kvp.Value as object
                                );

            var payload = new LineProtocolPayload();
            payload.Add(new LineProtocolPoint("health", measurement));
            lpc.WriteAsync(payload).Wait();
        }

        private static Dictionary<string, int> GetFanSpeeds()
        {
            var regex = new Regex(@"^#(\d+)\s+(\S+).+\s(\d+)%", RegexOptions.Multiline);
            return 
                regex
                    .Matches(RunHpAsmCli("show fan"))
                    .ToDictionary(
                        match => string.Format("FAN{0}_{1}", match.Groups[1].Value, match.Groups[2].Value),
                        match => int.Parse(match.Groups[3].Value)
                    );
        }
        
        private static Dictionary<string, int> GetTemperatures()
        {
            var regex = new Regex(@"^#(\d+)\s+(\S+)\s+(\d+)C", RegexOptions.Multiline);
            return
                regex
                    .Matches(RunHpAsmCli("show temp"))
                    .ToDictionary(
                        match => string.Format("{0:d2}_{1}", int.Parse(match.Groups[1].Value), match.Groups[2].Value),
                        match => int.Parse(match.Groups[3].Value)
                    );
        }

        private static string RunHpAsmCli(string command)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = @"/sbin/hpasmcli";
                process.StartInfo.ArgumentList.Add("-s");
                process.StartInfo.ArgumentList.Add(command);
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                if (!process.Start())
                {
                    return null;
                }
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output;
            }
        }
    }
}

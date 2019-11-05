﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Ptolemy.Parameters;

namespace Ptolemy.Argo.Request {
    public class ArgoRequest {
        public Guid GroupId { get; set; }
        public string HspicePath { get; set; }
        public List<string> HspiceOptions { get; set; }
        public long Seed { get; set; }
        public long Sweep { get; set; }
        public long SweepStart { get; set; }
        public decimal Temperature { get; set; }
        public TransistorPair Transistors { get; set; }
        public RangeParameter Time { get; set; }
        public List<string> IcCommands { get; set; }
        public string NetList { get; set; }
        public List<string> Includes { get; set; }
        public decimal Vdd { get; set; }
        public decimal Gnd { get; set; }
        public List<string> Signals { get; set; }
        public string ResultFile { get; set; }
        public static ArgoRequest FromJson(string json) => JsonConvert.DeserializeObject<ArgoRequest>(json);
        public string ToJson() => JsonConvert.SerializeObject(this);
        /// <summary>
        /// プロットする時間のリスト
        /// </summary>
        public List<decimal> PlotTimeList { get; set; }

        public static ArgoRequest FromFile(string path) {
            using var sr = new StreamReader(path);
            return FromJson(sr.ReadToEnd());
        }

        public string GetHashString() {
            using var sha256 = SHA256.Create();

            return string.Join("", sha256.ComputeHash(
                Encoding.UTF8.GetBytes(
                    string.Join("", new[] {$"{Transistors}", $"{Gnd}", $"{Vdd}", $"{Temperature}", NetList}
                        .Concat(IcCommands)
                        .Concat(Includes)))
            ).Select(s => $"{s:X2}"));
        }

        /// <summary>
        /// SPIスクリプトをpathに書き込む
        /// </summary>
        /// <param name="path"></param>
        public void WriteSpiScript(string path) {
            var sb = new StringBuilder();

            // Comment
            sb.AppendLine("* Generated for: HSPICE");
            sb.AppendLine("* Generated by: Ptolemy.Argo");
            sb.AppendLine($"* Target: {NetList}");

            // Parameters
            sb.AppendLine(
                $".param vtn=AGAUSS({Transistors.Vtn.Threshold},{Transistors.Vtn.Sigma},{Transistors.Vtn.Deviation}) vtp=AGAUSS({Transistors.Vtp.Threshold},{Transistors.Vtp.Sigma},{Transistors.Vtp.Deviation})");
            sb.AppendLine(".option PARHIER=LOCAL");
            sb.AppendLine($".option SEED={Seed}");
            sb.AppendLine($".temp {Temperature}");
            sb.AppendLine($".IC {string.Join(" ", IcCommands)}");
            sb.AppendLine($"VDD VDD! 0 {Vdd}V");
            sb.AppendLine($"VGND GND! 0 {Gnd}V");

            // include extra files
            foreach (var include in Includes) {
                sb.AppendLine($".include '{include}'");
            }

            // include NetList
            if (File.Exists(NetList)) sb.AppendLine($".include '{NetList}'");
            else throw new FileNotFoundException("NetListが見つかりません", NetList);

            // write .tran command
            sb.AppendLine(
                $".tran {Time.Step} {Time.Stop} start={Time.Start} uic sweep monte={Sweep} firstrun={SweepStart}");
            sb.AppendLine(".option opfile=0");

            // write .print command
            sb.AppendLine($".print {string.Join(" ", Signals.Select(x => $"V({x})"))}");
            sb.AppendLine(".end");

            // write spi script to path
            using var sw = new StreamWriter(path, false, new UTF8Encoding(false));
            sw.WriteLine(sb.ToString());
        }
    }
}

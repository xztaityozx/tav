﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Ptolemy.Argo.Request;
using Ptolemy.Config;
using Ptolemy.FilePath;
using Ptolemy.Lupus.Request;
using Ptolemy.Parameters;
using Xunit;
using YamlDotNet.Serialization;

namespace UnitTest.LibTest {
    public class ConfigTest {

        private static readonly Config Config = new Config {
            ArgoDefault = new ArgoRequest {
                GroupId = Guid.NewGuid(),
                SweepStart = 1,
                Temperature = 25,
                HspicePath = "/path/to/hspice",
                HspiceOptions = new List<string>{"Option1","Option2"},
                Seed = 1,
                Sweep = 2,
                Vtn = new Transistor(0.1,0.2,0.3),
                Vtp = new Transistor(0.4,0.5,0.6),
                Time = new Range(0.7M,0.8M,0.9M),
                BaseDirectory = "/path/to/baseDir",
                Gnd = 0.123M,
                IcCommands = new List<string> { "Command1", "Command2"},
                ModelFilePath = "/path/to/model",
                TargetCircuit = "/path/to/target",
                Vdd = 45.67M,
                Signals = new List<string>{ "A","B","C" },
                ResultFile = "/path/to/result"
            },
        };

        private readonly string yamlDoc = new Serializer().Serialize(Config);
        private readonly string jsonDoc = JsonConvert.SerializeObject(Config);

        [Fact]
        public void ConfigFileTest() {
            Assert.Equal(Path.Combine(FilePath.DotConfig, "config.yaml"), Config.ConfigFile);
            Config.ConfigFile = "/path/to/config";
            Assert.Equal("/path/to/config", Config.ConfigFile);
        }

        [Fact]
        public void LoadTest() {

            var data = new[] {
                new {throws = false, doc = jsonDoc},
                new {throws = false, doc = yamlDoc},
                new {throws = true, doc = "invalid"}
            };

            var path = Path.GetTempFileName();
            Config.ConfigFile = path;
            foreach (var d in data) {
                using (var sw = new StreamWriter(path)) {
                    sw.WriteLine(d.doc);
                }

                if (d.throws) Assert.Throws<InvalidDataContractException>(Config.Load);
                else {
                    Config.Load();
                    Assert.Equal(jsonDoc, JsonConvert.SerializeObject(Config.Instance));
                }
            }

        }
    }
}

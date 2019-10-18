﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ptolemy.Argo.Request;
using Ptolemy.Repository;
using Ptolemy.SiMetricPrefix;
using ShellProgressBar;

namespace Ptolemy.Argo {
    public class Hspice {
        private readonly string workingRoot;

        public Hspice() {
            workingRoot = Path.Combine(Path.GetTempPath(), "Ptolemy.Argo");
        }

        // TODO: Test
        public List<ResultEntity> Run(CancellationToken token, ArgoRequest request, IProgressBar bar = null) {
            var rt = new List<ResultEntity>();

            var guid = Guid.NewGuid();
            var dir = Path.Combine(workingRoot, $"{request.GroupId}");
            FilePath.FilePath.TryMakeDirectory(dir);
            Directory.SetCurrentDirectory(dir);
            var spi = Path.Combine(dir, guid + ".spi");

            request.WriteSpiScript(spi);

            using var p = new Process {
                StartInfo = new ProcessStartInfo {
                    UseShellExecute = false,
                    FileName = "bash",
                    ArgumentList =
                        {"-c", $"{request.HspicePath} {string.Join(" ", request.HspiceOptions)} -i {guid}.spi"},
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            p.Start();
            var stdout = p.StandardOutput;

            var signals = request.Signals;

            var kind = HspiceOutKind.Else;
            var sweep = request.SweepStart;
            string line;
            while ((line = stdout.ReadLine()) != null || !p.HasExited) {
                token.ThrowIfCancellationRequested();


                if (string.IsNullOrEmpty(line)) continue;
                if (line[0] == 'x') {
                    kind = HspiceOutKind.Data;
                }
                else if (line[0] == 'y') {
                    sweep++;
                    kind = HspiceOutKind.Else;
                    bar?.Tick();
                }
                else if (line[0] == 't') continue;

                if (kind == HspiceOutKind.Else) continue;
                if (!line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]
                    .TryParseDecimalWithSiPrefix(out _)) continue;


                rt.AddRange(ResultEntity.Parse(request.Seed, sweep, line, signals));
            }

            p.WaitForExit();
            File.Delete(spi);

            return rt;
        }

        private enum HspiceOutKind {
            Data,Else
        }
    }
}

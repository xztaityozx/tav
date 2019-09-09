﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kurukuru;
using Ptolemy.Argo.Request;

namespace Ptolemy.Argo {
    public class Runner {
        private readonly string command;
        private readonly string simDir, resultDir;
        private readonly string circuitDir, netlistDir, spiFile;
        private readonly ArgoRequest request;
        private readonly Guid id;
        private readonly CancellationToken token;

        public Runner(CancellationToken token,ArgoRequest request, string circuitRoot) {
            this.request = request;
            id = Guid.NewGuid();
            this.token = token;
            circuitRoot = FilePath.FilePath.Expand(circuitRoot);
            
            // create directories
            var workingRoot = Path.Combine(
                request.BaseDirectory,
                request.TargetCircuit.Replace("/", "_"),
                $"Vtn_{request.Vtn}",
                $"Vtp_{request.Vtp}"
            );
            simDir = Path.Combine(workingRoot, id.ToString());
            resultDir = Path.Combine(workingRoot, "result");
            circuitDir = Path.Combine(circuitRoot, request.TargetCircuit, "HSPICE", "nominal", "netlist");
            netlistDir = Path.Combine(workingRoot, "netlist");
            spiFile = Path.Combine(netlistDir, id + ".spi");
            command =
                $"cd {simDir} && {request.HspicePath} {string.Join(" ", request.HspiceOptions)} -i {spiFile} -o ./hspice";

        }

        /// <summary>
        /// Run command async
        /// </summary>
        /// <returns></returns>
        /// <exception cref="AggregateException"></exception>
        public async Task<Guid> RunAsync() {
            var exp = await Task.Factory.StartNew(Run, token).ContinueWith(t => t.Exception, token);
            if (exp != null) throw exp;
            
            return request.GroupId;
        }

        /// <summary>
        /// Run command sync
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgoException"></exception>
        public Guid Run() {
            try {
                BuildEnvironment();
                token.ThrowIfCancellationRequested();

                using (var exec = new Exec.Exec(token)) {
                    exec.Run(command);
                    exec.ThrowIfNonZeroExitCode();
                }
                CheckResultsFiles();
            }
            catch (ArgoException) {
                throw;
            }
            catch (Exception e) {
                throw new ArgoException("unknown error has occured\n\t-->" + e);
            }
            

            return request.GroupId;
        }

        /// <summary>
        /// Run command with shell spinner
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgoException"></exception>
        public Guid RunWithSpinner() {
                Spinner.Start("simulating...",spin => {
                    using (var exec = new Exec.Exec(token)) {
                        Run();
                    }
                });

            return request.GroupId;
        }

        private void BuildEnvironment() {
            CreateDirectories();
            CreateSymbolicLink();
            CreateDirectories();
            CreateSpiScript();
        }

        private void CreateSpiScript() {
            string baseNetList;
            using (var sr = new StreamReader(Path.Combine(netlistDir, "netlist"))) baseNetList = sr.ReadToEnd();

            if (string.IsNullOrEmpty(baseNetList)) throw new ArgoException("netlist file can not be empty");

            try {
                using (var sw = new StreamWriter(spiFile)) {
                    sw.WriteLine("* Generate for HSPICE");
                    sw.WriteLine("* Generated by Ptolemy.Argo");
                    sw.WriteLine($"* Generated at {DateTime.Now}");

                    sw.WriteLine(".option MCBRIEF=2");
                    sw.WriteLine($".param vtn=AGAUSS({request.Vtn.Threshold}, {request.Vtn.Sigma}, {request.Vtn.Deviation}) vtp=AGAUSS({request.Vtp.Threshold}, {request.Vtp.Sigma}, {request.Vtp.Deviation})");
                    sw.WriteLine(".option PARHIER = LOCAL");
                    sw.WriteLine($".option SEED = {request.Seed}");
                    sw.WriteLine($"VDD VDD! 0 0 {request.Vdd}");
                    sw.WriteLine($"VGND GND! 0 0 {request.Gnd}");
                    sw.WriteLine($".IC {string.Join(" ", request.IcCommands)}");
                    sw.WriteLine(".option ARTIST=2 PSF=2");
                    sw.WriteLine($".temp {request.Temperature}");
                    sw.WriteLine($".include {request.ModelFilePath}");
                    
                    sw.WriteLine(baseNetList);
                    
                    sw.WriteLine(
                        $".tran {request.Time.Step:E} {request.Time.Stop:E} start={request.Time.Start:E} sweep monte={request.Sweep:E} firstrun={request.SweepStart:E}");
                    sw.WriteLine(".option opfile=1 split_dp=2");
                    sw.WriteLine(".end");
                }
            }
            catch (Exception e) {
                throw new ArgoException($"Failed create spi script\n\tinnerException-->{e}");
            }

        }

        private void CreateDirectories() {
            foreach (var dir in new[]{simDir, resultDir,netlistDir}) {
                token.ThrowIfCancellationRequested();

                if(Directory.Exists(dir)) continue;
                try {
                    Directory.CreateDirectory(dir);
                }
                catch (Exception ) {
                    throw new ArgoException($"Failed create directory: {dir}");
                }
            }
        }

        private void CheckResultsFiles() {
            if(Directory.GetFiles(simDir, "*.tr0@*").Length != request.Sweep) 
                throw new ArgoException("not enough simulation result files(*.tr0@*)");
            
        }
        
        private void CreateSymbolicLink() {
            foreach (var target in new[] {"cnl", "netlist"}) {
                token.ThrowIfCancellationRequested();

                try {
                    var from = Path.Combine(circuitDir,target);
                    var to = Path.Combine(netlistDir, target);
                    
                    if(File.Exists(to)) continue;
                    if(Directory.Exists(to)) continue;
                    
                    using (var ex = new Exec.Exec(token)) {
                        var output = "";
                        ex.Run($"ln -s {from} {to}", s => output += s, true);

                        if (ex.ExitCode != 0)
                            throw new ArgoException(
                                $"Failed create symbolic link: {from} ==> {to}\n\t-->command output: {output}");
                    }
                }
                catch (Exception e) {
                    throw new ArgoException($"Failed start ln command\n\t-->{e}");
                }
            }

        }
    }
}

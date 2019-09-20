using CommandLine;
using Ptolemy.Parameters;

namespace Ptolemy.Interface {
    public static partial class OptionDefault {
        public const string SweepDefault = "1,5000", SeedDefault="1,2000";

        public static (Range sweep, Range seed) Bind(this IRangeOption @this, (Range w, Range s) config) {
            return (
                new Range(@this.Sweep, (config.w?.Start ?? 1M, config.w?.Step ?? 1M, config.w?.Stop ?? 5000M)),
                new Range(@this.Seed, (config.s?.Start ?? 1M, config.s?.Step ?? 1M, config.s?.Stop ?? 2000M))
            );
        }
    } 
    
    public interface  IRangeOption {
        [Option('w', "sweeps", Default = OptionDefault.SweepDefault,
            HelpText = "Sweepの幅を[start],[stop]もくは[start],[step],[stop]で指定します")]
        string Sweep { get; set; }

        [Option('e', "seeds", Default = OptionDefault.SeedDefault, HelpText = "Seedの幅を[start],[stop]もくは[start],[step],[stop]で指定します")]
         string Seed { get; set; }
    }
}
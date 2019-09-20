using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Ptolemy.Map;
using Ptolemy.SiMetricPrefix;

namespace Ptolemy.Repository {
    public class HspiceResultEntity {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        [Required]
        public decimal Time { get; set; }
        [Required]
        public Map.Map<string, decimal> Values { get; set; }
        
        public HspiceResultEntity(){}

        public HspiceResultEntity(string hspiceOutputLine, IEnumerable<string> keys) {
            var split = hspiceOutputLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Time = split[0].ParseDecimalWithSiPrefix();
            Values=new Map<string, decimal>();
            foreach (var d in split.Skip(1).Zip(keys, (v,k) => new{v,k})) {
                Values[d.k] = d.v.ParseDecimalWithSiPrefix();
            }
        }
    }
}
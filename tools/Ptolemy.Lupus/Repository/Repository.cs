﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using EFCore.BulkExtensions;
using Ptolemy.Map;

namespace Ptolemy.Lupus.Repository {
    public abstract class Repository<TEntity> where TEntity : class {
        public abstract void Use(string db);
        public abstract void BulkUpsert(IList<TEntity> list);
        public abstract Tuple<string, long>[] Count(Func<TEntity, bool> whereFunc, Filter filter);
    }

    public class MssqlRepository : Repository<Record.Record> {
        private string name;

        public override void Use(string db) {
            name = db;
            using (var c = new Context(name)) {
                c.Database.EnsureCreated();
            }
        }

        public override void BulkUpsert(IList<Record.Record> list) {
            using (var context = new Context(name)) {
                context.Database.EnsureCreated();
                using (var tr = context.Database.BeginTransaction()) {
                    context.BulkInsertOrUpdate(list);
                    tr.Commit();
                }
            }
        }

        public void BulkUpsertRange(IList<IList<Record.Record>> list) {
            var cnt = 0;
            using (var context = new Context(name)) {
                using (var tr = context.Database.BeginTransaction()) {
                    foreach (var records in list) {
                        context.BulkInsertOrUpdate(records);
                        Console.WriteLine($" [RepositoryInfo][{DateTime.Now}]{records.Count} records was BulkUpsert(total: {cnt+=records.Count})");
                    }

                    tr.Commit();
                }
            }
        }

        public override Tuple<string, long>[] Count(Func<Record.Record, bool> whereFunc, Filter filter) {
            using (var context = new Context(name))
                return filter.Aggregate(
                    context
                        .Records
                        .Where(whereFunc)
                        .GroupBy(r => new {r.Sweep, r.Seed})
                        .Select(g => g.ToMap(r => r.Key, r => r.Value)).ToList()
                );
        }

        /// <summary>
        /// Context class for EntityFrameworkCore
        /// </summary>
        internal class Context : DbContext {
            private readonly string name;
            public Context(string name) => this.name = name;

            public DbSet<Record.Record> Records { get; set; }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
                base.OnConfiguring(optionsBuilder);
                optionsBuilder.UseSqlServer(LupusConfig.Instance.ConnectionString + $";Database={name}");
                optionsBuilder.UseLazyLoadingProxies();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder) {
                base.OnModelCreating(modelBuilder);
                modelBuilder.Entity<Record.Record>().HasKey(e => new {e.Sweep, e.Key, e.Seed});
            }
        }
    }
}
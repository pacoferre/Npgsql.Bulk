﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Npgsql.Bulk.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

namespace Npgsql.Bulk
{
    /// <summary>
    /// Some internal helper methods for Npgsql
    /// </summary>
    internal static class NpgsqlHelper
    {
        internal static string GetQualifiedName(string name, string prefix = null)
        {
            return $"{(prefix == null ? "" : "\"" + prefix + "\".")}\"{name}\"";
        }

        internal static NpgsqlConnection GetNpgsqlConnection(DbContext context)
        {
            return (NpgsqlConnection)context.Database.GetDbConnection();
        }

        internal static IDbContextTransaction EnsureOrStartTransaction(
            DbContext context, IsolationLevel isolation)
        {
            if (context.Database.CurrentTransaction == null)
            {
                if (System.Transactions.Transaction.Current != null)
                {
                    //System.Transactions.TransactionsDatabaseFacadeExtensions.EnlistTransaction(context.Database, System.Transactions.Transaction.Current);
                    return null;
                }

                return context.Database.BeginTransaction(isolation);
            }

            return null;
        }

        internal static List<ColumnInfo> GetColumnsInfo(DbContext context, string tableName)
        {
            var sql = @"
                    SELECT column_name as ColumnName, udt_name as ColumnType, (column_default IS NOT NULL) as HasDefault 
                    FROM information_schema.columns
                    WHERE table_name = @tableName";
            var param = new NpgsqlParameter("@tableName", tableName);

            var conn = GetNpgsqlConnection(context);
            if (conn.State != ConnectionState.Open)
                conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add(param);
                var result = new List<ColumnInfo>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new ColumnInfo()
                        {
                            ColumnName = (string)reader["ColumnName"],
                            ColumnType = (string)reader["ColumnType"],
                            HasDefault = (bool)reader["HasDefault"]
                        });
                    }
                }
                return result;
            }
        }

        internal static List<MappingInfo> GetMetadata(DbContext context, Type type)
        {
            var metadata = context.Model;
            var entityType = metadata.GetEntityTypes().Single(x => x.ClrType == type);

            var tableName = GetTableName(context, type);
            var columnsInfo = GetColumnsInfo(context, tableName);
            if (entityType.BaseType != null)
            {
                var baseTableName = GetTableName(context, entityType.BaseType.ClrType);
                if (baseTableName != tableName)
                {
                    var extraColumnsInfo = GetColumnsInfo(context, baseTableName);
                    columnsInfo.AddRange(extraColumnsInfo);
                }
            }

            var innerList = entityType.GetProperties()
                .Where(x => x.PropertyInfo != null)
                .Select(x =>
                {
#if !NETSTANDARD2_1
                    var relational = x.DeclaringEntityType.Relational();
#endif
                    ValueGenerator localGenerator = null;

                    var generatorFactory = x.GetAnnotations().FirstOrDefault(a => a.Name == "ValueGeneratorFactory");
                    if (generatorFactory != null)
                    {
                        var valueGeneratorAccessor = generatorFactory.Value as Func<IProperty, IEntityType, ValueGenerator>;
                        localGenerator = valueGeneratorAccessor(x, x.DeclaringEntityType);
                    }

                    return new MappingInfo()
                    {
#if NETSTANDARD2_1
                        TableName = x.DeclaringEntityType.GetTableName(),
                        TableNameQualified = NpgsqlHelper.GetQualifiedName(x.DeclaringEntityType.GetTableName(), x.DeclaringEntityType.GetSchema()),
                        ColumnInfo = columnsInfo.First(c => c.ColumnName == x.GetColumnName()),
#else
                        TableName = relational.TableName,
                        TableNameQualified = NpgsqlHelper.GetQualifiedName(relational.TableName, relational.Schema),
                        ColumnInfo = columnsInfo.First(c => c.ColumnName == x.Relational().ColumnName),
#endif
                        Property = x.PropertyInfo,
                        IsDbGenerated = x.ValueGenerated != ValueGenerated.Never && localGenerator == null,
                        LocalGenerator = localGenerator,
                        IsKey = x.IsKey(),
                        IsInheritanceUsed = entityType.BaseType != null
                    };
                }).ToList();

            return innerList;
        }

        internal static string GetTableName(DbContext context, Type t)
        {
#if NETSTANDARD2_1
            var entityType = context.Model.FindEntityType(t);
            return entityType.GetTableName();
#else
            var relational = context.Model.FindEntityType(t).Relational();
            return relational.TableName;
#endif
        }

        internal static string GetTableNameQualified(DbContext context, Type t)
        {
#if NETSTANDARD2_1
            var entityType = context.Model.FindEntityType(t);
            return GetQualifiedName(entityType.GetTableName(), entityType.GetSchema());
#else
            var relational = context.Model.FindEntityType(t).Relational();
            return GetQualifiedName(relational.TableName, relational.Schema);
#endif
        }
    }
}

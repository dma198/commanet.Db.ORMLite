using System;
using System.Collections.Generic;
using System.Reflection;
using System.Data;
using System.Text.RegularExpressions;

using NLog;

namespace commanet.Db
{
    public class ORMDBSchemaGenerator
    {
        private readonly Logger? Logger;
        private readonly SQLDBConnection db;
        public ORMDBSchemaGenerator(SQLDBConnection con, Logger? log)
        {
            Logger = log;
            db = con;
        }

        private readonly List<Type> tableClasses = new List<Type>();
        public void AddTableClass<T>()
        {
            tableClasses.Add(typeof(T));
        }

        public string PostGenerationScript { get; set; } = "";

        private readonly List<string> ExistedTables = new List<string>();

        private static readonly Regex inlineCommentsRgx = new Regex(@"--[^-\n]*");

        public void GenerateSchema(string SchemaName)
        {
            Logger?.Info("Database Schema check/generation started");

            var schema = db.GetSchema("Tables");

            foreach (DataRow? row in schema.Rows)
            {
                if (row == null) continue;
                var dbName = row[0]?.ToString();
                var schName = row[1].ToString();
                var tblName = row[2].ToString();
                if (schName == SchemaName && tblName != null)
                {
                    ExistedTables.Add(tblName);
                }
            }

            foreach (var c in tableClasses)
            {
                if (!c.IsClass) continue;
                var orm = new ORMLite(db);
                CreateTable(c);
            }


            var sqls = PostGenerationScript.Split(";");
            foreach (var sql in sqls)
            {
                var lsql = inlineCommentsRgx.Replace(sql, "");
                if (!string.IsNullOrEmpty(lsql))
                {
                    try
                    {
                        db.Transaction((th) => th.ExecuteNonQuery(lsql));
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception ex)
                    {
                        Logger?.Error(ex, $"DBSchemaGenerator. Operation: {lsql.Trim()} ");
                    }
#pragma warning restore CA1031 // Do not catch general exception types
                }
            }
            Logger?.Info("Database Schema check/generation done");
        }

        private void CreateTable(Type typ)
        {
            var nattr = typ.GetCustomAttribute<TableNameAttribute>();
            var tname = "";
            if (nattr != null) tname = nattr.TableName;
            else tname = ORMLite.ConvertNameToDB(typ.Name);

            if (ExistedTables.Exists((n) => n == tname)) return;

            Logger?.Info($"Table {tname} not found in database. Creating...");

            var colCnt = 0;
            var SQL = "CREATE TABLE " + tname + "(";

            #region Reflection returns properties of parent class after child. Here we reverse it.
            List<KeyValuePair<Type, List<PropertyInfo>>> pTypes = new List<KeyValuePair<Type, List<PropertyInfo>>>();
            foreach (var p in typ.GetProperties())
            {
                KeyValuePair<Type, List<PropertyInfo>> pType;
                if (!pTypes.Exists((pp) => pp.Key == p.DeclaringType) && p.DeclaringType != null)
                {
                    pType = new KeyValuePair<Type, List<PropertyInfo>>(p.DeclaringType, new List<PropertyInfo>());
                    pTypes.Add(pType);
                }
                else pType = pTypes.Find((pp) => pp.Key == p.DeclaringType);
                pType.Value.Add(p);
            }
            pTypes.Reverse();
            #endregion

            foreach (var pType in pTypes)
            {
                foreach (var p in pType.Value)
                {

                    var cName = ORMLite.ConvertNameToDB(p.Name);
                    var aColDef = p.GetCustomAttribute<ColumnDefAttribute>();
                    var colDef = "";
                    if (aColDef != null) colDef = aColDef.ColumnDef;
                    else continue;
                    SQL += cName + " " + colDef + ",";
                    colCnt++;
                }
            }


            SQL = SQL.Trim(',') + ")";
            if (colCnt == 0) throw new Exception(
                $"No any DB column definitions found for class {typ.Name}");
            db.Transaction((th) => {
                th.ExecuteNonQuery(SQL);
                var atPostCreate = typ.GetCustomAttribute<PostTableCreateScriptAttribute>();
                if (atPostCreate != null)
                {
                    var sqls = atPostCreate.Script.Split(";");
                    foreach (var sql in sqls)
                    {
                        if (!string.IsNullOrEmpty(sql?.Trim()))
                        {
                            var lsql = sql.Replace("{TNAME}", tname, StringComparison.InvariantCultureIgnoreCase);
                            th.ExecuteNonQuery(lsql);
                        }
                    }
                }
                Logger?.Info($"Created table {tname}");
            },
            IsolationLevel.ReadCommitted,
            true // Perform DDL out of transaction 
            );

        }
    }
}

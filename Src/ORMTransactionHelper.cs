using System;
using System.Collections.Generic;
using System.Reflection;

namespace commanet.Db
{
    public class ORMTransactionHelper
    {
        //private readonly SQLDBConnection connection;
        private readonly TransactionHelper mth;

        private static object GetPropValue(PropertyInfo prp, object instance)
        {
            var v = prp.GetValue(instance) ??
                        new Exception($"Property {prp.Name} not found in type {instance.GetType().Name}");
            if (prp.PropertyType == typeof(bool))
                v = (bool?)v == true ? 1 : 0;
            return v;
        }

        public ORMTransactionHelper(TransactionHelper mth)
        {
            this.mth = mth;
        }

        public int Delete(string TableName, params KeyValuePair<string, object>[] parameters)
        {
            var SQL = "DELETE FROM " + TableName;
            if (parameters != null && parameters.Length > 0)
            {
                SQL += " WHERE ";
                foreach (var kv in parameters)
                {
                    SQL += kv.Key + "=:" + kv.Key + " AND ";
                }
                SQL = SQL.Remove(SQL.Length - 5);
            }
            return parameters == null ? mth.ExecuteNonQuery(SQL)
                                      : mth.ExecuteNonQuery(SQL, parameters);
        }

        public int Delete<T>(T dbobj, string TableName, params KeyValuePair<string, object>[] parameters)
            where T : class
        {
            if (dbobj == null)
                throw new ArgumentNullException(nameof(dbobj));

            var SQL = "DELETE FROM " + TableName;
            var SqlParams = new List<KeyValuePair<string, object>>();
            if (parameters != null && parameters.Length > 0)
            {
                var lkeys = (string[])parameters.Clone();
                for (int i = 0; i < lkeys.Length; i++) lkeys[i] = lkeys[i].ToUpperInvariant();
                SQL += " WHERE ";
                var type = dbobj.GetType();
                var properties = type.GetProperties();
                foreach (var prp in properties)
                {
                    if (prp.GetCustomAttribute<PDBIgnoreAttribute>() != null) continue;
                    if (ORMLite.IsSupportedProperyType(prp))
                    {
                        var fname = prp.Name;
                        var idx = Array.IndexOf(parameters, fname.ToUpperInvariant());
                        if (idx < 0)
                        {
                            var fname2 = ORMLite.ConvertNameToDB(fname);
                            idx = Array.IndexOf(lkeys, fname2.ToUpperInvariant());
                            if (idx >= 0) fname = fname2;
                        }
                        if (idx >= 0)
                        {
                            SQL += fname + "=:" + fname + " AND ";
                            SqlParams.Add(new KeyValuePair<string, object>(fname, GetPropValue(prp, dbobj)));
                        }
                    }
                }
                SQL = SQL.Remove(SQL.Length - 5);
            }

            var res = mth.ExecuteNonQuery(SQL, SqlParams.ToArray());
            return res;
        }

        public void Insert<T>(T dbobj, string TableName, bool ConvertFieldNames = true, params KeyValuePair<string, object>[] parameters)
            where T : class
        {
            if (dbobj == null)
                throw new ArgumentNullException(nameof(dbobj));

            var SQL = "INSERT INTO " + TableName + "(";
            var SQLV = "VALUES(";
            var type = dbobj.GetType();
            var properties = type.GetProperties();
            var SqlParams = new List<KeyValuePair<string, object>>();
            foreach (var prp in properties)
            {
                if (prp.GetCustomAttribute<PDBIgnoreAttribute>() != null) continue;
                if (ORMLite.IsSupportedProperyType(prp))
                {
                    var fname = prp.Name;
                    if (ConvertFieldNames)
                        fname = ORMLite.ConvertNameToDB(fname);
                    var value = GetPropValue(prp, dbobj);
                    SQL += fname + ",";
                    SQLV += ":" + fname + ",";
                    SqlParams.Add(new KeyValuePair<string, object>(fname, value));
                }
            }
            if (parameters != null)
            {
                foreach (var k in parameters)
                {
                    SQL += k.Key + ",";
                    SQLV += ":" + k.Key + ",";
                    SqlParams.Add(new KeyValuePair<string, object>(k.Key, k.Value));
                }
            }
            SQL = SQL.TrimEnd(',');
            SQLV = SQLV.TrimEnd(',');
            SQL += ")" + SQLV + ")";

            mth.ExecuteNonQuery(SQL, SqlParams.ToArray());
        }

        public int Update<T>(T dbobj, string TableName, bool ConvertFieldNames = true, params string[] keys)
        where T : class
        {
            if (dbobj == null)
                throw new ArgumentNullException(nameof(dbobj));
            var lkeys = (string[])Array.CreateInstance(typeof(string), 0);
            if (keys != null)
                lkeys = (string[])keys.Clone();

            for (int i = 0; i < lkeys.Length; i++) lkeys[i] = lkeys[i].ToUpperInvariant();
            var SQL = "UPDATE " + TableName + " SET ";
            var SQLW = " WHERE ";
            var type = dbobj.GetType();
            var properties = type.GetProperties();
            var keyscnt = 0;
            var SqlParams = new List<KeyValuePair<string, object>>();
            var SqlParamsW = new List<KeyValuePair<string, object>>();
            foreach (var prp in properties)
            {
                if (prp.GetCustomAttribute<PDBIgnoreAttribute>() != null) continue;
                if (ORMLite.IsSupportedProperyType(prp))
                {
                    var fname = prp.Name;
                    if (ConvertFieldNames)
                        fname = ORMLite.ConvertNameToDB(fname);
                    var value = GetPropValue(prp, dbobj);
                    var idx = keys == null ? -1
                                           : Array.IndexOf(keys, fname.ToUpperInvariant());
                    if (idx < 0)
                    {
                        idx = Array.IndexOf(lkeys, fname.ToUpperInvariant());
                    }
                    if (idx < 0)
                    {
                        SQL += (fname + " = :" + fname + ",");
                        SqlParams.Add(new KeyValuePair<string, object>(fname, GetPropValue(prp, dbobj)));
                    }

                    if (idx >= 0)
                    {
                        keyscnt++;
                        SQLW += (fname + " = :" + fname + " AND ");
                        SqlParamsW.Add(new KeyValuePair<string, object>(fname, value));
                    }
                }
            }
            SQL = SQL.TrimEnd(',');
            SqlParams.AddRange(SqlParamsW);
            if (keyscnt > 0)
                SQL += SQLW.Remove(SQLW.Length - 5);// Remove trailing ' AND '
            var res = mth.ExecuteNonQuery(SQL, SqlParams.ToArray());
            return res;
        }

        public int Update<T>(T dbobj, string TableName, params string[] keys)
        where T : class
        {
            return Update(dbobj, TableName, true, keys);
        }

        public bool UpdateOrInsert<T>(T dbobj, string TableName, bool ConvertFieldNames = true, params string[] keys)
        where T : class
        {
            var res = Update(dbobj, TableName, ConvertFieldNames, keys);
            if (res == 0)
            {
                Insert(dbobj, TableName, ConvertFieldNames);
                return false;
            }
            return true;
        }

    }

}

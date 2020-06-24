using System;
using System.Reflection;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace commanet.Db
{
    public class ORMLite
    {
        private readonly SQLDBConnection con;
        public ORMLite(SQLDBConnection con)
        {
            this.con = con;
        }

        public static bool IsSupportedProperyType(PropertyInfo prp)
        {
            if(prp==null)
            {
                throw new ArgumentNullException(nameof(prp));
            }
            return ((!prp.PropertyType.IsClass && !prp.PropertyType.IsArray) ||
                    prp.PropertyType == typeof(string) || prp.PropertyType == typeof(DateTime));
        }

        public static string ConvertNameToDB(string CSharpName)
        {
            if (CSharpName == null)
            {
                throw new ArgumentNullException(nameof(CSharpName));
            }

            var DBName = "";
            for (int i = 0; i < CSharpName.Length; i++)
            {
                var c = CSharpName[i];
                if (i > 0 && char.IsUpper(c)) DBName += "_" + char.ToUpperInvariant(c);
                else DBName += char.ToUpperInvariant(c);
            }
            return DBName;
        }

        private static void SetPropertyValue(object obj, PropertyInfo prp, IDataReader rd, int fldIdx)
        {
            if (rd.IsDBNull(fldIdx))
            {
                if (!(prp.PropertyType.IsGenericType && prp.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)))
                    return;
                else
                {
                    prp.SetValue(obj, null);
                    return;
                }
            }

            if (prp.PropertyType.IsEnum)
            {
                var v = rd.GetValue(fldIdx);
                if (v != null)
                {
                    var data = v.ToString() ?? "";
                    prp.SetValue(obj, Enum.ToObject(prp.PropertyType, int.Parse(data,CultureInfo.InvariantCulture.NumberFormat)));
                }
            }
            else if (!rd.IsDBNull(fldIdx))
                prp.SetValue(obj, Convert.ChangeType(rd.GetValue(fldIdx), prp.PropertyType, CultureInfo.InvariantCulture));
            else
                prp.SetValue(obj, "");
        }

        public List<T> SelectAll<T>(string SQL, bool RequiredToFillAllFields = false, params KeyValuePair<string,object>[] parameters)
            where T : class, new()
        {
            var res = new List<T>();
            con.ExecuteReader(SQL, (rd) => {
                var colNames = new List<string>();
                var type = typeof(T);
                var properties = type.GetProperties();
                if (colNames.Count == 0)
                    for (int i = 0; i < rd.FieldCount; i++)
                        colNames.Add(rd.GetName(i).ToUpperInvariant());
                T obj = new T();
                foreach (var prp in properties)
                {
                    if (IsSupportedProperyType(prp))
                    {
                        var idx = colNames.IndexOf(prp.Name.ToUpperInvariant());
                        if (idx < 0)
                        {
                            idx = colNames.IndexOf(ConvertNameToDB(prp.Name));
                        }
                        if (idx < 0 && RequiredToFillAllFields)
                            throw new Exception("Property <" + prp.Name + "> not found in SQL Query result");
                        if (idx >= 0)
                        {
                            SetPropertyValue(obj, prp, rd, idx);
                        }
                    }
                }
                res.Add(obj);
                return true;
            }, parameters);
            return res;
        }

        public List<T> GetAll<T>(string TableName, bool ConvertFieldNames=true, string? OrderBy=null, params KeyValuePair<string, object>[] parameters)
        where T : class, new()
        {
            var SQL = "SELECT ";
            var type = typeof(T);
            var properties = type.GetProperties();
            foreach (var prp in properties)
            {
                if (prp.GetCustomAttribute<PDBIgnoreAttribute>() != null) continue;
                if (IsSupportedProperyType(prp))
                {
                    var fname = prp.Name;
                    if (ConvertFieldNames) fname = ConvertNameToDB(fname);
                    SQL += fname + ",";
                }
            }
            SQL = SQL.TrimEnd(',');
            SQL += " FROM " + TableName;
            if (parameters != null && parameters.Length > 0)
            {
                SQL += " WHERE ";
                foreach (var k in parameters)
                {
                    SQL += k.Key + "= :" + k.Key + " AND ";
                }
                SQL = SQL.Remove(SQL.Length - 5);
            }
            if (OrderBy != null) SQL += " ORDER BY " + OrderBy;
            return parameters == null ? SelectAll<T>(SQL, false)
                                      : SelectAll<T>(SQL, false, parameters);
        }

        public T? SelectOne<T>(string SQL, bool RequiredToFillAllFields = true, params KeyValuePair<string, object>[] parameters)
            where T : class, new()
        {
            T? res = null;

            con.ExecuteReader(SQL, (rd) =>
            {
                var colNames = new List<string>();
                var type = typeof(T);
                var properties = type.GetProperties();
                res = new T();
                if (colNames.Count == 0)
                    for (int i = 0; i < rd.FieldCount; i++)
                        colNames.Add(rd.GetName(i).ToUpperInvariant());
                foreach (var prp in properties)
                {
                    if (IsSupportedProperyType(prp))
                    {
                        var idx = colNames.IndexOf(prp.Name.ToUpperInvariant());
                        if (idx < 0)
                        {
                            idx = colNames.IndexOf(ConvertNameToDB(prp.Name));
                        }
                        if (idx < 0 && RequiredToFillAllFields)
                            throw new Exception("Property <" + prp.Name + "> not found in SQL Query result");
                        if (idx >= 0)
                            SetPropertyValue(res, prp, rd, idx);
                    }
                }

                return true;
            }, parameters);

            return res;
        }

        public void Transaction(Action<ORMTransactionHelper> ac)
        {
            con.Transaction((tr) =>
            {
                try
                {
                    var th = new ORMTransactionHelper(tr);
                    ac(th);
                }
                catch (Exception)
                {
                    throw;
                }
            });
        }

    }
}

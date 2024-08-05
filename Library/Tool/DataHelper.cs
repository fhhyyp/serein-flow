using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Tool
{
    public static class DataHelper
    {
        /// <summary>
        /// 把Object转换为Json字符串
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string ToJson(this object obj)
        {
            IsoDateTimeConverter val = new IsoDateTimeConverter();
            val.DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
            IsoDateTimeConverter val2 = val;
            return JsonConvert.SerializeObject(obj, (JsonConverter[])(object)new JsonConverter[1] { (JsonConverter)val2 });
        }



        /// <summary>
        /// 把Json文本转为实体
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <returns></returns>
        public static T FromJSON<T>(this string input)
        {
            try
            {
                if (typeof(T).IsAssignableFrom(typeof(T)))
                {

                }
                return JsonConvert.DeserializeObject<T>(input);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                // return default(T);
                return default;
            }
        }

        public static List<T> IListToList<T>(IList list)
        {
            T[] array = new T[list.Count];
            list.CopyTo(array, 0);
            return new List<T>(array);
        }

        public static DataTable GetNewDataTable(DataTable dt, string condition)
        {
            if (!IsExistRows(dt))
            {
                if (condition.Trim() == "")
                {
                    return dt;
                }

                DataTable dataTable = new DataTable();
                dataTable = dt.Clone();
                DataRow[] array = dt.Select(condition);
                for (int i = 0; i < array.Length; i++)
                {
                    dataTable.ImportRow(array[i]);
                }

                return dataTable;
            }

            return null;
        }

        public static bool IsExistRows(DataTable dt)
        {
            if (dt != null && dt.Rows.Count > 0)
            {
                return false;
            }

            return true;
        }

        public static Hashtable DataTableToHashtable(DataTable dt)
        {
            Hashtable hashtable = new Hashtable();
            foreach (DataRow row in dt.Rows)
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    string columnName = dt.Columns[i].ColumnName;
                    hashtable[columnName] = row[columnName];
                }
            }

            return hashtable;
        }

        public static DataTable ListToDataTable<T>(List<T> entitys)
        {
            if (entitys == null || entitys.Count < 1)
            {
                return null;
            }

            Type type = entitys[0].GetType();
            PropertyInfo[] properties = type.GetProperties();
            DataTable dataTable = new DataTable();
            for (int i = 0; i < properties.Length; i++)
            {
                dataTable.Columns.Add(properties[i].Name);
            }

            foreach (T entity in entitys)
            {
                object obj = entity;
                if (obj.GetType() != type)
                {
                    throw new Exception("要转换的集合元素类型不一致");
                }

                object[] array = new object[properties.Length];
                for (int j = 0; j < properties.Length; j++)
                {
                    array[j] = properties[j].GetValue(obj, null);
                }

                dataTable.Rows.Add(array);
            }

            return dataTable;
        }

        public static string DataTableToXML(DataTable dt)
        {
            if (dt != null && dt.Rows.Count > 0)
            {
                StringWriter stringWriter = new StringWriter();
                dt.WriteXml((TextWriter)stringWriter);
                return stringWriter.ToString();
            }

            return string.Empty;
        }

        public static string DataSetToXML(DataSet ds)
        {
            if (ds != null)
            {
                StringWriter stringWriter = new StringWriter();
                ds.WriteXml((TextWriter)stringWriter);
                return stringWriter.ToString();
            }

            return string.Empty;
        }
    }
}

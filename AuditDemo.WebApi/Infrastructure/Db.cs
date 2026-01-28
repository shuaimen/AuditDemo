using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace AuditDemo.WebApi.Infrastructure
{
    public static class Db
    {
        private static string ConnStr => ConfigurationManager.ConnectionStrings["Default"].ConnectionString;

        public static DataTable Query(string sql, params SqlParameter[] ps)
        {
            using (var conn = new SqlConnection(ConnStr))
            using (var cmd = new SqlCommand(sql, conn))
            {
                if (ps != null && ps.Length > 0) cmd.Parameters.AddRange(ps);
                using (var da = new SqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    da.Fill(dt);
                    return dt;
                }
            }
        }

        public static DataRow QuerySingle(string sql, params SqlParameter[] ps)
        {
            var dt = Query(sql, ps);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public static int Execute(string sql, params SqlParameter[] ps)
        {
            using (var conn = new SqlConnection(ConnStr))
            using (var cmd = new SqlCommand(sql, conn))
            {
                if (ps != null && ps.Length > 0) cmd.Parameters.AddRange(ps);
                conn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        public static object Scalar(string sql, params SqlParameter[] ps)
        {
            using (var conn = new SqlConnection(ConnStr))
            using (var cmd = new SqlCommand(sql, conn))
            {
                if (ps != null && ps.Length > 0) cmd.Parameters.AddRange(ps);
                conn.Open();
                return cmd.ExecuteScalar();
            }
        }

        public static SqlParameter P(string name, object value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        public static Guid NewId()
        {
            return Guid.NewGuid();
        }
    }
}

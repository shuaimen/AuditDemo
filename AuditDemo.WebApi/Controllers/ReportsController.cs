using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web.Http;
using AuditDemo.WebApi.Infrastructure;

namespace AuditDemo.WebApi.Controllers
{
    [RoutePrefix("api/reports")]
    public class ReportsController : ApiController
    {
        // Dashboard overview (trend / module compare / closure / certificate expiry / risk)
        [HttpGet]
        [Route("overview")]
        public IHttpActionResult Overview(int? year = null, int days = 60)
        {
            var y = year ?? DateTime.Now.Year;
            if (days <= 0) days = 60;
            if (days > 3650) days = 3650;

            var summary = GetSummary(y);
            var gradeTrend = GetGradeTrend();
            var moduleCompare = GetModuleCompare(y);
            var capa = GetCapaClosure(y);
            var cert = GetCertExpiring(days);
            var risk = GetRisk(y, days);

            return Ok(new
            {
                year = y,
                days = days,
                summary = summary,
                gradeTrend = gradeTrend,
                moduleCompare = moduleCompare,
                capa = capa,
                certExpiring = cert,
                risk = risk
            });
        }

        // Same factory history compare: per-audit grade + nonconform counts
        [HttpGet]
        [Route("factory-history/{factoryId:guid}")]
        public IHttpActionResult FactoryHistory(Guid factoryId)
        {
            var dt = Db.Query(@"
                SELECT TOP 50 a.AuditId, a.AuditType, a.[Year], a.Status, a.FinalGrade, a.CreatedAt, a.RatedAt
                FROM Audit a WITH (NOLOCK)
                WHERE a.FactoryId=@fid
                ORDER BY a.[Year] DESC, a.CreatedAt DESC
            ", Db.P("@fid", factoryId));

            var auditIds = dt.AsEnumerable().Select(r => (Guid)r["AuditId"]).ToList();
            var ncMap = new Dictionary<Guid, int>();
            if (auditIds.Count > 0)
            {
                // count partial/fail per audit
                var inSql = string.Join(",", auditIds.Select((_, i) => "@p" + i));
                var ps = auditIds.Select((id, i) => Db.P("@p" + i, id)).ToArray();
                var sql = @"
                    SELECT AuditId, SUM(CASE WHEN Status IN (2,3) THEN 1 ELSE 0 END) AS NcCount
                    FROM AuditClauseResult WITH (NOLOCK)
                    WHERE AuditId IN (" + inSql + @")
                    GROUP BY AuditId
                ";
                var nc = Db.Query(sql, ps);
                foreach (DataRow r in nc.Rows)
                    ncMap[(Guid)r["AuditId"]] = Convert.ToInt32(r["NcCount"]);
            }

            return Ok(new
            {
                items = dt.AsEnumerable().Select(r => new
                {
                    auditId = (Guid)r["AuditId"],
                    auditType = Convert.ToInt32(r["AuditType"]),
                    year = Convert.ToInt32(r["Year"]),
                    status = Convert.ToInt32(r["Status"]),
                    finalGrade = Convert.ToString(r["FinalGrade"]),
                    createdAt = Convert.ToDateTime(r["CreatedAt"]),
                    ratedAt = r["RatedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["RatedAt"]),
                    nonconformCount = ncMap.ContainsKey((Guid)r["AuditId"]) ? ncMap[(Guid)r["AuditId"]] : 0
                }).ToList()
            });
        }

        private object GetSummary(int year)
        {
            var row = Db.QuerySingle(@"
                SELECT
                    COUNT(1) AS TotalAudits,
                    SUM(CASE WHEN FinalGrade IS NOT NULL THEN 1 ELSE 0 END) AS GradedAudits,
                    SUM(CASE WHEN FinalGrade IN ('D','E') THEN 1 ELSE 0 END) AS DeAudits
                FROM Audit WITH (NOLOCK)
                WHERE [Year]=@y
            ", Db.P("@y", year));

            var capa = Db.QuerySingle(@"
                SELECT
                    COUNT(1) AS TotalCapa,
                    SUM(CASE WHEN ci.Status=3 THEN 1 ELSE 0 END) AS ClosedCapa,
                    SUM(CASE WHEN ci.Status<>3 AND ci.DueDate IS NOT NULL AND ci.DueDate < CONVERT(date,GETDATE()) THEN 1 ELSE 0 END) AS OverdueCapa
                FROM CapaItem ci WITH (NOLOCK)
                JOIN Audit a WITH (NOLOCK) ON ci.AuditId=a.AuditId
                WHERE a.[Year]=@y
            ", Db.P("@y", year));

            int totalAudits = row == null ? 0 : Convert.ToInt32(row["TotalAudits"]);
            int gradedAudits = row == null ? 0 : Convert.ToInt32(row["GradedAudits"]);
            int deAudits = row == null ? 0 : Convert.ToInt32(row["DeAudits"]);

            int totalCapa = capa == null ? 0 : Convert.ToInt32(capa["TotalCapa"]);
            int closedCapa = capa == null ? 0 : Convert.ToInt32(capa["ClosedCapa"]);
            int overdueCapa = capa == null ? 0 : Convert.ToInt32(capa["OverdueCapa"]);

            double closureRate = totalCapa <= 0 ? 1.0 : Math.Round((double)closedCapa / (double)totalCapa, 4);

            return new
            {
                totalAudits,
                gradedAudits,
                deAudits,
                totalCapa,
                closedCapa,
                openCapa = Math.Max(0, totalCapa - closedCapa),
                overdueCapa,
                closureRate
            };
        }

        private List<object> GetGradeTrend()
        {
            var dt = Db.Query(@"
                SELECT [Year],
                    SUM(CASE WHEN FinalGrade='A' THEN 1 ELSE 0 END) AS ACount,
                    SUM(CASE WHEN FinalGrade='B' THEN 1 ELSE 0 END) AS BCount,
                    SUM(CASE WHEN FinalGrade='C' THEN 1 ELSE 0 END) AS CCount,
                    SUM(CASE WHEN FinalGrade='D' THEN 1 ELSE 0 END) AS DCount,
                    SUM(CASE WHEN FinalGrade='E' THEN 1 ELSE 0 END) AS ECount,
                    COUNT(1) AS Total
                FROM Audit WITH (NOLOCK)
                WHERE FinalGrade IS NOT NULL
                GROUP BY [Year]
                ORDER BY [Year]
            ");

            return dt.AsEnumerable().Select(r => (object)new
            {
                year = Convert.ToInt32(r["Year"]),
                a = Convert.ToInt32(r["ACount"]),
                b = Convert.ToInt32(r["BCount"]),
                c = Convert.ToInt32(r["CCount"]),
                d = Convert.ToInt32(r["DCount"]),
                e = Convert.ToInt32(r["ECount"]),
                total = Convert.ToInt32(r["Total"])
            }).ToList();
        }

        private List<object> GetModuleCompare(int year)
        {
            var dt = Db.Query(@"
                SELECT tm.ModuleId, tm.ModuleName,
                    SUM(CASE WHEN r.Status=2 THEN 1 ELSE 0 END) AS PartialFailCount,
                    SUM(CASE WHEN r.Status=3 THEN 1 ELSE 0 END) AS FailCount,
                    SUM(CASE WHEN r.Status=4 THEN 1 ELSE 0 END) AS NotApplicableCount,
                    COUNT(1) AS TotalCount
                FROM Audit a WITH (NOLOCK)
                JOIN AuditClauseResult r WITH (NOLOCK) ON a.AuditId=r.AuditId
                JOIN TemplateClause c WITH (NOLOCK) ON r.ClauseId=c.ClauseId
                JOIN TemplateModule tm WITH (NOLOCK) ON c.ModuleId=tm.ModuleId
                WHERE a.[Year]=@y AND a.FinalGrade IS NOT NULL AND r.Status<>0
                GROUP BY tm.ModuleId, tm.ModuleName
                ORDER BY tm.ModuleName
            ", Db.P("@y", year));

            return dt.AsEnumerable().Select(r => (object)new
            {
                moduleId = (Guid)r["ModuleId"],
                moduleName = Convert.ToString(r["ModuleName"]),
                partialFail = Convert.ToInt32(r["PartialFailCount"]),
                fail = Convert.ToInt32(r["FailCount"]),
                notApplicable = Convert.ToInt32(r["NotApplicableCount"]),
                total = Convert.ToInt32(r["TotalCount"]),
                nonconform = Convert.ToInt32(r["PartialFailCount"]) + Convert.ToInt32(r["FailCount"])
            }).ToList();
        }

        private object GetCapaClosure(int year)
        {
            var row = Db.QuerySingle(@"
                SELECT
                    COUNT(1) AS TotalCapa,
                    SUM(CASE WHEN ci.Status=3 THEN 1 ELSE 0 END) AS ClosedCapa,
                    SUM(CASE WHEN ci.Status<>3 AND ci.DueDate IS NOT NULL AND ci.DueDate < CONVERT(date,GETDATE()) THEN 1 ELSE 0 END) AS OverdueCapa
                FROM CapaItem ci WITH (NOLOCK)
                JOIN Audit a WITH (NOLOCK) ON ci.AuditId=a.AuditId
                WHERE a.[Year]=@y
            ", Db.P("@y", year));

            int total = row == null ? 0 : Convert.ToInt32(row["TotalCapa"]);
            int closed = row == null ? 0 : Convert.ToInt32(row["ClosedCapa"]);
            int overdue = row == null ? 0 : Convert.ToInt32(row["OverdueCapa"]);
            double closureRate = total <= 0 ? 1.0 : Math.Round((double)closed / (double)total, 4);

            return new { total, closed, open = Math.Max(0, total - closed), overdue, closureRate };
        }

        private List<object> GetCertExpiring(int days)
        {
            var dt = Db.Query(@"
                SELECT TOP 200 f.FactoryId, f.FactoryCode, f.Name AS FactoryName,
                       c.CertName, c.CertNo, c.ExpireDate,
                       DATEDIFF(day, CONVERT(date,GETDATE()), c.ExpireDate) AS DaysLeft
                FROM FactoryCertificate c WITH (NOLOCK)
                JOIN Factory f WITH (NOLOCK) ON c.FactoryId=f.FactoryId
                WHERE c.IsActive=1 AND f.IsActive=1
                  AND c.ExpireDate <= DATEADD(day, @days, CONVERT(date,GETDATE()))
                ORDER BY c.ExpireDate ASC
            ", Db.P("@days", days));

            return dt.AsEnumerable().Select(r => (object)new
            {
                factoryId = (Guid)r["FactoryId"],
                factoryCode = Convert.ToString(r["FactoryCode"]),
                factoryName = Convert.ToString(r["FactoryName"]),
                certName = Convert.ToString(r["CertName"]),
                certNo = Convert.ToString(r["CertNo"]),
                expireDate = Convert.ToDateTime(r["ExpireDate"]).ToString("yyyy-MM-dd"),
                daysLeft = Convert.ToInt32(r["DaysLeft"])
            }).ToList();
        }

        private object GetRisk(int year, int days)
        {
            var overdueCapa = Db.QuerySingle(@"
                SELECT COUNT(1) AS Cnt
                FROM CapaItem ci WITH (NOLOCK)
                WHERE ci.Status<>3 AND ci.DueDate IS NOT NULL AND ci.DueDate < CONVERT(date,GETDATE())
            ");

            var certExpired = Db.QuerySingle(@"
                SELECT COUNT(1) AS Cnt
                FROM FactoryCertificate c WITH (NOLOCK)
                WHERE c.IsActive=1 AND c.ExpireDate < CONVERT(date,GETDATE())
            ");

            var certSoon = Db.QuerySingle(@"
                SELECT COUNT(1) AS Cnt
                FROM FactoryCertificate c WITH (NOLOCK)
                WHERE c.IsActive=1 AND c.ExpireDate >= CONVERT(date,GETDATE())
                  AND c.ExpireDate <= DATEADD(day, 7, CONVERT(date,GETDATE()))
            ");

            var deFactories = Db.Query(@"
                WITH Latest AS (
                    SELECT a.FactoryId, a.[Year], a.FinalGrade,
                           ROW_NUMBER() OVER(PARTITION BY a.FactoryId ORDER BY a.[Year] DESC, a.CreatedAt DESC) AS rn
                    FROM Audit a WITH (NOLOCK)
                    WHERE a.FinalGrade IS NOT NULL
                )
                SELECT f.FactoryId, f.FactoryCode, f.Name AS FactoryName, l.[Year], l.FinalGrade,
                       (SELECT COUNT(1)
                        FROM CapaItem ci WITH (NOLOCK)
                        JOIN Audit a2 WITH (NOLOCK) ON ci.AuditId=a2.AuditId
                        WHERE a2.FactoryId=f.FactoryId
                          AND ci.Status<>3 AND ci.DueDate IS NOT NULL AND ci.DueDate < CONVERT(date,GETDATE())) AS OverdueCapa
                FROM Latest l
                JOIN Factory f WITH (NOLOCK) ON l.FactoryId=f.FactoryId
                WHERE l.rn=1 AND l.FinalGrade IN ('D','E')
                ORDER BY l.FinalGrade DESC, f.FactoryCode
            ");

            var yearDeCount = Db.QuerySingle(@"
                SELECT COUNT(1) AS Cnt
                FROM Audit WITH (NOLOCK)
                WHERE [Year]=@y AND FinalGrade IN ('D','E')
            ", Db.P("@y", year));

            return new
            {
                overdueCapaCount = overdueCapa == null ? 0 : Convert.ToInt32(overdueCapa["Cnt"]),
                certExpiredCount = certExpired == null ? 0 : Convert.ToInt32(certExpired["Cnt"]),
                certExpiringSoonCount = certSoon == null ? 0 : Convert.ToInt32(certSoon["Cnt"]),
                yearDeAudits = yearDeCount == null ? 0 : Convert.ToInt32(yearDeCount["Cnt"]),
                deFactories = deFactories.AsEnumerable().Select(r => new
                {
                    factoryId = (Guid)r["FactoryId"],
                    factoryCode = Convert.ToString(r["FactoryCode"]),
                    factoryName = Convert.ToString(r["FactoryName"]),
                    year = Convert.ToInt32(r["Year"]),
                    grade = Convert.ToString(r["FinalGrade"]),
                    overdueCapa = Convert.ToInt32(r["OverdueCapa"])
                }).ToList()
            };
        }
    }
}

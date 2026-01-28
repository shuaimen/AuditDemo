using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Configuration;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace AuditDemo.WebApi.Infrastructure
{
    public static class ExcelExporter
    {
        private static string BaseUrl => (ConfigurationManager.AppSettings["ExportBaseUrl"] ?? "").TrimEnd('/');

        // Certificate module: export expiring certificates (<= days) to Excel
        public static byte[] ExportCertificateExpiring(int days, Guid? factoryId = null)
        {
            if (days <= 0) days = 60;
            if (days > 3650) days = 3650;

            var sql = @"
                SELECT TOP 500
                    f.FactoryCode,
                    f.Name AS FactoryName,
                    c.CertName,
                    c.CertNo,
                    c.CertType,
                    c.IssueDate,
                    c.ExpireDate,
                    DATEDIFF(day, CONVERT(date,GETDATE()), c.ExpireDate) AS DaysLeft,
                    c.Remark
                FROM FactoryCertificate c WITH (NOLOCK)
                JOIN Factory f WITH (NOLOCK) ON c.FactoryId=f.FactoryId
                WHERE c.IsActive=1 AND f.IsActive=1
                  AND c.ExpireDate <= DATEADD(day, @days, CONVERT(date,GETDATE()))
                  AND (@fid IS NULL OR c.FactoryId=@fid)
                ORDER BY c.ExpireDate ASC
            ";

            var dt = Db.Query(sql,
                Db.P("@days", days),
                Db.P("@fid", (object)factoryId ?? DBNull.Value));

            IWorkbook wb = new XSSFWorkbook();
            var sheet = wb.CreateSheet("证照到期");

            var header = sheet.CreateRow(0);
            header.CreateCell(0).SetCellValue("工厂编号");
            header.CreateCell(1).SetCellValue("工厂名称");
            header.CreateCell(2).SetCellValue("证照名称");
            header.CreateCell(3).SetCellValue("证照编号");
            header.CreateCell(4).SetCellValue("证照类型");
            header.CreateCell(5).SetCellValue("发证日");
            header.CreateCell(6).SetCellValue("到期日");
            header.CreateCell(7).SetCellValue("剩余天数");
            header.CreateCell(8).SetCellValue("备注");

            var wrap = wb.CreateCellStyle();
            wrap.WrapText = true;

            var redBold = CreateRedBoldStyle(wb);
            var normal = wb.CreateCellStyle();
            normal.WrapText = true;

            int rix = 1;
            foreach (DataRow r in dt.Rows)
            {
                var row = sheet.CreateRow(rix++);

                var daysLeft = Convert.ToInt32(r["DaysLeft"]);
                bool expired = daysLeft < 0;
                var st = expired ? redBold : normal;

                row.CreateCell(0).SetCellValue(Convert.ToString(r["FactoryCode"]));
                row.CreateCell(1).SetCellValue(Convert.ToString(r["FactoryName"]));
                row.CreateCell(2).SetCellValue(Convert.ToString(r["CertName"]));
                row.CreateCell(3).SetCellValue(Convert.ToString(r["CertNo"]));
                row.CreateCell(4).SetCellValue(Convert.ToString(r["CertType"]));

                var issue = r["IssueDate"] == DBNull.Value ? "" : Convert.ToDateTime(r["IssueDate"]).ToString("yyyy-MM-dd");
                var exp = Convert.ToDateTime(r["ExpireDate"]).ToString("yyyy-MM-dd");
                row.CreateCell(5).SetCellValue(issue);
                row.CreateCell(6).SetCellValue(exp);
                row.CreateCell(7).SetCellValue(daysLeft);
                row.CreateCell(8).SetCellValue(Convert.ToString(r["Remark"]));
                row.GetCell(8).CellStyle = wrap;

                for (int i = 0; i <= 7; i++) row.GetCell(i).CellStyle = st;
            }

            for (int i = 0; i <= 8; i++) sheet.AutoSizeColumn(i);

            using (var ms = new MemoryStream())
            {
                wb.Write(ms);
                return ms.ToArray();
            }
        }

        public static byte[] ExportAuditDetail(Guid auditId)
        {
            var dt = Db.Query(@"
                SELECT
                  m.ModuleName,
                  r.ClauseCode,
                  r.ClauseLevel,
                  c.Content,
                  r.Status,
                  r.Comment,
                  r.ResultId
                FROM AuditClauseResult r
                JOIN TemplateClause c ON r.ClauseId=c.ClauseId
                JOIN TemplateModule m ON c.ModuleId=m.ModuleId
                WHERE r.AuditId=@id
                ORDER BY m.SortNo, c.SortNo
            ", Db.P("@id", auditId));

            var photoDt = Db.Query(@"
                SELECT p.PhotoId, p.ResultId, p.SortNo
                FROM AuditClausePhoto p
                JOIN AuditClauseResult r ON p.ResultId=r.ResultId
                WHERE r.AuditId=@id
                ORDER BY p.SortNo
            ", Db.P("@id", auditId));

            var photoMap = new Dictionary<Guid, List<string>>();
            foreach (DataRow pr in photoDt.Rows)
            {
                var resultId = (Guid)pr["ResultId"];
                var photoId = (Guid)pr["PhotoId"];
                var url = BuildFileUrl(photoId);
                if (!photoMap.ContainsKey(resultId)) photoMap[resultId] = new List<string>();
                photoMap[resultId].Add(url);
            }

            IWorkbook wb = new XSSFWorkbook();
            var sheet = wb.CreateSheet("明细");

            var header = sheet.CreateRow(0);
            header.CreateCell(0).SetCellValue("模块");
            header.CreateCell(1).SetCellValue("条款ID");
            header.CreateCell(2).SetCellValue("等级");
            header.CreateCell(3).SetCellValue("条款内容");
            header.CreateCell(4).SetCellValue("状态");
            header.CreateCell(5).SetCellValue("评估记录");
            header.CreateCell(6).SetCellValue("照片");

            var wrapStyle = wb.CreateCellStyle();
            wrapStyle.WrapText = true;

            var redBold = CreateRedBoldStyle(wb);
            var normal = wb.CreateCellStyle();
            normal.WrapText = true;

            int rowIndex = 1;
            foreach (DataRow r in dt.Rows)
            {
                var row = sheet.CreateRow(rowIndex++);

                var status = Convert.ToInt32(r["Status"]);
                bool bad = (status == 2 || status == 3);

                row.CreateCell(0).SetCellValue(Convert.ToString(r["ModuleName"]));
                row.CreateCell(1).SetCellValue(Convert.ToString(r["ClauseCode"]));
                row.CreateCell(2).SetCellValue(Convert.ToString(r["ClauseLevel"]));
                row.CreateCell(3).SetCellValue(Convert.ToString(r["Content"]));
                row.CreateCell(4).SetCellValue(StatusText(status));
                row.CreateCell(5).SetCellValue(Convert.ToString(r["Comment"]));

                var resultId = (Guid)r["ResultId"];
                var photos = photoMap.ContainsKey(resultId) ? photoMap[resultId] : null;
                var photoCell = row.CreateCell(6);
                photoCell.CellStyle = wrapStyle;
                if (photos != null && photos.Count > 0)
                    photoCell.SetCellValue(string.Join("\n", photos));

                // style
                var st = bad ? redBold : normal;
                for (int i = 0; i <= 5; i++) row.GetCell(i).CellStyle = st;
                row.GetCell(6).CellStyle = wrapStyle;
            }

            for (int i = 0; i <= 6; i++) sheet.AutoSizeColumn(i);

            // Nonconformity sheet
            ExportNonconformitySheet(wb, auditId, photoMap);

            using (var ms = new MemoryStream())
            {
                wb.Write(ms);
                return ms.ToArray();
            }
        }

        private static void ExportNonconformitySheet(IWorkbook wb, Guid auditId, Dictionary<Guid, List<string>> photoMap)
        {
            var dt = Db.Query(@"
                SELECT
                  m.ModuleName,
                  m.SortNo AS ModuleSort,
                  r.ClauseCode,
                  r.ClauseLevel,
                  c.Content,
                  r.Status,
                  r.Comment,
                  r.ResultId,
                  c.SortNo AS ClauseSort
                FROM AuditClauseResult r
                JOIN TemplateClause c ON r.ClauseId=c.ClauseId
                JOIN TemplateModule m ON c.ModuleId=m.ModuleId
                WHERE r.AuditId=@id AND r.Status IN (2,3)
                ORDER BY m.SortNo, c.SortNo
            ", Db.P("@id", auditId));

            // Pre-count per module for "模块名（不合格条数：X）" header rows
            var moduleCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (DataRow r in dt.Rows)
            {
                var mn = Convert.ToString(r["ModuleName"]);
                if (string.IsNullOrWhiteSpace(mn)) mn = "(未命名模块)";
                int cur;
                moduleCounts.TryGetValue(mn, out cur);
                moduleCounts[mn] = cur + 1;
            }

            var sheet = wb.CreateSheet("不合格汇总");
            var redBold = CreateRedBoldStyle(wb);
            var wrap = wb.CreateCellStyle();
            wrap.WrapText = true;

            int rix = 0;
            var header = sheet.CreateRow(rix++);
            header.CreateCell(0).SetCellValue("模块/小计");
            header.CreateCell(1).SetCellValue("条款ID");
            header.CreateCell(2).SetCellValue("等级");
            header.CreateCell(3).SetCellValue("条款内容");
            header.CreateCell(4).SetCellValue("状态");
            header.CreateCell(5).SetCellValue("评估记录");
            header.CreateCell(6).SetCellValue("照片");

            string curModule = null;

            foreach (DataRow r in dt.Rows)
            {
                var moduleName = Convert.ToString(r["ModuleName"]);
                if (string.IsNullOrWhiteSpace(moduleName)) moduleName = "(未命名模块)";

                if (curModule == null || !string.Equals(curModule, moduleName, StringComparison.Ordinal))
                {
                    curModule = moduleName;
                    int cnt = 0;
                    moduleCounts.TryGetValue(curModule, out cnt);

                    var hrow = sheet.CreateRow(rix++);
                    var hcell = hrow.CreateCell(0);
                    hcell.SetCellValue(string.Format("{0}（不合格条数：{1}）", curModule, cnt));
                    hcell.CellStyle = redBold;
                    for (int i = 1; i <= 6; i++) hrow.CreateCell(i).SetCellValue("");
                }
                var row = sheet.CreateRow(rix++);
                row.CreateCell(0).SetCellValue(moduleName);
                row.CreateCell(1).SetCellValue(Convert.ToString(r["ClauseCode"]));
                row.CreateCell(2).SetCellValue(Convert.ToString(r["ClauseLevel"]));
                row.CreateCell(3).SetCellValue(Convert.ToString(r["Content"]));
                var st = Convert.ToInt32(r["Status"]);
                row.CreateCell(4).SetCellValue(StatusText(st));
                row.CreateCell(5).SetCellValue(Convert.ToString(r["Comment"]));

                var resultId = (Guid)r["ResultId"];
                var photos = photoMap.ContainsKey(resultId) ? photoMap[resultId] : null;
                var pcell = row.CreateCell(6);
                pcell.CellStyle = wrap;
                if (photos != null && photos.Count > 0)
                    pcell.SetCellValue(string.Join("\n", photos));

                for (int i = 0; i <= 5; i++) row.GetCell(i).CellStyle = redBold;
            }

            for (int i = 0; i <= 6; i++) sheet.AutoSizeColumn(i);
        }

        private static ICellStyle CreateRedBoldStyle(IWorkbook wb)
        {
            var font = wb.CreateFont();
            font.IsBold = true;
            font.Color = IndexedColors.Red.Index;

            var style = wb.CreateCellStyle();
            style.SetFont(font);
            style.WrapText = true;
            return style;
        }

        private static string StatusText(int status)
        {
            switch (status)
            {
                case 1: return "符合";
                case 2: return "部分不符合";
                case 3: return "不符合";
                case 4: return "不适用";
                case 0:
                default: return "未填写";
            }
        }

        private static string BuildFileUrl(Guid photoId)
        {
            var p = "/api/files/" + photoId.ToString("N");
            return string.IsNullOrWhiteSpace(BaseUrl) ? p : (BaseUrl + p);
        }
    }
}

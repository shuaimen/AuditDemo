using System;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace AuditDemo.WebApi.Infrastructure
{
    public class TokenAuthHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri.AbsolutePath.ToLowerInvariant();
            if (path.StartsWith("/api/auth/login"))
                return base.SendAsync(request, cancellationToken);

            if (!path.StartsWith("/api/"))
                return base.SendAsync(request, cancellationToken);

            if (!request.Headers.Contains("X-Token"))
                return Task.FromResult(request.CreateResponse(HttpStatusCode.Unauthorized, new { message = "Missing X-Token" }));

            var token = string.Join("", request.Headers.GetValues("X-Token"));
            if (string.IsNullOrWhiteSpace(token))
                return Task.FromResult(request.CreateResponse(HttpStatusCode.Unauthorized, new { message = "Empty token" }));

            var row = Db.QuerySingle(
                @"SELECT TOP 1 t.Token, t.UserId, u.Role, u.IsActive, t.ExpiredAt
                  FROM AuthToken t WITH (NOLOCK)
                  JOIN [User] u WITH (NOLOCK) ON t.UserId=u.UserId
                  WHERE t.Token=@t AND t.ExpiredAt>GETDATE()",
                Db.P("@t", token));

            if (row == null || row["IsActive"] == DBNull.Value || !(bool)row["IsActive"])
                return Task.FromResult(request.CreateResponse(HttpStatusCode.Unauthorized, new { message = "Invalid token" }));

            var userId = (Guid)row["UserId"];
            var role = Convert.ToInt32(row["Role"]);

            var identity = new ClaimsIdentity("Token");
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
            identity.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
            var principal = new ClaimsPrincipal(identity);

            Thread.CurrentPrincipal = principal;
            if (HttpContext.Current != null) HttpContext.Current.User = principal;

            return base.SendAsync(request, cancellationToken);
        }
    }

    public static class Auth
    {
        public static Guid CurrentUserId
        {
            get
            {
                var p = Thread.CurrentPrincipal as ClaimsPrincipal;
                var id = p?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                Guid g;
                return Guid.TryParse(id, out g) ? g : Guid.Empty;
            }
        }

        public static int CurrentRole
        {
            get
            {
                var p = Thread.CurrentPrincipal as ClaimsPrincipal;
                var r = p?.FindFirst(ClaimTypes.Role)?.Value;
                int v;
                return int.TryParse(r, out v) ? v : 0;
            }
        }

        public static bool IsAdmin => CurrentRole == 1;
    }
}

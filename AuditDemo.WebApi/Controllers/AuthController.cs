using System;
using System.Configuration;
using System.Web.Http;
using AuditDemo.WebApi.Infrastructure;
using AuditDemo.WebApi.Models;

namespace AuditDemo.WebApi.Controllers
{
    [RoutePrefix("api/auth")]
    public class AuthController : ApiController
    {
        [HttpPost]
        [Route("login")]
        public IHttpActionResult Login(LoginRequest req)
        {
            if (req == null) return BadRequest("Missing body");
            var username = (req.Username ?? string.Empty).Trim();
            var password = req.Password ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username)) return BadRequest("Username required");

            var row = Db.QuerySingle(@"
                SELECT TOP 1 UserId, Username, PasswordHash, PasswordSalt, Role, IsActive
                FROM [User] WITH (NOLOCK)
                WHERE Username=@u
            ", Db.P("@u", username));

            // Bootstrap demo accounts: if user table is empty, allow first login to auto-create.
            if (row == null)
            {
                if ((string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase) && password == "admin123")
                    || (string.Equals(username, "auditor", StringComparison.OrdinalIgnoreCase) && password == "auditor123"))
                {
                    var userRole = string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
                    byte[] pwdSalt, pwdHash;
                    PasswordHasher.CreateHash(password, out pwdSalt, out pwdHash);
                    var newId = Guid.NewGuid();
                    Db.Execute(@"INSERT INTO [User](UserId, Username, PasswordHash, PasswordSalt, Role, IsActive, CreatedAt)
                                VALUES(@id,@u,@h,@s,@r,1,GETDATE())",
                        Db.P("@id", newId), Db.P("@u", username), Db.P("@h", pwdHash), Db.P("@s", pwdSalt), Db.P("@r", userRole));
                    row = Db.QuerySingle(@"SELECT TOP 1 UserId, Username, PasswordHash, PasswordSalt, Role, IsActive FROM [User] WITH (NOLOCK) WHERE Username=@u", Db.P("@u", username));
                }
            }
            if (row == null) return Unauthorized();
            if (!(bool)row["IsActive"]) return Unauthorized();

            var salt = (byte[])row["PasswordSalt"];
            var hash = (byte[])row["PasswordHash"];
            if (!PasswordHasher.Verify(password, salt, hash)) return Unauthorized();

            var userId = (Guid)row["UserId"];
            var role = Convert.ToInt32(row["Role"]);

            var token = Guid.NewGuid().ToString("N");
            int hours;
            if (!int.TryParse(ConfigurationManager.AppSettings["TokenExpiryHours"], out hours)) hours = 12;

            Db.Execute(@"
                INSERT INTO AuthToken(Token, UserId, CreatedAt, ExpiredAt)
                VALUES(@t, @uid, GETDATE(), DATEADD(HOUR, @h, GETDATE()))
            ",
            Db.P("@t", token),
            Db.P("@uid", userId),
            Db.P("@h", hours));

            var resp = new LoginResponse
            {
                Token = token,
                UserId = userId,
                Role = role,
                RoleName = role == 1 ? "Admin" : "Auditor"
            };
            return Ok(resp);
        }
    }
}

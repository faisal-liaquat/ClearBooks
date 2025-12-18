using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClearBooksFYP.Models;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Text;

namespace ClearBooksFYP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ClearBooksDbContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(ClearBooksDbContext context, ILogger<AuthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // create an account
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
        {
            try
            {
                //validate input
                if (string.IsNullOrWhiteSpace(request.Name) ||
                    string.IsNullOrWhiteSpace(request.Username) ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { message = "All fields are required" });
                }

                //check if username already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Email);

                if (existingUser != null)
                {
                    return BadRequest(new { message = "Username or email already exists" });
                }

                //gash password
                var passwordHash = HashPassword(request.Password);

                //reate new user
                var user = new User
                {
                    Name = request.Name.Trim(),
                    Username = request.Username.Trim().ToLower(),
                    Email = request.Email.Trim().ToLower(),
                    PasswordHash = passwordHash,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsActive = true
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // create session
                var session = await CreateSession(user.UserId);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Registration successful",
                    User = new UserDto
                    {
                        UserId = user.UserId,
                        Name = user.Name,
                        Username = user.Username,
                        Email = user.Email
                    },
                    SessionId = session.SessionId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return StatusCode(500, new { message = "Internal server error during registration" });
            }
        }


        //login
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
        {
            try
            {
                //validate input
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { message = "Username and password are required" });
                }

                //find user
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username.ToLower() && u.IsActive);

                if (user == null)
                {
                    return BadRequest(new { message = "Invalid username or password" });
                }

                //vrify password
                if (!VerifyPassword(request.Password, user.PasswordHash))
                {
                    return BadRequest(new { message = "Invalid username or password" });
                }

                //ceate session
                var session = await CreateSession(user.UserId);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Login successful",
                    User = new UserDto
                    {
                        UserId = user.UserId,
                        Name = user.Name,
                        Username = user.Username,
                        Email = user.Email
                    },
                    SessionId = session.SessionId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                return StatusCode(404, new { message = "Internal server error during login" });
            }
        }

        //logout
        [HttpPost("logout")]
        public async Task<ActionResult> Logout()
        {
            try
            {
                var sessionId = GetSessionIdFromRequest();
                if (!string.IsNullOrEmpty(sessionId))
                {
                    await InvalidateSession(sessionId);
                }

                return Ok(new { message = "Logout successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(404, new { message = "Internal server error during logout" });
            }
        }


        //validation
        [HttpGet("validate")]
        public async Task<ActionResult<UserDto>> ValidateSession()
        {
            try
            {
                var sessionId = GetSessionIdFromRequest();
                if (string.IsNullOrEmpty(sessionId))
                {
                    return Unauthorized(new { message = "No session found" });
                }

                var sessionData = await _context.UserSessions
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId &&
                                            s.ExpiresAt > DateTime.Now &&
                                            s.IsActive &&
                                            s.User.IsActive);

                if (sessionData == null)
                {
                    return Unauthorized(new { message = "Invalid or expired session" });
                }

                return Ok(new UserDto
                {
                    UserId = sessionData.User.UserId,
                    Name = sessionData.User.Name,
                    Username = sessionData.User.Username,
                    Email = sessionData.User.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session validation");
                return StatusCode(500, new { message = "Internal server error during validation" });
            }
        }

        //private helper methods
        private string HashPassword(string password)
        {
            //generate a 128-bit salt using a secure PRNG
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            //hash the password with the salt
            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));

            //combine salt and hash for storage
            return Convert.ToBase64String(salt) + ":" + hashed;
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                var parts = storedHash.Split(':');
                if (parts.Length != 2)
                    return false;

                var salt = Convert.FromBase64String(parts[0]);
                var hash = parts[1];

                //hash the provided password with the stored salt
                string computedHash = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                    password: password,
                    salt: salt,
                    prf: KeyDerivationPrf.HMACSHA256,
                    iterationCount: 10000,
                    numBytesRequested: 256 / 8));

                return hash == computedHash;
            }
            catch
            {
                return false;
            }
        }

        private async Task<UserSession> CreateSession(int userId)
        {
            //generate unique session ID
            var sessionId = Guid.NewGuid().ToString() + "-" + Guid.NewGuid().ToString();

            var session = new UserSession
            {
                SessionId = sessionId,
                UserId = userId,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddDays(7), //session expires in 7 days
                IsActive = true
            };

            _context.UserSessions.Add(session);
            await _context.SaveChangesAsync();

            return session;
        }

        private async Task InvalidateSession(string sessionId)
        {
            var session = await _context.UserSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session != null)
            {
                session.IsActive = false;
                await _context.SaveChangesAsync();
            }
        }

        private string GetSessionIdFromRequest()
        {
            //try to get session ID from Authorization header
            if (Request.Headers.ContainsKey("Authorization"))
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    return authHeader.Substring("Bearer ".Length);
                }
            }

            //try to get session ID from cookie
            if (Request.Cookies.ContainsKey("SessionId"))
            {
                return Request.Cookies["SessionId"];
            }

            return null;
        }

        //clean up expired sessions
        [HttpPost("cleanup")]
        public async Task<ActionResult> CleanupExpiredSessions()
        {
            try
            {
                var expiredSessions = await _context.UserSessions
                    .Where(s => s.ExpiresAt < DateTime.Now || !s.IsActive)
                    .ToListAsync();

                _context.UserSessions.RemoveRange(expiredSessions);
                await _context.SaveChangesAsync();

                return Ok(new { message = $"Cleaned up {expiredSessions.Count} expired sessions" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
                return StatusCode(500, new { message = "Internal server error during cleanup" });
            }
        }
    }

    //dtoss
    public class RegisterRequest
    {
        public string Name { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public UserDto User { get; set; }
        public string SessionId { get; set; }
    }

    public class UserDto
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
    }
}
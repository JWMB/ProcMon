using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Security.Principal;

namespace ProcServer.Services
{
    public interface IUserRepository
    {
        Task<User?> GetByUsernameAndPassword(string username, string password);

        public ClaimsIdentity CreateIdentity(User user)
        {
			var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };
			return new ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
		}

        public static User? CreateUser(ClaimsPrincipal? identity)
        {
            if (identity?.Identity?.IsAuthenticated != true)
                return null;

            var claims = new[]
            {
                ClaimTypes.Role,
                ClaimTypes.Name
            }.ToDictionary(o => o, o => identity.FindFirst(o)?.Value);

            if (claims.Any(o => string.IsNullOrEmpty(o.Value)))
                return null;

            return new User
            {
                Password = "hidden",
                Username = claims[ClaimTypes.Name]!,
                Role = Enum.Parse<UserRole>(claims[ClaimTypes.Role]!)
            };
        }
	}

    public enum UserRole
    {
        Anonymous,
        User,
        Admin
    }

    public class User
    {
        public required string Username { get; set; }
		public required string Password { get; set; }
        public UserRole Role { get; set; } = UserRole.Anonymous;
	}

	public class HardcodedUserRepository : IUserRepository
    {
        private readonly Config config;
        private readonly ILogger<HardcodedUserRepository> log;

        public record Config(List<User> Users);

        public HardcodedUserRepository(Config config, ILogger<HardcodedUserRepository> log)
        {
            this.config = config;
            this.log = log;
        }

        public async Task<User?> GetByUsernameAndPassword(string username, string password)
        {
			//Trace.TraceError($"TFound");
			//log.LogError($"LE");
			//log.LogInformation($"LI");
			//Console.WriteLine($"CFound");
			//Debug.WriteLine($"Found");

			if (string.IsNullOrEmpty(password))
            {
				//log.LogInformation($"No pwd provided");
				log.LogError($"No pwd provided");
				return null;
			}

            var user = config.Users.SingleOrDefault(o => o.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (user == null)
            {
				log.LogError($"No usr {username} ({config.Users.Count})");
				return null;
			}

			//log.LogError($"Found {user.Username}");

            try
            {
				var hasher = new PasswordHasher<User>();
				if (hasher.VerifyHashedPassword(user, user.Password, password) != PasswordVerificationResult.Failed)
					return user;
			}
            catch (FormatException fEx) when (fEx.Message.Contains("non-base 64"))
            {
				log.LogError($"Pwd not hashed for {user.Username}");
			}

			// allow clear text for now
			if (user.Password == password)
                return user;

			log.LogError($"Incorrect pwd for {user.Username}");
			return null;
        }
    }
}

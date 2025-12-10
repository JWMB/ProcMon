using Microsoft.AspNetCore.Identity;

namespace ProcServer.Services
{
    public interface IUserRepository
    {
        Task<User?> GetByUsernameAndPassword(string username, string password);
    }
    public class User
    {
        public required string Username { get; set; }
		public required string Password { get; set; }
	}

	public class HardcodedUserRepository : IUserRepository
    {
        private readonly Config config;

        public record Config(List<User> Users);

        public HardcodedUserRepository(Config config)
        {
            this.config = config;
        }

        public async Task<User?> GetByUsernameAndPassword(string username, string password)
        {
            var hasher = new PasswordHasher<User>();

            var user = config.Users.SingleOrDefault(o => o.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (user == null)
                return null;

            if (hasher.VerifyHashedPassword(user, user.Password, password) != PasswordVerificationResult.Failed)
                return user;
            // allow clear text
            if (user.Password == password)
                return user;

            return null;
        }
    }
}

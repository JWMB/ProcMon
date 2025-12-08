using Microsoft.AspNetCore.Authorization;

namespace ProcServer
{

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class AuthorizePageHandlerAttribute : Attribute, IAuthorizeData
	{
		public AuthorizePageHandlerAttribute(string? policy = null)
		{
			Policy = policy;
		}

		public string? Policy { get; set; }

		public string? Roles { get; set; }

		public string? AuthenticationSchemes { get; set; }
	}
}
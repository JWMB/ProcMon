using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using System.Reflection;

namespace ProcServer
{
    public class AuthorizePageHandlerFilter : IAsyncPageFilter, IOrderedFilter
	{
		private readonly IAuthorizationPolicyProvider policyProvider;
		private readonly IPolicyEvaluator policyEvaluator;

		public AuthorizePageHandlerFilter(
			IAuthorizationPolicyProvider policyProvider,
			IPolicyEvaluator policyEvaluator)
		{
			this.policyProvider = policyProvider;
			this.policyEvaluator = policyEvaluator;
		}

		// Run late in the selection pipeline
		public int Order => 10000;

		public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
		{
			var policy = await GP(context.HandlerMethod);
			if (policy is not null)
			{ }
			await next();
		}

		public async Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
		{
			var policy = await GP(context.HandlerMethod);
			if (policy is null)
				return;

			await AuthorizeAsync(context, policy);
		}

		private async Task<AuthorizationPolicy?> GP(HandlerMethodDescriptor? method)
		{
			var attribute = method?.MethodInfo?.GetCustomAttribute<AuthorizePageHandlerAttribute>();
			if (attribute is null)
				return null;

			return await AuthorizationPolicy.CombineAsync(policyProvider, new[] { attribute });
		}

		private async Task AuthorizeAsync(ActionContext actionContext, AuthorizationPolicy policy)
		{
			var httpContext = actionContext.HttpContext;
			var authenticateResult = await policyEvaluator.AuthenticateAsync(policy, httpContext);
			var authorizeResult = await policyEvaluator.AuthorizeAsync(policy, authenticateResult, httpContext, actionContext.ActionDescriptor);
			if (authorizeResult.Challenged)
			{
				if (policy.AuthenticationSchemes.Count > 0)
				{
					foreach (var scheme in policy.AuthenticationSchemes)
					{
						await httpContext.ChallengeAsync(scheme);
					}
				}
				else
				{
					await httpContext.ChallengeAsync();
				}

				return;
			}
			else if (authorizeResult.Forbidden)
			{
				if (policy.AuthenticationSchemes.Count > 0)
				{
					foreach (var scheme in policy.AuthenticationSchemes)
					{
						await httpContext.ForbidAsync(scheme);
					}
				}
				else
				{
					await httpContext.ForbidAsync();
				}

				return;
			}
		}
	}
}
using Microsoft.AspNetCore.Mvc;

namespace ProcServer.Controllers
{
	[ApiController]
	[Route("api/[controller]")] //[action]
	public class DirectivesController : ControllerBase
	{
        private readonly IDirectiveStore directiveStore;

        public DirectivesController(IDirectiveStore directiveStore)
        {
            this.directiveStore = directiveStore;
        }

        [HttpGet]
		public async Task<ActionResult<List<Directive>>> Index()
		{
			return Ok(await directiveStore.GetActiveDirectives());
		}

		//[HttpDelete]
		//public async Task<ActionResult> MarkConsumed([FromBody] List<string> ids)
		//{
		//	await directiveStore.MarkConsumed(ids);
		//	return Ok();
		//}
		[HttpDelete]
		public async Task<ActionResult> MarkConsumed([FromQuery] DateTime until)
		{
			await directiveStore.MarkConsumed(until);
			return Ok();
		}

		[HttpPost]
		public async Task<IActionResult> AddDirectives(List<Directive> directives)
		{
			await directiveStore.AddDirectives(directives);
			return Ok();
		}
	}


	public interface IDirectiveStore
	{
		Task<List<Directive>> GetActiveDirectives();
		Task AddDirectives(IEnumerable<Directive> directives);
		Task MarkConsumed(IEnumerable<string> ids);
		Task MarkConsumed(DateTime until);
	}

	public class DirectiveStore : IDirectiveStore
    {
		private readonly List<Directive> store = new();
		public Task AddDirectives(IEnumerable<Directive> directives)
        {
			foreach (var item in directives)
				if (string.IsNullOrEmpty(item.Id))
					item.Id = Guid.NewGuid().ToString();
			store.AddRange(directives);
			return Task.CompletedTask;
		}

        public Task<List<Directive>> GetActiveDirectives()
        {
			return Task.FromResult(store.Where(d => d.Consumed == null).ToList());
		}

        public Task MarkConsumed(IEnumerable<string> ids)
        {
			var items = store.Where(d => ids.Contains(d.Id)).ToList();
			foreach (var item in items)
				item.Consumed = DateTime.UtcNow;
			return Task.CompletedTask;
		}

        public Task MarkConsumed(DateTime until)
        {
			var items = store.Where(d => d.Consumed == null && d.Created < until);
			foreach (var item in items)
				item.Consumed = DateTime.UtcNow;
			return Task.CompletedTask;
		}
	}

}

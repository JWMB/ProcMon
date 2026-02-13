using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ProcServer.Pages
{
    public class IndexModel : PageModel
    {
		private readonly IMessageRepository repository;

		public List<Session> Sessions { get; set; } = new();
		private static (DateTime, List<Session>) cached = new();

		public IndexModel(IMessageRepository repository)
		{
			this.repository = repository;
		}

		public async Task<IActionResult> OnGetAsync()
		{
			if (cached.Item1 == default || (DateTime.UtcNow - cached.Item1).TotalMinutes > 5)
			{
				var entries = await repository.Get(DateTime.UtcNow.AddDays(-3));
				Sessions = Session.GetSessions(entries, skipSessionsShorterThan: TimeSpan.FromMinutes(5));
				//cached = ()
				//var appStats = ApplicationStats.Create(entries.Select(p => (p.Time, p.Message)));
				//DurationToday = appStats.Select(o => ApplicationStats.GetTotalTime(o.Events)).Aggregate((p, c) => p + c);
			}
			else
			{
				Sessions = cached.Item2;
			}
			return Page();
		}
		//public void OnGet() { }
	}
}

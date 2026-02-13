using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProcServer.Services;

namespace ProcServer.Pages
{
	[TypeFilter(typeof(AuthorizePageHandlerFilter))]
	public class ProcessesModel : PageModel
    {
        private readonly IMessageRepository repository;

        public ProcessesModel(IMessageRepository repository)
        {
            this.repository = repository;
        }

        public List<Entry> Entries { get; set; } = new();
		public List<string> Senders { get; set; } = new();
		public List<(DateTime, List<Session.AppTime>)> Stats { get; set; } = new();
		public List<Session> Sessions { get; set; } = new();
		public List<Entry> AppDetails { get; set; } = new();

		[AuthorizePageHandler]
		public async Task<IActionResult> OnGetAsync()
		{
			var role = IUserRepository.CreateUser(User)?.Role;
			if (role == null)
				return new UnauthorizedResult();

			var since = TimeSpan.FromDays(-30);
			var sender = Request.Query["sender"].FirstOrDefault();
			if (string.IsNullOrWhiteSpace(sender))
				sender = null;

			Entries = await repository.Get(DateTime.UtcNow + since, sender);

			//var senders = Entries.Select(o => o.Sender).Distinct().ToList();

			Sessions = Session.GetSessions(Entries, skipSessionsShorterThan: TimeSpan.FromMinutes(5));

			var detailsForApp = Request.Query["app"].FirstOrDefault();
			if (detailsForApp != null && role >= UserRole.Admin)
			{
				var date = DateOnly.TryParse(Request.Query["date"].FirstOrDefault() ?? "", out var v) ? (DateOnly?)v : null;
				AppDetails = Entries
					.Where(o => o.Message.Application == detailsForApp)
					.Where(o => date.HasValue ? DateOnly.FromDateTime(o.Time) == date : true)
					.OrderBy(o => o.Time)
					.ToList();
			}

			//Stats = Entries.GroupBy(o => o.Time.Date)
			//	.Select(o => new { Date = o.Key, Start = o.Min(o => o.Time), Stats = ApplicationStats.Create(o.Select(p => (p.Time, p.Message)))
			//		.Select(p => new { p.Application, Time = GetTotalTime(p.Events).Modulo(TimeSpanExtensions.CreateFullModuloUntil(tsRounding)) }) })
			//	.OrderByDescending(o => o.Date)
			//	.Select(o => (o.Start, o.Stats.Select(p => new AppTime(p.Application, p.Time)).ToList()))
			//	.ToList();

			//DayStats = Stats
			//	.Select(o => (o.Item1, TimeOnly.FromDateTime(o.Item2.Min(p => p.)), o.Item2.Max(p => p.Time)))
			//	.ToList();

			return Page();
		}

		[AuthorizePageHandler]
		public void OnPostAuthorized()
		{
		}
	}
}

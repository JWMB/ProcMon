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

        public List<(DateTime, Message)> Entries { get; set; } = new();
		public List<(DateOnly, List<(string Application, TimeSpan Time)>)> Stats { get; set; } = new();
		public List<(DateOnly, TimeOnly, TimeOnly)> DayStats { get; set; } = new();
		public List<(DateTime, Message)> AppDetails { get; set; } = new();

		[AuthorizePageHandler]
		public async Task<IActionResult> OnGetAsync()
		{
			var since = TimeSpan.FromDays(-30);
			Entries = await repository.Get(DateTime.UtcNow + since);

			var detailsForApp = Request.Query["app"].FirstOrDefault();
			if (detailsForApp != null)
			{
				var date = DateOnly.TryParse(Request.Query["date"].FirstOrDefault() ?? "", out var v) ? (DateOnly?)v : null;
				AppDetails = Entries
					.Where(o => o.Item2.Application == detailsForApp)
					.Where(o => date.HasValue ? DateOnly.FromDateTime(o.Item1) == date : true)
					.OrderBy(o => o.Item1)
					.ToList();
			}
			var tsRounding = TimeSpanExtensions.TimeSpanPart.Minutes;
			
			Stats = Entries.GroupBy(o => o.Item1.Date)
				.Select(o => new { Date = o.Key, Stats = ApplicationStats.Create(o)
					.Select(p => new { p.Application, Time = GetTotalTime(p.Events).Modulo(TimeSpanExtensions.CreateFullModuloUntil(tsRounding)) }) })
				.OrderByDescending(o => o.Date)
				.Select(o => (DateOnly.FromDateTime(o.Date), o.Stats.Select(p => (p.Application, p.Time)).ToList()))
				.ToList();

			//DayStats = Stats
			//	.Select(o => (o.Item1, TimeOnly.FromDateTime(o.Item2.Min(p => p.)), o.Item2.Max(p => p.Time)))
			//	.ToList();

			TimeSpan GetTotalTime(IEnumerable<global::ApplicationStats.Event> events)
			{
				var total = TimeSpan.Zero;
				var firstIndex = events.Index().FirstOrDefault(o => o.Item.Action != "Defocus").Index;
				var lastTime = events.Skip(firstIndex).First().Time;
				var lastAction = "";
				foreach (var e in events.Skip(firstIndex + 1))
				{
					if (lastAction != "Defocus")
						total += e.Time - lastTime;
					lastAction = e.Action;
					lastTime = e.Time;
				}
				return total;
			}

			return Page();
		}


		[AuthorizePageHandler]
		public void OnPostAuthorized()
		{

		}
	}
}

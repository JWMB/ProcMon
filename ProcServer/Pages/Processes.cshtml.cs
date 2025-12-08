using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProcServer.Services;

namespace ProcServer.Pages
{
	//[TypeFilter(typeof(AuthorizePageHandlerFilter))]
	public class ProcessesModel : PageModel
    {
        private readonly IMessageRepository repository;

        public ProcessesModel(IMessageRepository repository)
        {
            this.repository = repository;
        }

        public List<(DateTime, Message)> Entries { get; set; } = new();

		public async Task<IActionResult> OnGetAsync()
		{
			var since = TimeSpan.FromDays(-20);
			Entries = await repository.Get(DateTime.UtcNow + since);
			return Page();
		}


		//[AuthorizePageHandler]
		public void OnPostAuthorized()
		{

		}
	}
}

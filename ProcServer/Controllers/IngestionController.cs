using Microsoft.AspNetCore.Mvc;
using ProcServer.Services;

namespace ProcServer.Controllers
{
	[ApiController]
	[Route("api/[controller]")] //[action]
	public class IngestionController : ControllerBase
    {
        private readonly ILogSink logWriter;
        private readonly ILogItemParser logItemParser;

        public IngestionController(ILogSink logWriter, ILogItemParser logItemParser)
        {
            this.logWriter = logWriter;
            this.logItemParser = logItemParser;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return Ok();
        }

        [HttpPost]
        public async Task<ActionResult<int>> Post([FromBody] PostDto dto)
        {
            var msgs = dto.Messages.Select(o => o.Trim()).Where(o => o.Any()).ToList();

            foreach (var msg in msgs)
            {
				var dict = logItemParser.Parse(msg);
                //Console.WriteLine($"parsed: {string.Join(",", dict.Select(o => $"{o.Key}={o.Value}"))}");
			}
			//Console.WriteLine($"Got {msgs.Count} msgs: {string.Join("\n", msgs.Select(o => o.Substring(0, Math.Min(50, o.Length - 1))))}");
			await logWriter.Write(msgs);
			return Ok(1);
        }

		public record PostDto(string[] Messages);
    }
}

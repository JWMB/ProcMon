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
        private readonly IMessageRepository messageRepository;

        public IngestionController(ILogSink logWriter, ILogItemParser logItemParser, IMessageRepository messageRepository)
        {
            this.logWriter = logWriter;
            this.logItemParser = logItemParser;
            this.messageRepository = messageRepository;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return Ok();
        }

        [HttpPost]
        public async Task<ActionResult<int>> Post([FromBody] PostDto dto)
        {
			// POST { "Messages": ["2025-12-14T22:27:18.70 {\"Action\":\"Start\",\"Application\":\"tposd\",\"Title\":\"\",\"Id\":5820}"] }
			var msgs = dto.Messages.Select(o => o.Trim()).Where(o => o.Any()).ToList();

            foreach (var msg in msgs)
            {
				var dict = logItemParser.Parse(msg);
				var tmp = System.Text.Json.JsonSerializer.Deserialize<(DateTime, Message)>(System.Text.Json.JsonSerializer.Serialize(dict));
                await messageRepository.Add(tmp.Item1, tmp.Item2);
				//Console.WriteLine($"parsed: {string.Join(",", dict.Select(o => $"{o.Key}={o.Value}"))}");
			}
			//Console.WriteLine($"Got {msgs.Count} msgs: {string.Join("\n", msgs.Select(o => o.Substring(0, Math.Min(50, o.Length - 1))))}");
			await logWriter.Write(msgs);
			return Ok(msgs.Count);
        }

		public record PostDto(string[] Messages);
    }
}

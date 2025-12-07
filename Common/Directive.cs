namespace ProcServer.Controllers
{
    public class Directive
	{
		public required string Id { get; set; }
		public required DateTime Created { get; set; } = DateTime.UtcNow;
		public DateTime? Consumed { get; set; }
		//public required ICommand Command { get; set; }
		public required Dictionary<string, object> CommandData { get; set; }
	}

	public interface ICommand
	{
		string Name { get; }
		Task Execute();
		public static ICommand Create(Dictionary<string, object> data)
		{
			var type = data.GetValueOrDefault("Type") switch
			{
				"ShutDown" => typeof(ShutDownCommand),
				"Message" => typeof(MessageCommand),
				_ => throw new NotSupportedException($"Command type: {data.GetValueOrDefault("Type")}")
			};
			data.Remove("Type");
			var json = System.Text.Json.JsonSerializer.Serialize(data);
			var instance = System.Text.Json.JsonSerializer.Deserialize(json, type);
			if (instance is not ICommand command)
				throw new InvalidOperationException("Deserialized command is not ICommand");
			return command;
		}
	}

	public class ShutDownCommand : ICommand
	{
		public string Name => "ShutDown";
		public Task Execute()
		{
			return Task.CompletedTask;
		}
	}
	public class MessageCommand : ICommand
	{
		public string Name => "Message";
		public required string Sender { get; set; }
		public required string Message { get; set; }

		public Task Execute()
		{
			return Task.CompletedTask;
		}
	}
}

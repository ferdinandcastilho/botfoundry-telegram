namespace BotFoundry.Telegram.Abstractions.Models
{
    public sealed class FlowResult
    {
        public bool IsComplete { get; private set; }
        public static FlowResult Continue() => new() { IsComplete = false };
        public static FlowResult Complete() => new() { IsComplete = true };
    }
}

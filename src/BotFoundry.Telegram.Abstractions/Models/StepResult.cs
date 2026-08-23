namespace BotFoundry.Telegram.Abstractions.Models
{
    public enum StepResultKind { Continue, Complete, JumpTo }

    public sealed class StepResult
    {
        public StepResultKind Kind { get; private set; }
        public string? TargetId { get; private set; }
        public static StepResult Continue() => new() { Kind = StepResultKind.Continue };
        public static StepResult Complete() => new() { Kind = StepResultKind.Complete };
        public static StepResult JumpTo(string stepId) => new() { Kind = StepResultKind.JumpTo, TargetId = stepId };
    }
}

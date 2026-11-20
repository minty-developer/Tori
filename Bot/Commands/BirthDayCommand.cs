using Discord.Interactions;

public class BirthdayCommands(ILogger<BirthdayCommands> logger) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<BirthdayCommands> _logger = logger;
}
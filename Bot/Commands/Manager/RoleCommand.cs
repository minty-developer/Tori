using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

[Group("역할", "유저의 역할을 관리합니다.")]
public class RoleCommands(ILogger<RoleCommands> logger, Util util) : InteractionModuleBase<SocketInteractionContext>
{
    private ILogger<RoleCommands> _logger = logger;
    private Util _util = util;

    [SlashCommand("추가", "특정 유저에게 역할을 부여합니다.")]
    public async Task AddRoleAsync(
        [Summary(description: "대상 유저")] SocketGuildUser targetUser,
        [Summary(description: "역할")] SocketRole targetRole
    )
    {
        try
        {
            if (Context.User is not IGuildUser user)
            {
                await RespondAsync("서버에서만 사용할 수 있는 명령어야!", ephemeral: true);
                return;
            }

            if (!user.GuildPermissions.Administrator)
            {
                await RespondAsync("관리자만 사용할 수 있는 명령어야!", ephemeral: true);
                return;
            }

            await targetUser.AddRoleAsync(targetRole);
            await RespondAsync($"✅ **{targetUser.Username}**님에게 **{targetRole.Name}** 역할을 부여했어!", ephemeral: true);
            _logger.LogInformation("{Admin}님이 {Target}님에게 {Role} 역할을 부여했습니다.", Context.User.Username, targetUser.Username, targetRole.Name);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync("역할관리", ex, "역할을 관리하던 중에 에러가 났습니다.", _logger);
        }
    }

    [SlashCommand("삭제", "특정 유저에게서 역할을 제거합니다.")]
    public async Task RemoveRoleAsync(
        [Summary(description: "대상 유저")] SocketGuildUser targetUser,
        [Summary(description: "역할")] SocketRole targetRole
    )
    {
        try
        {
            if (Context.User is not IGuildUser user)
            {
                await RespondAsync("서버에서만 사용할 수 있는 명령어야!", ephemeral: true);
                return;
            }

            if (!user.GuildPermissions.Administrator)
            {
                await RespondAsync("관리자만 사용할 수 있는 명령어야!", ephemeral: true);
                return;
            }

            if (!targetUser.Roles.Any(r => r.Id == targetRole.Id))
            {
                await RespondAsync("이 유저는 그 역할이 없는걸?", ephemeral: true);
                return;
            }

            await targetUser.RemoveRoleAsync(targetRole);
            await RespondAsync($"✅ **{targetUser.Username}**님의 **{targetRole.Name}** 역할을 회수했어!", ephemeral: true);
            _logger.LogInformation("{Admin}님이 {Target}님의 {Role} 역할을 회수했습니다.", Context.User.Username, targetUser.Username, targetRole.Name);
        }
        catch (Exception ex)
        {
            await _util.RespondErrorAsync("역할관리", ex, "역할을 관리하던 중에 에러가 났습니다.", _logger);
        }
    }

    [SlashCommand("리스트", "특정 유저의 역할을 표시합니다.")]
    public async Task ShowRolesAsync(
        [Summary(description: "대상 유저")] SocketGuildUser targetUser
    )
    {
        try
        {
            if (Context.User is not IGuildUser user)
            {
                await RespondAsync("서버에서만 사용할 수 있는 명령어야!", ephemeral: true);
                return;
            }

            if (!user.GuildPermissions.Administrator)
            {
                await RespondAsync("관리자만 사용할 수 있는 명령어야!", ephemeral: true);
                return;
            }

            StringBuilder roles = new();
            foreach(var role in targetUser.Roles)
            {
                roles.Append($"- {role.Name}\n");
            }
            var embed = new EmbedBuilder()
                .WithTitle($"{targetUser.Nickname}님의 역할")
                .AddField("리스트", roles.ToString())
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);
        } catch (Exception ex)
        {
            await _util.RespondErrorAsync("역할관리", ex, "역할을 관리하던 중에 에러가 났습니다.", _logger);
        }
    }
}
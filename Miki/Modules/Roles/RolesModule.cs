﻿using IA;
using IA.Events.Attributes;
using IA.SDK.Events;
using IA.SDK.Interfaces;
using Miki.Models;
using Miki.Utility.MML;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miki.Modules.Roles
{
	[Module(Name = "Role Management")]
	class RolesModule
	{
		#region commands
		[Command(Name = "iam")]
		public async Task IAmAsync(EventContext e)
		{
			IDiscordRole role = GetRoleByName(e.Guild, e.arguments);

			if(role == null)
			{
				await e.ErrorEmbed(e.Channel.GetLocale().GetString("error_role_null"))
					.SendToChannel(e.Channel);
				return;
			}

			if (e.Author.RoleIds.Contains(role.Id))
			{
				await e.ErrorEmbed(e.Channel.GetLocale().GetString("error_role_already_given"))
					.SendToChannel(e.Channel);
				return;
			}

			using (var context = new MikiContext())
			{
				LevelRole newRole = await context.LevelRoles.FindAsync(e.Guild.Id.ToDbLong(), role.Id.ToDbLong());
				User user = await context.Users.FindAsync(e.Author.Id.ToDbLong());

				if (newRole == null || newRole.RequiredLevel > user.Level)
				{
					await e.ErrorEmbed(e.Channel.GetLocale().GetString("error_role_forbidden"))
						.SendToChannel(e.Channel);
					return;
				}

				await e.Author.RemoveRoleAsync(newRole.Role);

				await Utils.Embed
					.SetTitle("I AM")
					.SetDescription($"You're a(n) {role.Name} now!")
					.SendToChannel(e.Channel);
			}
		}

		[Command(Name = "iamnot")]
		public async Task IAmNotAsync(EventContext e)
		{
			IDiscordRole role = GetRoleByName(e.Guild, e.arguments);

			if (role == null)
			{
				await e.ErrorEmbed(e.Channel.GetLocale().GetString("error_role_null"))
					.SendToChannel(e.Channel);
				return;
			}

			if (!e.Author.RoleIds.Contains(role.Id))
			{
				await e.ErrorEmbed(e.Channel.GetLocale().GetString("error_role_forbidden"))
					.SendToChannel(e.Channel);
				return;
			}

			using (var context = new MikiContext())
			{
				LevelRole newRole = await context.LevelRoles.FindAsync(e.Guild.Id.ToDbLong(), role.Id.ToDbLong());
				User user = await context.Users.FindAsync(e.Author.Id.ToDbLong());

				await e.Author.AddRoleAsync(newRole.Role);

				await Utils.Embed
					.SetTitle("I AM NOT")
					.SetDescription($"You're no longer a(n) {role.Name}!")
					.SendToChannel(e.Channel);
			}
		}

		[Command(Name = "iamlist")]
		public async Task IAmListAsync(EventContext e)
		{
			using (var context = new MikiContext())
			{
				long guildId = e.Guild.Id.ToDbLong();
				List<LevelRole> roles = await context.LevelRoles
					.Where(x => x.GuildId == guildId)
					.OrderBy(x => x.RoleId)
					.Skip(0)
					.Take(25)
					.ToListAsync();

				StringBuilder stringBuilder = new StringBuilder();

				foreach(var role in roles)
				{
					if(role.Optable)
					{
						if(role.Role == null)
						{
							context.LevelRoles.Remove(role);
						}
						stringBuilder.Append(role.Role.Name.PadRight(20));
						if(role.RequiredLevel > 0)
						{
							stringBuilder.Append($"{role.RequiredLevel}⭐ ");
						}
						if(role.Automatic)
						{
							stringBuilder.Append($"⚙️");
						}
						stringBuilder.AppendLine();
					}
				}

				if(stringBuilder.Length == 0)
				{
					stringBuilder.Append(e.Channel.GetLocale().GetString("miki_placeholder_null"));
				}

				await context.SaveChangesAsync();
					
				await Utils.Embed
					.SetTitle("Available Roles")
					.SetDescription(stringBuilder.ToString())
					.SendToChannel(e.Channel);
			}
		}

		[Command(Name = "test", Accessibility = IA.SDK.EventAccessibility.DEVELOPERONLY)]
		public async Task TestAsync(EventContext e)
		{
			int index = int.Parse(e.arguments.Split(' ')[0]);
			int value = int.Parse(e.arguments.Split(' ')[1]);
			using (var context = new MikiContext())
			{
				PinnedBadges p = await context.PinnedBadges.FindAsync(e.Author.Id.ToDbLong());
				p.SetPinnedBadge(index, value);
				await context.SaveChangesAsync();
			}
		}

		[Command(Name = "configrole", Accessibility = IA.SDK.EventAccessibility.ADMINONLY)]
		public async Task ConfigRoleAsync(EventContext e)
		{
			using (var context = new MikiContext())
			{
				Dictionary<string, object> arguments = new MMLParser(e.arguments).Parse()
					.ToDictionary(x => x.Key, x => x.Value);

				IDiscordRole role = GetRoleByName(e.Guild, e.arguments.Split('"')[1]);
				LevelRole newRole = await context.LevelRoles.FindAsync(e.Guild.Id.ToDbLong(), role.Id.ToDbLong());

				if(role.Name.Length > 20)
				{
					await e.ErrorEmbed("Please keep role names below 20 letters.")
						.SendToChannel(e.Channel);
					return;
				}

				if(newRole == null)
				{
					newRole = context.LevelRoles.Add(new LevelRole()
					{
						GuildId = (e.Guild.Id.ToDbLong()),
						RoleId = (role.Id.ToDbLong()),
					});
				}

				if (arguments.ContainsKey("-automatic"))
				{
					newRole.Automatic = true;
				}

				if(arguments.ContainsKey("-optable"))
				{
					newRole.Optable = true;
				}

				if (arguments.ContainsKey("-level-required"))
				{
					newRole.RequiredLevel = (int)arguments["-level-required"];
				}

				await context.SaveChangesAsync();
				await Utils.Embed
					.SetTitle("Role Config")
					.SetDescription($"Updated {role.Name}!")
					.SendToChannel(e.Channel);
			}


		}

		[Command(Name = "abr", Accessibility = IA.SDK.EventAccessibility.DEVELOPERONLY)]
		public async Task SerializeAsync(EventContext e)
		{
			using (var context = new MikiContext())
			{
				context.BadgeResources.Add(MMLParser.Serialize<BadgeResources>(e.arguments));
				await context.SaveChangesAsync();
			}
			await e.Channel.SendMessage(":ok:");
		}
		#endregion

		public IDiscordRole GetRoleByName(IDiscordGuild guild, string roleName)
		{
			IDiscordRole role = guild.Roles.Where(x => x.Name.ToLower() == roleName.ToLower()).FirstOrDefault();
			return role;
		}
	}
}

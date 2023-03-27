namespace PrincessRTFM.PositionalGuide;

using System;
using System.Numerics;

using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;

using ImGuiNET;

public class Plugin: IDalamudPlugin {
	public const string Command = "/posguide";

	private bool disposed;

	public string Name { get; } = "Positional Assistant";

	[PluginService] public static GameGui Gui { get; private set; } = null!;
	[PluginService] public static ChatGui Chat { get; private set; } = null!;
	[PluginService] public static DalamudPluginInterface Interface { get; private set; } = null!;
	[PluginService] public static CommandManager Commands { get; private set; } = null!;
	[PluginService] public static ClientState Client { get; private set; } = null!;
	[PluginService] public static TargetManager Targets { get; private set; } = null!;

	public Configuration Config { get; private set; }

	private readonly WindowSystem windowSystem;
	private readonly ConfigWindow configWindow;

	public Plugin() {
		this.Config = Interface.GetPluginConfig() as Configuration ?? new();

		this.configWindow = new(this);
		this.windowSystem = new(this.GetType().Namespace!);
		this.windowSystem.AddWindow(this.configWindow);

		Commands.AddHandler(Command, new(this.onPluginCommand) {
			HelpMessage = $"Open {this.Name}'s config window",
			ShowInHelp = true,
		});

		Interface.UiBuilder.OpenConfigUi += this.toggleConfigUi;
		Interface.UiBuilder.Draw += this.draw;
	}

	internal void draw() {
		this.windowSystem.Draw();

		// If positionals matter in PVP, I won't help you. If they don't, I won't distract you.
		if (Client.IsPvP)
			return;

		if (!this.Config.Enabled)
			return;

		if (Targets.Target is not BattleChara target)
			return;

		if (target.ObjectKind is ObjectKind.Player) {
			if (!this.Config.DrawOnPlayers)
				return;
		}

		ImGuiHelpers.ForceNextWindowMainViewport();
		ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos);
		ImGui.SetNextWindowSize(ImGui.GetMainViewport().Size);

		if (!ImGui.Begin("###PositionalGuidelineOverlay",
			ImGuiWindowFlags.NoDecoration
			| ImGuiWindowFlags.NoSavedSettings
			| ImGuiWindowFlags.NoMove
			| ImGuiWindowFlags.NoInputs
			| ImGuiWindowFlags.NoFocusOnAppearing
			| ImGuiWindowFlags.NoBackground
			| ImGuiWindowFlags.NoNav)
		) {
			return;
		}

		bool limitEither = this.Config.OnlyRenderWhenEitherEndOnScreen;
		bool limitInner = this.Config.OnlyRenderWhenCentreOnScreen;
		bool limitOuter = this.Config.OnlyRenderWhenEndpointOnScreen;
		Vector3 pos = target.Position;
		float y = pos.Y;
		float angle = -target.Rotation;
		float length = Math.Min(this.Config.SoftMaxRange, Math.Max(this.Config.SoftMinRange, target.HitboxRadius + this.Config.SoftDrawRange));
		ImDrawListPtr drawing = ImGui.GetWindowDrawList();

		bool targetOnScreen = Gui.WorldToScreen(pos, out Vector2 centre);
		if (!targetOnScreen && !limitEither && limitInner)
			return;

		// +X = east, -X = west
		// +Z = south, -Z = north
		Vector3 basePoint = pos + new Vector3(0, 0, length);

		for (int i = 0; i < 8; ++i) {
			if (!this.Config.DrawGuides[i])
				continue;

			Vector3 rotated = rotatePoint(pos, basePoint, angle + deg2rad(i * 45), y);
			bool endpointOnScreen = Gui.WorldToScreen(rotated, out Vector2 coord);

			if (limitEither) {
				if (!targetOnScreen && !endpointOnScreen)
					continue;
			}
			else if (limitOuter && !endpointOnScreen) {
				continue;
			}

			drawing.AddLine(centre, coord, ImGui.GetColorU32(this.Config.LineColours[i]), this.Config.LineThickness);
		}

		if (this.Config.DrawTetherLine) {
			if (Plugin.Client.LocalPlayer is not null) {
				bool playerOnScreen = Gui.WorldToScreen(Plugin.Client.LocalPlayer.Position, out Vector2 player);
				bool draw = true;

				if (limitEither) {
					if (!targetOnScreen && !playerOnScreen)
						draw = false;
				}
				else if (limitOuter && !playerOnScreen) {
					draw = false;
				}

				if (draw)
					drawing.AddLine(centre, player, ImGui.GetColorU32(this.Config.TetherColour), this.Config.LineThickness);
			}
		}

		ImGui.End();
	}

	private static float deg2rad(float degrees) => (float)(degrees * Math.PI / 180);

	private static Vector3 rotatePoint(Vector3 centre, Vector3 originalPoint, double angleRadians, float y) {
		// Adapted (read: shamelessly stolen) from https://github.com/PunishedPineapple/Distance

		Vector3 translatedOriginPoint = originalPoint - centre;
		float distance = new Vector2(translatedOriginPoint.X, translatedOriginPoint.Z).Length();
		double translatedAngle = Math.Atan2(translatedOriginPoint.Z, translatedOriginPoint.X);

		return new(
			((float)Math.Cos(translatedAngle + angleRadians) * distance) + centre.X,
			y,
			((float)Math.Sin(translatedAngle + angleRadians) * distance) + centre.Z
		);
	}

	internal void onPluginCommand(string command, string arguments) {
		string[] args = arguments.Trim().Split();

		string action = args.Length >= 1 ? args[0].ToLower() : "config";
		string target = args.Length >= 2 ? args[1].ToLower() : "render";
		if (string.IsNullOrEmpty(action))
			action = "config";

		bool? state;

		switch (action) {
			case "enable":
				state = true;
				break;
			case "disable":
				state = false;
				break;
			case "toggle":
				state = null;
				break;
			case "config":
				this.toggleConfigUi();
				return;
			case "swap":
				target = "all";
				state = null;
				break;
			default:
				Chat.PrintError($"Unknown action '{args[0]}'");
				return;
		}

		switch (target) {
			case "f":
			case "front":
				this.Config.DrawFront = state ?? !this.Config.DrawFront;
				break;
			case "fr":
			case "frontright":
			case "front-right":
				this.Config.DrawFrontRight = state ?? !this.Config.DrawFrontRight;
				break;
			case "r":
			case "right":
				this.Config.DrawRight = state ?? !this.Config.DrawRight;
				break;
			case "br":
			case "backright":
			case "back-right":
				this.Config.DrawBackRight = state ?? !this.Config.DrawBackRight;
				break;
			case "b":
			case "back":
				this.Config.DrawBack = state ?? !this.Config.DrawBack;
				break;
			case "bl":
			case "backleft":
			case "back-left":
				this.Config.DrawBackLeft = state ?? !this.Config.DrawBackLeft;
				break;
			case "l":
			case "left":
				this.Config.DrawLeft = state ?? !this.Config.DrawLeft;
				break;
			case "fl":
			case "frontleft":
			case "front-left":
				this.Config.DrawFrontLeft = state ?? !this.Config.DrawFrontLeft;
				break;
			case "cardinal":
			case "cardinals":
				this.Config.DrawFront = state ?? !this.Config.DrawFront;
				this.Config.DrawRight = state ?? !this.Config.DrawRight;
				this.Config.DrawBack = state ?? !this.Config.DrawBack;
				this.Config.DrawLeft = state ?? !this.Config.DrawLeft;
				break;
			case "diagonal":
			case "diagonals":
				this.Config.DrawFrontRight = state ?? !this.Config.DrawFrontRight;
				this.Config.DrawBackRight = state ?? !this.Config.DrawBackRight;
				this.Config.DrawBackLeft = state ?? !this.Config.DrawBackLeft;
				this.Config.DrawFrontLeft = state ?? !this.Config.DrawFrontLeft;
				break;
			case "all":
				this.Config.DrawFront = state ?? !this.Config.DrawFront;
				this.Config.DrawRight = state ?? !this.Config.DrawRight;
				this.Config.DrawBack = state ?? !this.Config.DrawBack;
				this.Config.DrawLeft = state ?? !this.Config.DrawLeft;
				this.Config.DrawFrontRight = state ?? !this.Config.DrawFrontRight;
				this.Config.DrawBackRight = state ?? !this.Config.DrawBackRight;
				this.Config.DrawBackLeft = state ?? !this.Config.DrawBackLeft;
				this.Config.DrawFrontLeft = state ?? !this.Config.DrawFrontLeft;
				break;
			case "tether":
				this.Config.DrawTetherLine = state ?? !this.Config.DrawTetherLine;
				Interface.UiBuilder.AddNotification($"Tether rendering {(this.Config.DrawTetherLine ? "enabled" : "disabled")}", this.Name, NotificationType.Info);
				break;
			case "render":
				this.Config.Enabled = state ?? !this.Config.Enabled;
				Interface.UiBuilder.AddNotification($"Guide rendering {(this.Config.Enabled ? "enabled" : "disabled")}", this.Name, NotificationType.Info);
				break;
			default:
				Chat.PrintError($"Unknown target '{args[1]}'");
				return;
		}

		Interface.SavePluginConfig(this.Config);
	}

	internal void toggleConfigUi() {
		if (this.configWindow is not null) {
			this.configWindow.IsOpen = !this.configWindow.IsOpen;
		}
		else {
			PluginLog.Error("Cannot toggle configuration window, reference does not exist");
		}
	}

	#region Disposable
	protected virtual void Dispose(bool disposing) {
		if (this.disposed)
			return;
		this.disposed = true;

		if (disposing) {
			Interface.UiBuilder.OpenConfigUi -= this.toggleConfigUi;
			Interface.UiBuilder.Draw -= this.draw;

			Commands.RemoveHandler(Command);

			this.configWindow.Dispose();
		}
	}

	public void Dispose() {
		this.Dispose(true);
		GC.SuppressFinalize(this);
	}
	#endregion
}

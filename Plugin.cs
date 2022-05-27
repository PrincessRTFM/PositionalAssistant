namespace PositionalGuide;

using System;
using System.Numerics;

using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;

using ImGuiNET;

public class Plugin: IDalamudPlugin {
	public const string Command = "/posguide";

	private bool disposed;

	public string Name { get; } = "Positional Helper";

	[PluginService] public static GameGui Gui { get; private set; } = null!;
	[PluginService] public static DalamudPluginInterface Interface { get; private set; } = null!;
	[PluginService] public static CommandManager Commands { get; private set; } = null!;
	[PluginService] public static ClientState Client { get; private set; } = null!;
	[PluginService] public static TargetManager Targets { get; private set; } = null!;

	public Configuration Config;

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
		// If positionals matter in PVP, I won't help you. If they don't, I won't distract you.
		if (Client.IsPvP)
			return;

		this.windowSystem.Draw();

		if (Targets.Target is not BattleChara target)
			return;

		if (target.ObjectKind is ObjectKind.Player) // TODO make this configurable?
			return;

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

		Vector3 pos = target.Position;
		float y = pos.Y;
		float angle = target.Rotation;
		float length = Math.Min(this.Config.SoftMaxRange, Math.Max(this.Config.SoftMinRange, target.HitboxRadius + this.Config.SoftDrawRange));
		ImDrawListPtr drawing = ImGui.GetWindowDrawList();

		Gui.WorldToScreen(pos, out Vector2 centre);

		// +X = east, -X = west
		// +Z = south, -Z = north
		Vector3 north = pos + new Vector3(0, 0, -length);

		for (int i = 0; i < 8; ++i) {
			if (!this.Config.DrawGuides[i])
				continue;

			Vector3 rotated = rotatePoint(pos, north, -angle + deg2rad(i * 45), y);
			Gui.WorldToScreen(rotated, out Vector2 coord);
			drawing.AddLine(centre, coord, ImGui.GetColorU32(this.Config.LineColours[i]), this.Config.LineThickness);
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
		string[] argumentsParts = arguments.Split();

		switch (argumentsParts[0].ToLower()) {
			// TODO subcommands
			default:
				this.toggleConfigUi();
				break;
		}
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

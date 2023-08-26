namespace PrincessRTFM.PositionalGuide;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Numerics;

using ImGuiNET;

public class Plugin: IDalamudPlugin {
	public const string Command = "/posguide";

	public const double ArcLength = 360 / 16;
	public const int ArcSegmentCount = 8;

	private bool disposed;

	public string Name { get; } = "Positional Assistant";

	[PluginService] public static IGameGui Gui { get; private set; } = null!;
	[PluginService] public static ChatGui Chat { get; private set; } = null!;
	[PluginService] public static DalamudPluginInterface Interface { get; private set; } = null!;
	[PluginService] public static ICommandManager Commands { get; private set; } = null!;
	[PluginService] public static IClientState Client { get; private set; } = null!;
	[PluginService] public static ITargetManager Targets { get; private set; } = null!;

	public Configuration Config { get; private set; }

	private readonly WindowSystem windowSystem;
	private readonly ConfigWindow configWindow;

	public Plugin() {
		this.Config = Interface.GetPluginConfig() as Configuration ?? new();
		this.Config.Update();

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

		if (Client.LocalPlayer is not PlayerCharacter player)
			return;

		if (target.ObjectKind is ObjectKind.Player) {
			if (!this.Config.DrawOnPlayers)
				return;
			if (target != Client.LocalPlayer && this.Config.DrawOnSelfOnly)
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

		Vector3 playerPos = player.Position;
		Vector3 targetPos = target.Position;

		// no, I don't know why it needs to be negated, but everything rotates the wrong way if it's not
		float targetFacing = -target.Rotation;

		// this is the length of the guidelines, no relation to the tether
		float length = Math.Min(this.Config.SoftMaxRange, Math.Max(this.Config.SoftMinRange, target.HitboxRadius + this.Config.SoftDrawRange));

		ImDrawListPtr drawing = ImGui.GetWindowDrawList();

		// we use `centre` later to rotate points, but if we're supposed to limit to when the TARGET is on screen and they aren't, then we stop early
		bool targetOnScreen = Gui.WorldToScreen(targetPos, out Vector2 centre);
		if (!targetOnScreen && !limitEither && limitInner)
			return;

		float arcRadius = target.HitboxRadius;

		// this is where we render the lines and the circle segments, which used to be simpler when it was just the lines
		for (int arcIndex = 0; arcIndex < 16; ++arcIndex) {
			int line = (arcIndex - (arcIndex % 2)) / 2;
			bool drawingLine = arcIndex % 2 == 0;

			// +X = east, -X = west
			// +Z = south, -Z = north
			Vector3 guidelineBasePoint = targetPos + new Vector3(0, 0, length);
			Vector3 arcBasePoint = targetPos + new Vector3(0, 0, target.HitboxRadius);

			// this is basically the same as the old setup for drawing the lines, working on every other iteration of this loop
			if (drawingLine && this.Config.DrawGuides[line]) {

				// this is the WORLD coordinate for the outer endpoint for the current guideline
				Vector3 rotated = rotatePoint(targetPos, guidelineBasePoint, targetFacing + deg2rad(line * 45));
				bool endpointOnScreen = Gui.WorldToScreen(rotated, out Vector2 coord);

				// depending on the render constraints, we may not actually want to draw anything
				if (limitEither) {
					if (!targetOnScreen && !endpointOnScreen)
						continue;
				}
				else if (limitOuter && !endpointOnScreen) {
					continue;
				}

				drawing.AddLine(centre, coord, ImGui.GetColorU32(this.Config.LineColours[line]), this.Config.LineThickness);
			}

			if (this.Config.DrawCircle) {
				Vector4 arcColour = this.Config.LineColours[Configuration.IndexCircle];

				// we basically bounce back and forth, starting at the arc's adjacent line and then going left (negative) when the arc is on the left or right (positive) otherwise,
				// then flipping repeatedly until we either find an enabled line to pull the colour from, or cover all of the lines and find nothing
				int sign = drawingLine ? 1 : -1; // inverted from the above description because the first loop uses 0, so this is flipped once before it has any effect
				int arcColourLine = line;
				for (int offset = 0; offset < Configuration.IndexCircle; ++offset) {
					arcColourLine += offset * sign;
					sign *= -1;
					if (arcColourLine >= Configuration.IndexCircle)
						arcColourLine %= Configuration.IndexCircle;
					else if (arcColourLine < 0)
						arcColourLine = Configuration.IndexCircle + arcColourLine; // add because it's negative
					if (this.Config.DrawGuides[arcColourLine]) {
						arcColour = this.Config.LineColours[arcColourLine];
						break;
					}
				}

				// unfortunately, ImGui doesn't actually offer a way to draw arcs, so we're gonna have to do it manually... by drawing a series of line segments
				// which means we need to calculate the endpoints for those line segments, along the arc, based on the calculated endpoints of the arc
				// we start with the endpoints of the arc itself here
				double endpointOffsetLeft = ((arcIndex - 1) * ArcLength) + (arcIndex / 2d);
				double endpointOffsetRight = (arcIndex * ArcLength) + (arcIndex / 2d);
				double totalArcRadians = deg2rad(endpointOffsetRight - endpointOffsetLeft);
				Vector3 arcEndpointLeft = rotatePoint(targetPos, arcBasePoint, targetFacing + deg2rad(endpointOffsetLeft));
				Vector3 arcEndpointRight = rotatePoint(targetPos, arcBasePoint, targetFacing + deg2rad(endpointOffsetRight));

				// only render the arc segments if the entire arc is on screen
				bool renderingArc = true;
				renderingArc &= Gui.WorldToScreen(arcEndpointLeft, out Vector2 screenEndpointLeft);
				renderingArc &= Gui.WorldToScreen(arcEndpointRight, out Vector2 screenEndpointRight);

				if (renderingArc) {
					Vector2[] points = arcPoints(targetPos, arcEndpointLeft, totalArcRadians, ArcSegmentCount + 1)
						.Select(world => { Gui.WorldToScreen(world, out Vector2 screen); return screen; })
						.ToArray();
					for (int i = 1; i < points.Length; ++i)
						drawing.AddLine(points[i - 1], points[i], ImGui.GetColorU32(arcColour), this.Config.LineThickness + 2);
				}
			}
		}

		// the tether line itself used to be pretty simple: from our position to the target's
		// then I decided to allow controlling the maximum length of it and suddenly everything got Complicated
		if (this.Config.DrawTetherLine && target != player) {

			// radians between DUE SOUTH (angle=0) and PLAYER POSITION using TARGET POSITION as the vertex
			double angleToPlayer = angleBetween(targetPos, targetPos + Vector3.UnitZ, playerPos);

			// radians between DUE SOUTH (angle=0) and TARGET POSITION using PLAYER POSITION as the vertex
			double angleToTarget = angleBetween(playerPos, playerPos + Vector3.UnitZ, targetPos);

			// the scalar offset from the target position towards their target ring, in the direction DUE SOUTH (angle=0)
			// if set distance is -1, point is the centre of the target, so offset is 0
			float offset = this.Config.TetherLengthInner == -1
				? 0
				: Math.Max(target.HitboxRadius - this.Config.SoftInnerTetherLength, 0);

			// the world position for the tether's inner point, as the offset point rotated around the centre to face the player
			Vector3 tetherInner = offset == 0
				? targetPos
				: rotatePoint(targetPos, targetPos + new Vector3(0, 0, offset), angleToPlayer);

			// the world position for the tether's outer point, extending the set distance from the target's ring in the direction of the player
			// or, if set distance is -1, to the player's centre; if -2, to the edge of the player's ring
			Vector3 tetherOuter = this.Config.TetherLengthOuter switch {
				-2 => rotatePoint(playerPos, playerPos + new Vector3(0, 0, player.HitboxRadius), angleToTarget),
				-1 => playerPos,
				_ => rotatePoint(targetPos, targetPos + new Vector3(0, 0, target.HitboxRadius + this.Config.SoftOuterTetherLength), angleToPlayer).WithY(playerPos.Y),
			};

			if (this.Config.FlattenTether)
				tetherOuter.Y = tetherInner.Y;

			// check whether we're allowed to render, in case points are off screen
			bool insideOnScreen = Gui.WorldToScreen(tetherInner, out Vector2 inner);
			bool outsideOnScreen = Gui.WorldToScreen(tetherOuter, out Vector2 outer);
			bool draw = true;
			if (limitEither) {
				if (!insideOnScreen && !outsideOnScreen)
					draw = false;
			}
			else {
				if (limitInner && !insideOnScreen)
					draw = false;
				if (limitOuter && !outsideOnScreen)
					draw = false;
			}

			// and finally, draw the line (but only if we're allowed to)
			if (draw)
				drawing.AddLine(inner, outer, ImGui.GetColorU32(this.Config.TetherColour), this.Config.LineThickness);
		}

		ImGui.End();
	}

	private static double deg2rad(double degrees) => degrees * Math.PI / 180;

	private static Vector2 rotatePoint(Vector2 centre, Vector2 originalPoint, double angleRadians) {
		// Adapted (read: shamelessly stolen) from https://github.com/PunishedPineapple/Distance

		Vector2 translatedOriginPoint = originalPoint - centre;
		float distance = new Vector2(translatedOriginPoint.X, translatedOriginPoint.Y).Length();
		double translatedAngle = Math.Atan2(translatedOriginPoint.Y, translatedOriginPoint.X);

		return new(
			((float)Math.Cos(translatedAngle + angleRadians) * distance) + centre.X,
			((float)Math.Sin(translatedAngle + angleRadians) * distance) + centre.Y
		);
	}
	private static Vector3 rotatePoint(Vector3 centre, Vector3 originalPoint, double angleRadians) {
		Vector2 rotated = rotatePoint(new Vector2(centre.X, centre.Z), new Vector2(originalPoint.X, originalPoint.Z), angleRadians);
		return new(rotated.X, centre.Y, rotated.Y);
	}

	private static double angleBetween(Vector2 vertex, Vector2 a, Vector2 b) => Math.Atan2(b.Y - vertex.Y, b.X - vertex.X) - Math.Atan2(a.Y - vertex.Y, a.X - vertex.X);
	private static double angleBetween(Vector3 vertex, Vector3 a, Vector3 b) => angleBetween(new Vector2(vertex.X, vertex.Z), new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

	private static IEnumerable<Vector2> arcPoints(Vector2 centre, Vector2 start, double totalAngle, int count) {
		double angleStep = totalAngle / (count - 1);
		for (int i = 0; i < count; i++)
			yield return rotatePoint(centre, start, angleStep * i);
	}
	private static IEnumerable<Vector3> arcPoints(Vector3 centre, Vector3 start, double totalAngle, int count)
		=> arcPoints(new Vector2(centre.X, centre.Z), new Vector2(start.X, start.Z), totalAngle, count).Select(v2 => new Vector3(v2.X, centre.Y, v2.Y));

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
			case "lines":
				this.Config.DrawFront = state ?? !this.Config.DrawFront;
				this.Config.DrawRight = state ?? !this.Config.DrawRight;
				this.Config.DrawBack = state ?? !this.Config.DrawBack;
				this.Config.DrawLeft = state ?? !this.Config.DrawLeft;
				this.Config.DrawFrontRight = state ?? !this.Config.DrawFrontRight;
				this.Config.DrawBackRight = state ?? !this.Config.DrawBackRight;
				this.Config.DrawBackLeft = state ?? !this.Config.DrawBackLeft;
				this.Config.DrawFrontLeft = state ?? !this.Config.DrawFrontLeft;
				break;
			case "c":
			case "circle":
				this.Config.DrawCircle = state ?? !this.Config.DrawCircle;
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
				this.Config.DrawCircle = state ?? !this.Config.DrawCircle;
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

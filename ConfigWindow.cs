namespace PositionalGuide;

using System;
using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Interface.Windowing;

using ImGuiNET;

public class ConfigWindow: Window, IDisposable {
	private const ImGuiWindowFlags flags = ImGuiWindowFlags.None
		| ImGuiWindowFlags.NoScrollbar
		| ImGuiWindowFlags.NoScrollWithMouse
		| ImGuiWindowFlags.AlwaysAutoResize;
	private const int ptrMemWidth = sizeof(short);

	private readonly IntPtr stepPtr;
	private readonly IntPtr minBoundingPtr;
	private readonly IntPtr maxBoundingPtr;
	private readonly IntPtr minModifierPtr;
	private readonly IntPtr maxModifierPtr;
	private readonly IntPtr minThicknessPtr;
	private readonly IntPtr maxThicknessPtr;

	private bool disposed;

	private readonly Configuration conf;

	public ConfigWindow(Plugin core) : base(core.Name, flags) {
		this.RespectCloseHotkey = true;
		this.conf = core.Config;
		this.stepPtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.minBoundingPtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.maxBoundingPtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.minModifierPtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.maxModifierPtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.minThicknessPtr = Marshal.AllocHGlobal(ptrMemWidth);
		this.maxThicknessPtr = Marshal.AllocHGlobal(ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes((short)1), 0, this.stepPtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes(byte.MinValue), 0, this.minBoundingPtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes(byte.MaxValue), 0, this.maxBoundingPtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes(sbyte.MinValue), 0, this.minModifierPtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes(sbyte.MaxValue), 0, this.maxModifierPtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes((short)1), 0, this.minThicknessPtr, ptrMemWidth);
		Marshal.Copy(BitConverter.GetBytes((short)5), 0, this.maxThicknessPtr, ptrMemWidth);
	}

	public override void Draw() {
		ImGuitils utils = new(40);
		bool changed = false;

		short[] hack = new short[] {
			this.conf.ExtraDrawRange,
			this.conf.MinDrawRange,
			this.conf.MaxDrawRange,
			this.conf.LineThickness,
		};
		bool[] drawing = this.conf.DrawGuides;
		bool[] limitRender = new bool[] {
			this.conf.OnlyRenderWhenEitherEndOnScreen,
			this.conf.OnlyRenderWhenCentreOnScreen,
			this.conf.OnlyRenderWhenEndpointOnScreen,
		};
		Vector4[] colours = this.conf.LineColours;

		IntPtr[] ptrs = new IntPtr[hack.Length];
		for (int i = 0; i < hack.Length; ++i) {
			ptrs[i] = Marshal.AllocHGlobal(ptrMemWidth);
			Marshal.Copy(BitConverter.GetBytes(hack[i]), 0, ptrs[i], ptrMemWidth);
		}

		bool active = this.conf.Enabled;
		bool tether = this.conf.DrawTetherLine;
		bool players = this.conf.DrawOnPlayers;
		Vector4 tetherColour = this.conf.TetherColour;

		if (Plugin.Client.IsPvP) {
			ImGui.TextUnformatted("You are currently in a PvP zone.");
			ImGui.TextUnformatted("This plugin does not function in PvP.");
			ImGui.Spacing();
			ImGui.Separator();
			ImGui.Spacing();
		}

		changed |= ImGui.Checkbox("Enabled?", ref active);
		utils.Tooltip("Whether to draw any lines at all");

#if DEBUG
		// Not available outside of debug mode because there's no point except for debugging
		// If you REALLY want to force it on, it's in the config file, just not the settings window
		changed |= ImGui.Checkbox("Players?", ref players);
		utils.Tooltip("Should guides be drawn on players?"
			+ "\n"
			+ "\nThis is a debug-build exclusive setting. PvP still isn't enabled.");
#endif

		changed |= ImGui.Checkbox("Tether line?", ref tether);
		utils.Tooltip("Should a line be drawn connecting you to your target?");
		ImGui.SameLine();
		changed |= ImGui.ColorEdit4("Line colour for the tether", ref tetherColour, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);

		ImGui.TextUnformatted("\nWhich guidelines do you want, and in what colours?");

		ImGui.Columns(3, "###drawControls", false);
		foreach (int i in new int[] { 7, 0, 1, 6, 2, 5, 4, 3 }) {
			changed |= ImGui.Checkbox($"Show?###drawGuide{i}", ref drawing[i]);
			utils.Tooltip($"Draw the {Configuration.Directions[i]} guideline?");
			ImGui.SameLine();
			changed |= ImGui.ColorEdit4($"Line colour for {Configuration.Directions[i]}", ref colours[i], ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);
			ImGui.NextColumn();
			if (i is 6)
				ImGui.NextColumn();
		}
		ImGui.Columns(1);

		changed |= ImGui.Checkbox("Only draw lines when at least one of the points is on screen?", ref limitRender[0]);
		utils.Tooltip("Some users have reported that lines being partially off-screen causes a visual bug, wherein the line just shoots off across your whole screen in a random direction."
				+ " This setting will stop individual lines from being drawn if neither point is on the screen."
				+ "\n"
				+ "\nThere may still be some weirdness in certain camera angles, but it should be much more limited."
				+ "\n"
				+ "\nThe following two options only apply if this one is disabled.");
		if (limitRender[0])
			ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.35f);
		ImGui.Indent();
		changed |= ImGui.Checkbox("Only draw lines when the target's centrepoint is on the screen?", ref limitRender[1]);
		ImGui.Unindent();
		if (limitRender[0])
			ImGui.PopStyleVar();
		utils.Tooltip("This setting will stop individual lines from being drawn if the inner point (the middle of the target's hitbox) isn't on your screen."
			+ "\n"
			+ "\nOnly applies if the above override option is disabled.");
		if (limitRender[0])
			ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.35f);
		ImGui.Indent();
		changed |= ImGui.Checkbox("Only draw lines when the line's endpoint is on the screen?", ref limitRender[2]);
		ImGui.Unindent();
		if (limitRender[0])
			ImGui.PopStyleVar();
		utils.Tooltip("Much like the above, this setting stops lines from drawing if their endpoint (at/past the outside of the target's hitbox) isn't on your screen."
				+ "\n"
				+ "\nOnly applies if the above override option is disabled.");

		changed |= ImGui.SliderScalar("Guideline size modifier", ImGuiDataType.S16, ptrs[0], this.minModifierPtr, this.maxModifierPtr, "%i", ImGuiSliderFlags.AlwaysClamp);
		utils.Tooltip("The positional guidelines normally extend from the target's centre point out to the size of their hitbox. This allows you to make them longer or shorter.");

		changed |= ImGui.SliderScalar("Minimum guideline size", ImGuiDataType.U16, ptrs[1], this.minBoundingPtr, this.maxBoundingPtr, "%i", ImGuiSliderFlags.AlwaysClamp);
		utils.Tooltip("Guidelines will always be drawn to at least this length, even if they would ordinarily have been smaller.");

		changed |= ImGui.SliderScalar("Maximum guideline size", ImGuiDataType.U16, ptrs[2], this.minBoundingPtr, this.maxBoundingPtr, "%i", ImGuiSliderFlags.AlwaysClamp);
		utils.Tooltip("Guidelines will never be longer than this, no matter how big the target's hitbox is.");

		changed |= ImGui.SliderScalar("Line thickness", ImGuiDataType.U16, ptrs[3], this.minThicknessPtr, this.maxThicknessPtr, "%i", ImGuiSliderFlags.AlwaysClamp);
		utils.Tooltip("How wide/thick do you want the lines to be?");

		ImGui.TextUnformatted("");
		ImGui.Separator();
		ImGui.TextUnformatted("");

		if (ImGui.CollapsingHeader("Command Help")) {
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 24);
			ImGui.TextUnformatted("Most of the above toggle settings can be changed via command:");
			ImGui.Indent();
			ImGui.TextUnformatted($"{Plugin.Command} <action> [target]");
			ImGui.Unindent();
			ImGui.TextUnformatted("");
			ImGui.TextUnformatted("Target may be any of the following, hyphyens optional, to toggle that line:");
			ImGui.Indent();
			ImGui.TextUnformatted("fl, front-left, f, front, fr, front-right, r, right, br, back-right, b, back, bl, back-left, l, left");
			ImGui.Unindent();
			ImGui.TextUnformatted("");
			ImGui.TextUnformatted("Additionally, you can use 'cardinal', 'cardinals', 'diagonal', 'diagonals', and 'all' to affect multiple lines with a single command.");
			ImGui.TextUnformatted("");
			ImGui.TextUnformatted("Finally, you can use the target 'tether' to toggle tether line rendering, and the target 'render' (the default) to toggle showing guides at all without losing your settings.");
			ImGui.PopTextWrapPos();
		}

		if (changed) {
			for (int i = 0; i < hack.Length; ++i)
				Marshal.Copy(ptrs[i], hack, i, 1);
			this.conf.Enabled = active;
			this.conf.DrawOnPlayers = players;
			this.conf.DrawTetherLine = tether;
			this.conf.TetherColour = tetherColour;
			this.conf.OnlyRenderWhenEitherEndOnScreen = limitRender[0];
			this.conf.OnlyRenderWhenCentreOnScreen = limitRender[1];
			this.conf.OnlyRenderWhenEndpointOnScreen = limitRender[2];
			this.conf.ExtraDrawRange = hack[0];
			this.conf.MinDrawRange = hack[1];
			this.conf.MaxDrawRange = hack[2];
			this.conf.LineThickness = hack[3];
			this.conf.DrawGuides = drawing;
			this.conf.LineColours = colours;
			Plugin.Interface.SavePluginConfig(this.conf);
		}

		for (int i = 0; i < ptrs.Length; ++i)
			Marshal.FreeHGlobal(ptrs[i]);
	}

	#region Disposable
	protected virtual void Dispose(bool disposing) {
		if (this.disposed)
			return;
		this.disposed = true;

		if (disposing) {
			// nop
		}

		Marshal.FreeHGlobal(this.stepPtr);
		Marshal.FreeHGlobal(this.minBoundingPtr);
		Marshal.FreeHGlobal(this.maxBoundingPtr);
		Marshal.FreeHGlobal(this.minModifierPtr);
		Marshal.FreeHGlobal(this.maxModifierPtr);
		Marshal.FreeHGlobal(this.minThicknessPtr);
		Marshal.FreeHGlobal(this.maxThicknessPtr);
	}

	~ConfigWindow() {
		this.Dispose(false);
	}

	public void Dispose() {
		this.Dispose(true);
		GC.SuppressFinalize(this);
	}
	#endregion
}

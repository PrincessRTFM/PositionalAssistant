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
		bool changed = false;

		short[] vals = new short[] {
			this.conf.ExtraDrawRange,
			this.conf.MinDrawRange,
			this.conf.MaxDrawRange,
			this.conf.LineThickness,
		};

		IntPtr[] ptrs = new IntPtr[vals.Length];
		for (int i = 0; i < vals.Length; ++i) {
			ptrs[i] = Marshal.AllocHGlobal(ptrMemWidth);
			Marshal.Copy(BitConverter.GetBytes(vals[i]), 0, ptrs[i], ptrMemWidth);
		}

		bool active = this.conf.Enabled;

		if (Plugin.Client.IsPvP) {
			ImGui.TextUnformatted("You are currently in a PvP zone.");
			ImGui.TextUnformatted("This plugin does not function in PvP.");
			ImGui.Spacing();
			ImGui.Separator();
			ImGui.Spacing();
		}

		changed |= ImGui.Checkbox("Enabled?", ref active);
		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 40);
			ImGui.TextUnformatted("Whether to draw any positional guides at all");
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}
		ImGui.TextUnformatted("");

		bool[] drawing = this.conf.DrawGuides;
		Vector4[] colours = this.conf.LineColours;
		ImGui.Columns(3, "###drawControls", false);
		foreach (int i in new int[] { 7, 0, 1, 6, 2, 5, 4, 3 }) {
			changed |= ImGui.Checkbox($"Show?###drawGuide{i}", ref drawing[i]);
			if (ImGui.IsItemHovered()) {
				ImGui.BeginTooltip();
				ImGui.TextUnformatted($"Draw the {Configuration.Directions[i]} guideline?");
				ImGui.EndTooltip();
			}
			ImGui.SameLine();
			changed |= ImGui.ColorEdit4($"Line colour for {Configuration.Directions[i]}", ref colours[i], ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);
			ImGui.NextColumn();
			if (i is 6)
				ImGui.NextColumn();
		}
		ImGui.Columns(1);

		changed |= ImGui.SliderScalar("Guideline size modifier", ImGuiDataType.S16, ptrs[0], this.minModifierPtr, this.maxModifierPtr, "%i", ImGuiSliderFlags.AlwaysClamp);
		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 40);
			ImGui.TextUnformatted("The positional guidelines normally extend from the target's centre point out to the size of their hitbox. This allows you to make them longer or shorter.");
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}

		changed |= ImGui.SliderScalar("Minimum guideline size", ImGuiDataType.U16, ptrs[1], this.minBoundingPtr, this.maxBoundingPtr, "%i", ImGuiSliderFlags.AlwaysClamp);
		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 40);
			ImGui.TextUnformatted("Guidelines will always be drawn to at least this length, even if they would ordinarily have been smaller.");
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}

		changed |= ImGui.SliderScalar("Maximum guideline size", ImGuiDataType.U16, ptrs[2], this.minBoundingPtr, this.maxBoundingPtr, "%i", ImGuiSliderFlags.AlwaysClamp);
		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 40);
			ImGui.TextUnformatted("Guidelines will never be longer than this, no matter how big the target's hitbox is.");
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}

		changed |= ImGui.SliderScalar("Guideline thickness", ImGuiDataType.U16, ptrs[3], this.minThicknessPtr, this.maxThicknessPtr, "%i", ImGuiSliderFlags.AlwaysClamp);
		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 40);
			ImGui.TextUnformatted("How wide/thick do you want the guidelines to be?");
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		ImGui.PushTextWrapPos(ImGui.GetFontSize() * 25);
		ImGui.TextUnformatted("All of the above toggle settings can be changed via command:");
		ImGui.Indent();
		ImGui.TextUnformatted($"{Plugin.Command} <action> [target]");
		ImGui.Unindent();
		ImGui.TextUnformatted("");
		ImGui.TextUnformatted("Target may be any of the following, hyphyens optional:");
		ImGui.Indent();
		ImGui.TextUnformatted("fl, front-left, f, front, fr, front-right, r, right, br, back-right, b, back, bl, back-left, l, left");
		ImGui.Unindent();
		ImGui.TextUnformatted("");
		ImGui.TextUnformatted("Additionally, you can use 'cardinal', 'cardinals', 'diagonal', 'diagonals', and 'all' to affect multiple lines with a single command.");
		ImGui.TextUnformatted("");
		ImGui.TextUnformatted("Finally, you can use the action 'config' (the default) to explicitly toggle this window, and the target 'render' (the default) to toggle showing guides at all without losing your settings.");
		ImGui.PopTextWrapPos();

		if (changed) {
			for (int i = 0; i < vals.Length; ++i)
				Marshal.Copy(ptrs[i], vals, i, 1);
			this.conf.Enabled = active;
			this.conf.ExtraDrawRange = vals[0];
			this.conf.MinDrawRange = vals[1];
			this.conf.MaxDrawRange = vals[2];
			this.conf.LineThickness = vals[3];
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

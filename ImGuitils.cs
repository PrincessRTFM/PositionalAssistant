namespace PositionalGuide;

using ImGuiNET;

internal class ImGuitils {
	public int TooltipTextWrapWidth;

	public ImGuitils(int tooltipTextWrapWidth) {
		this.TooltipTextWrapWidth = tooltipTextWrapWidth;
	}

	public void Tooltip(string text) {
		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * this.TooltipTextWrapWidth);
			ImGui.TextUnformatted(text);
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}
	}
}

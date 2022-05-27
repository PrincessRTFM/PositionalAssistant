namespace PositionalGuide;

using System.Numerics;

using Dalamud.Configuration;

using Newtonsoft.Json;

public class Configuration: IPluginConfiguration {
	public static readonly string[] Directions = new string[] {
		"front",
		"front right",
		"right",
		"back right",
		"back",
		"back left",
		"left",
		"front left",
	};

	public int Version { get; set; } = 1;

	/// <summary>
	/// Starts at front-left then goes clockwise
	/// </summary>
	public bool[] DrawGuides { get; set; } = new bool[] {
		false, // front
		false, // front right
		false, // right
		false, // back right
		false, // back
		false, // back left
		false, // left
		false, // front left
	};

	public short ExtraDrawRange { get; set; } = 0;
	public short MinDrawRange { get; set; } = 0;
	public short MaxDrawRange { get; set; } = byte.MaxValue;
	public short LineThickness { get; set; } = 3;

	public Vector4[] LineColours { get; set; } = new Vector4[] {
		new Vector4(1, 0, 0, 1), // front
		new Vector4(1, 0, 0, 1), // front right
		new Vector4(0, 0, 1, 1), // right
		new Vector4(0, 1, 0, 1), // back right
		new Vector4(0, 1, 0, 1), // back
		new Vector4(0, 1, 0, 1), // back left
		new Vector4(0, 0, 1, 1), // left
		new Vector4(1, 0, 0, 1), // front left
	};

	#region Shortcuts
	[JsonIgnore]
	public float SoftDrawRange => (float)this.ExtraDrawRange / 10;

	[JsonIgnore]
	public float SoftMinRange => (float)this.MinDrawRange / 10;

	[JsonIgnore]
	public float SoftMaxRange => (float)this.MaxDrawRange / 10;
	#endregion
}

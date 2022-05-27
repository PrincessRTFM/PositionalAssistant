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

	public bool Enabled { get; set; } = true;

#if DEBUG
	public bool DrawOnPlayers { get; set; } = false;
#endif

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

	/// <summary>
	/// Starts at front-left then goes clockwise
	/// </summary>
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
	public bool DrawFront {
		get => this.DrawGuides[0];
		set => this.DrawGuides[0] = value;
	}
	[JsonIgnore]
	public bool DrawFrontRight {
		get => this.DrawGuides[1];
		set => this.DrawGuides[1] = value;
	}
	[JsonIgnore]
	public bool DrawRight {
		get => this.DrawGuides[2];
		set => this.DrawGuides[2] = value;
	}
	[JsonIgnore]
	public bool DrawBackRight {
		get => this.DrawGuides[3];
		set => this.DrawGuides[3] = value;
	}
	[JsonIgnore]
	public bool DrawBack {
		get => this.DrawGuides[4];
		set => this.DrawGuides[4] = value;
	}
	[JsonIgnore]
	public bool DrawBackLeft {
		get => this.DrawGuides[5];
		set => this.DrawGuides[5] = value;
	}
	[JsonIgnore]
	public bool DrawLeft {
		get => this.DrawGuides[6];
		set => this.DrawGuides[6] = value;
	}
	[JsonIgnore]
	public bool DrawFrontLeft {
		get => this.DrawGuides[7];
		set => this.DrawGuides[7] = value;
	}

	[JsonIgnore]
	public float SoftDrawRange => (float)this.ExtraDrawRange / 10;

	[JsonIgnore]
	public float SoftMinRange => (float)this.MinDrawRange / 10;

	[JsonIgnore]
	public float SoftMaxRange => (float)this.MaxDrawRange / 10;
	#endregion
}

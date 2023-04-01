namespace PrincessRTFM.PositionalGuide;

using System;
using System.Numerics;

using Dalamud.Configuration;

using Newtonsoft.Json;

public class Configuration: IPluginConfiguration {
	public const int
		IndexFront = 0,
		IndexFrontRight = 1,
		IndexRight = 2,
		IndexBackRight = 3,
		IndexBack = 4,
		IndexBackLeft = 5,
		IndexLeft = 6,
		IndexFrontLeft = 7,
		IndexCircle = 8;
	public static readonly string[] Directions = new string[] {
		"front guideline",
		"front right guideline",
		"right guideline",
		"back right guideline",
		"back guideline",
		"back left guideline",
		"left guideline",
		"front left guideline",
		"HIGHLY EXPERIMENTAL target circle",
	};

	public int Version { get; set; } = 1;

	public bool Enabled { get; set; } = true;
	public bool OnlyRenderWhenCentreOnScreen { get; set; } = false;
	public bool OnlyRenderWhenEndpointOnScreen { get; set; } = false;
	public bool OnlyRenderWhenEitherEndOnScreen { get; set; } = true;
	public bool DrawOnPlayers { get; set; } = false;
	public bool DrawOnSelfOnly { get; set; } = true;
	public bool DrawTetherLine { get; set; } = false;
	public bool FlattenTether { get; set; } = false;

	/// <summary>
	/// Starts at front then goes clockwise up to index=7, then circle at index=8
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
		false, // circle
	};

	public short ExtraDrawRange { get; set; } = 0;
	public short MinDrawRange { get; set; } = 0;
	public short MaxDrawRange { get; set; } = byte.MaxValue;
	public short LineThickness { get; set; } = 3;

	public short TetherLengthInner { get; set; } = -1; // if -1, go to centre of target's hitbox
	public short TetherLengthOuter { get; set; } = -1; // if -1, go to CENTRE of player's hitbox; if -2, go to EDGE of player's hitbox

	public Vector4 TetherColour { get; set; } = new(1);

	/// <summary>
	/// Starts at front then goes clockwise up to index=7, then circle at index=8
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
		new Vector4(1, 1, 0, 1), // circle default
	};

	public void Update() {

		if (this.DrawGuides.Length < 9) {
			bool[] guides = new bool[9];
			Array.Copy(this.DrawGuides, guides, this.DrawGuides.Length);
			guides[8] = false;
			this.DrawGuides = guides;
		}

		if (this.LineColours.Length < 9) {
			Vector4[] colours = new Vector4[9];
			Array.Copy(this.LineColours, colours, this.LineColours.Length);
			colours[8] = new Vector4(1, 1, 0, 1);
			this.LineColours = colours;
		}

		Plugin.Interface.SavePluginConfig(this);
	}

	#region Shortcuts
	[JsonIgnore]
	public bool DrawFront {
		get => this.DrawGuides[IndexFront];
		set => this.DrawGuides[IndexFront] = value;
	}
	[JsonIgnore]
	public bool DrawFrontRight {
		get => this.DrawGuides[IndexFrontRight];
		set => this.DrawGuides[IndexFrontRight] = value;
	}
	[JsonIgnore]
	public bool DrawRight {
		get => this.DrawGuides[IndexRight];
		set => this.DrawGuides[IndexRight] = value;
	}
	[JsonIgnore]
	public bool DrawBackRight {
		get => this.DrawGuides[IndexBackRight];
		set => this.DrawGuides[IndexBackRight] = value;
	}
	[JsonIgnore]
	public bool DrawBack {
		get => this.DrawGuides[IndexBack];
		set => this.DrawGuides[IndexBack] = value;
	}
	[JsonIgnore]
	public bool DrawBackLeft {
		get => this.DrawGuides[IndexBackLeft];
		set => this.DrawGuides[IndexBackLeft] = value;
	}
	[JsonIgnore]
	public bool DrawLeft {
		get => this.DrawGuides[IndexLeft];
		set => this.DrawGuides[IndexLeft] = value;
	}
	[JsonIgnore]
	public bool DrawFrontLeft {
		get => this.DrawGuides[IndexFrontLeft];
		set => this.DrawGuides[IndexFrontLeft] = value;
	}
	[JsonIgnore]
	public bool DrawCircle {
		get => this.DrawGuides[IndexCircle];
		set => this.DrawGuides[IndexCircle] = value;
	}

	[JsonIgnore]
	public float SoftDrawRange => (float)this.ExtraDrawRange / 10;

	[JsonIgnore]
	public float SoftMinRange => (float)this.MinDrawRange / 10;

	[JsonIgnore]
	public float SoftMaxRange => (float)this.MaxDrawRange / 10;

	[JsonIgnore]
	public float SoftInnerTetherLength => (float)this.TetherLengthInner / 10;

	[JsonIgnore]
	public float SoftOuterTetherLength => (float)this.TetherLengthOuter / 10;
	#endregion
}

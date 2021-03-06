using System.IO;

namespace VisualPinball.Engine.VPT.Sound
{
	public class Sound : Item<SoundData>
	{
		public Sound(SoundData data) : base(data)
		{
		}

		public Sound(BinaryReader reader, string itemName, int fileVersion) : this(new SoundData(reader, itemName, fileVersion))
		{
		}
	}
}

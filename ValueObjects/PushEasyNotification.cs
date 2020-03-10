using System.Collections.Generic;

namespace PushEasy
{
	public class PushEasyNotification
	{
		public enum Devices { Unknown, iOS, Android }

		public Devices Device { get; set; }
		
		public string Token { get; set; }
		public string Text { get; set; }
		// which sound-file to play on the mobile device.
		// default is "default", which plays the default sound on ios. Set to null for a silent notification
		public string Sound = "default";
		public int? Badge { get; set; }
		// use simple types as value, just as string, int, etc.
		public Dictionary<string, object> Payload { get; set; }

		public object Context { get; set; }

		// will never be null, since its directly initialized with an unkown result
		public PushEasyResult Result { get; internal set; }

		public PushEasyNotification()
		{
			this.Device = Devices.Unknown;
		}
	}
}
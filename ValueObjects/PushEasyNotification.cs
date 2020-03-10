using System.Collections.Generic;

namespace PushEasy
{
	/// <summary>
	/// A notification to set.
	/// </summary>
	public class PushEasyNotification
	{
		public enum Devices { Unknown, iOS, Android }

		/// <summary>
		/// The type of the device from your own data tier.
		/// </summary>
		public Devices Device { get; set; }
		/// <summary>
		/// The token you received from the device and stored to your own data tier.
		/// </summary>
		public string Token { get; set; }
		/// <summary>
		/// The text of the notification to display on the device.
		/// </summary>
		public string Text { get; set; }
		/// <summary>
		/// Which sound-file to play on the mobile device.
		/// Default is "default", which plays the default sound on ios. Set to null for a silent notification
		/// </summary>
		public string Sound = "default";
		public int? Badge { get; set; }
		/// <summary>
		/// The payload of the message for your own use on the device.
		/// Use simple types as value, like string, int, lists, dictionary, etc.
		/// </summary>
		public Dictionary<string, object> Payload { get; set; }

		/// <summary>
		/// For your own use to process this message after it has been sent (or not).
		/// </summary>
		public object Context { get; set; }

		/// <summary>
		/// The result of this message after it has been sent.
		/// Will never be null, since its directly initialized with an unkown result
		/// </summary>
		public PushEasyResult Result { get; internal set; }

		public PushEasyNotification()
		{
			this.Device = Devices.Unknown;
		}
	}
}
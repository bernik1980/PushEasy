namespace PushEasy
{
	/// <summary>
	/// Configuration the push service.
	/// </summary>
	public class PushEasyConfiguration
	{
		/// <summary>
		/// Notifications will be splitted into groups and sent simultaneously by count of the BulkSize.
		/// Default is 250
		/// </summary>
		public int BulkSize { get; set; }

		/// <summary>
		/// If you use an development certificate for ios, you need to use the sandbox apns provider.
		/// </summary>
		public bool UseSandbox { get; set; }

		/// <summary>
		/// Full path to the ios certificate (.p12).
		/// </summary>
		public string APNSCertificatePath { get; set; }
		/// <summary>
		/// The password of the ios certificate.
		/// </summary>
		public string APNSCertificatePassword { get; set; }

		/// <summary>
		/// The api case for android firebase push.
		/// </summary>
		public string FirebaseProjectAPIKey { get; set; }
	}
}
namespace PushEasy
{
	public class PushEasyConfiguration
	{
		public int BulkSize { get; set; }

		public bool UseSandbox { get; set; }

		public string APNSCertificatePath { get; set; }
		public string APNSCertificatePassword { get; set; }

		public string FirebaseProjectAPIKey { get; set; }
	}
}
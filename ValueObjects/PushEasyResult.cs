using System;

namespace PushEasy
{
	/// <summary>
	/// The result of a message after it has been sent.
	/// </summary>
	public class PushEasyResult
	{
		public enum Results
		{
			/// <summary>
			/// An unexpected error did occur. This is an error in the PushEasy library and should hopefully not occur.
			/// </summary>
			Unknown,
			/// <summary>
			/// This message has been submitted for sending. (Does not indicate, that it has arrived.)
			/// </summary>
			Success,
			/// <summary>
			/// An expected error did occur. Check Error and ErrorDetails.
			/// </summary>
			Error
		}

		public enum Errors
		{
			/// <summary>
			/// If the result is an error and the error is set to Unknown, this is an error in the PushEasy library and should hopefully not occur.
			/// </summary>
			Unknown,
			/// <summary>
			/// Something went wrong connecting to the remote provider.
			/// </summary>
			Connection,
			/// <summary>
			/// Something went wrong during data conversion or handling data response.
			/// </summary>
			Data,
			/// <summary>
			/// The device is unknown to the provider and should be removed from your data tier.
			/// </summary>
			Device
		}

		/// <summary>
		/// What did happen?
		/// </summary>
		public Results Result { get; internal set; }
		/// <summary>
		/// What went wrong?
		/// </summary>
		public Errors Error { get; internal set; }
		/// <summary>
		/// Why did it went wrong?
		/// </summary>
		public string ErrorDetails { get; internal set; }

		/// <summary>
		/// The timestamp when the provider did submit the message.
		/// </summary>
		public DateTime? SendStartedOn { get; internal set; }
		/// <summary>
		/// The timestamp when the result was recevied.
		/// </summary>
		public DateTime? SendCompletedOn { get; internal set; }

		internal PushEasyResult(Results result, Errors error = Errors.Unknown, string errorDetails = null, DateTime? startedOn = null, DateTime? completedOn = null)
		{
			this.Result = result;
			this.Error = error;
			this.ErrorDetails = errorDetails;
			this.SendStartedOn = startedOn;
			this.SendCompletedOn = completedOn;
		}

		internal PushEasyResult(Results result, DateTime? startedOn, DateTime? completedOn)
		{
			this.Result = result;
			this.Error = Errors.Unknown;
			this.SendStartedOn = startedOn;
			this.SendCompletedOn = completedOn;
		}
	}
}
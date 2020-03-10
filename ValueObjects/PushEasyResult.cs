using System;

namespace PushEasy
{
	public class PushEasyResult
	{
		public enum Results
		{
			Unknown, // an unexpected error did occur. this is an error in the PushEasy library.
			Success, // provider did sent the message
			Error // an expected error did occur. check Error and ErrorDetails
		}

		public enum Errors
		{
			Unknown, // this is an error in the PushEasy library
			Connection, // something went wrong connecting the remote provider
			Data, // something went wrong during data conversion or handling data response
			Device // the device does not exist anymore and should be removed
		}

		public Results Result { get; internal set; }
		public Errors Error { get; internal set; }
		public string ErrorDetails { get; internal set; }

		public DateTime? SendStartedOn { get; internal set; }
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
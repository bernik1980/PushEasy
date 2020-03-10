using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace PushEasy.Providers
{
	/// <summary>
	/// Provider for sending android push notifications.
	/// </summary>
	internal class PushEasyProviderFirebase : PushEasyProviderBase
	{
		private const string _firebaseUrl = "https://fcm.googleapis.com/fcm/send";

		internal override List<List<PushEasyNotification>> Group(List<PushEasyNotification> notifications)
		{
			var groups = new List<List<PushEasyNotification>>();

			var notificationGroups = notifications.GroupBy(n => n, new NotificationsComparer());
			foreach (var notificationGroup in notificationGroups)
			{
				groups.Add(notificationGroup.ToList());
			}

			return groups;
		}

		internal override void Send(PushEasyConfiguration configuration, List<PushEasyNotification> notifications)
		{
			// create payload
			// all registrationIds of the devices to send to
			var registrationIds = new List<string>();
			foreach (var notification in notifications)
			{
				registrationIds.Add(notification.Token);
			}

			// data is equal among devices, since they are grouped
			var notificationFirst = notifications.FirstOrDefault();
			var data = new Dictionary<string, object>();
			// text
			data.Add("text", notificationFirst.Text);
			// sound
			if (notificationFirst.Sound != null)
			{
				data.Add("sound", notificationFirst.Sound);
			}
			// message payload
			if (notificationFirst.Payload != null && notificationFirst.Payload != null)
			{
				foreach (var entry in notificationFirst.Payload)
				{
					if (entry.Key == null)
					{
						continue;
					}

					if (entry.Key.ToLower() == "text")
					{
						continue;
					}

					if (entry.Key.ToLower() == "sound")
					{
						continue;
					}

					data.Add(entry.Key, this.BaseProviderComplexToSimple(entry.Value));
				}
			}

			// the payload for FireBase api
			var json = new Dictionary<string, object>();
			json.Add("data", data);
			json.Add("registration_ids", registrationIds);

			// create request body
			string error = null;
			var jsonRequestString = this.BaseProviderToJsonString(json, out error);

			if (error != null)
			{
				this.BaseProviderAssignResults(notifications, new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Data, "Could not convert FireBase payload to json. Error: " + error));
				return;
			}

			var startedOn = DateTime.UtcNow;

			// send to FireBase and receive response string
			byte[] jsonResponseData = null;
			using (var client = new WebClient())
			{
				client.Headers[HttpRequestHeader.ContentType] = "application/json";
				client.Headers[HttpRequestHeader.Authorization] = "key=" + configuration.FirebaseProjectAPIKey;

				try
				{
					jsonResponseData = client.UploadData(_firebaseUrl, Encoding.UTF8.GetBytes(jsonRequestString));
				}
				catch (Exception ex)
				{
					this.BaseProviderAssignResults(notifications, new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Connection, "Did not get a response from FireBase. Error: " + ex.ToString(), startedOn));
					return;
				}
			}

			var completedOn = DateTime.UtcNow;

			var jsonResponseString = Encoding.UTF8.GetString(jsonResponseData);

			// parse to objects
			error = null;
			Dictionary<string, object> jsonResponse = this.BaseProviderFromJsonString<Dictionary<string, object>>(jsonResponseString, out error);
			if (error != null)
			{
				this.BaseProviderAssignResults(notifications, new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Data, "Could not convert FireBase json response. Error: " + error, startedOn));
				return;
			}

			// check for results
			if (jsonResponse == null || !jsonResponse.ContainsKey("results") || !(jsonResponse["results"] is object[]))
			{
				this.BaseProviderAssignResults(notifications, new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Data, "Invalid response from FireBase. results array missing.", startedOn));
				return;
			}

			var jsonResults = jsonResponse["results"] as object[];

			if (jsonResults.Length != notifications.Count())
			{
				this.BaseProviderAssignResults(notifications, new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Data, string.Format("Invalid results count ({0}) from FireBase. Did match to notifications ({1}).", jsonResults.Length, notifications.Count), startedOn));
				return;
			}

			// did receive valid results from FireBase
			// check devices
			for (var i = 0; i < jsonResults.Length; ++i)
			{
				var jsonResult = jsonResults[i] as Dictionary<string, object>;
				var notification = notifications[i];

				if (jsonResult == null)
				{
					notification.Result = new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Data, "Missing result in response for that device", startedOn);
				}
				else if (jsonResult.ContainsKey("message_id"))
				{
					notification.Result = new PushEasyResult(PushEasyResult.Results.Success, startedOn, completedOn);
				}
				else if (jsonResult.ContainsKey("error"))
				{
					notification.Result = new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Device, jsonResult["error"].ToString(), startedOn);
				}
				else
				{
					notification.Result = new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Data, "Unknown response from FireBase for device. Response: " + string.Join(", ", jsonResult), startedOn);
				}
			}
		}

		private class NotificationsComparer : IEqualityComparer<PushEasyNotification>
		{
			public bool Equals(PushEasyNotification notification1, PushEasyNotification notification2)
			{
				// equal text?
				if (notification1.Text != notification2.Text)
				{
					return false;
				}

				// equal sound?
				if (notification1.Sound != notification2.Sound)
				{
					return false;
				}

				// equal payload
				var payload1 = notification1.Payload != null && notification1.Payload.Any() ? notification1.Payload : null;
				var payload2 = notification2.Payload != null && notification2.Payload.Any() ? notification2.Payload : null;

				if (payload1 != null && payload2 == null)
				{
					return false;
				}

				if (payload1 == null && payload2 != null)
				{
					return false;
				}

				if (payload1 != null && payload2 != null)
				{
					if (payload1.Count != payload2.Count)
					{
						return false;
					}

					foreach (var entry1 in payload1)
					{
						if (!payload2.ContainsKey(entry1.Key))
						{
							return false;
						}

						var value2 = payload2[entry1.Key];

						if (entry1.Value != null && value2 == null)
						{
							return false;
						}

						if (entry1.Value == null && value2 != null)
						{
							return false;
						}

						if (entry1.Value != null && value2 != null)
						{
							if (!entry1.Value.Equals(value2))
							{
								return false;
							}
						}
					}
				}

				return true;
			}

			public int GetHashCode(PushEasyNotification notification)
			{
				return notification.Text.GetHashCode();
			}
		}
	}
}
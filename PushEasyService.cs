using PushEasy.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PushEasy
{
	/// <summary>
	/// Handles sending, splitting and grouping of notifications.
	/// </summary>
	public static class PushEasyService
	{
		public static void Send(PushEasyConfiguration configuration, IEnumerable<PushEasyNotification> notifications)
		{
			if (notifications == null)
			{
				return;
			}

			// this will increase needed resources, but there are multiple times a list handling is easier then a ienumerate handling
			var notificationsList = notifications.ToList();

			if (notificationsList.Count == 0)
			{
				return;
			}

			if (configuration.BulkSize <= 0)
			{
				configuration.BulkSize = 250;
			}

			// check general notification validity
			for (var i = 0; i < notificationsList.Count; ++i)
			{
				var notification = notificationsList[i];

				if (notification.Device == PushEasyNotification.Devices.Unknown)
				{
					notification.Result = new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Device, "Unknown device type.");
				}
				else if (string.IsNullOrEmpty(notification.Token))
				{
					notification.Result = new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Device, "Missing or empty token.");
				}

				if (notification.Result != null)
				{
					// remove erroneus devices
					notificationsList.RemoveAt(i);
					i -= 1;
				}
				else
				{
					// initialize each notification with an unknown response to avoid null results in case of an unexpected error
					notification.Result = new PushEasyResult(PushEasyResult.Results.Unknown);
				}
			}

			new PushEasyServiceInternal().Send(configuration, notificationsList);
		}

		public static IEnumerable<PushEasyNotification> Check(PushEasyConfiguration configuration)
		{
			return new PushEasyServiceInternal().Check(configuration);
		}

		internal class PushEasyServiceInternal : PushEasyProviderBase
		{
			internal override void Send(PushEasyConfiguration configuration, List<PushEasyNotification> notifications)
			{
				// create a task for each provider
				var tasks = new List<Task>();

				foreach (var device in Enum.GetValues(typeof(PushEasyNotification.Devices)).OfType<PushEasyNotification.Devices>())
				{
					var notificationsDevice = notifications.Where(n => n.Device == device).ToList();

					if (notificationsDevice.Count == 0)
					{
						continue;
					}

					var provider = this.GetProviderForDevice(device);

					if (provider == null)
					{
						this.BaseProviderAssignResults(notificationsDevice, new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Unknown, string.Format("Support for device ›{0}‹ not implemented.", device.ToString())));
						continue;
					}

					// group
					var groups = provider.Group(notificationsDevice);

					// bulk
					for (var i = 0; i < groups.Count; ++i)
					{
						var group = groups[i];

						// if we got more notifications in a group then we want per bulk
						// we create additional groups and split notifications equally between them
						if (group.Count > configuration.BulkSize)
						{
							// get count of chunks needed
							var countChunks = group.Count / configuration.BulkSize;
							if (countChunks * configuration.BulkSize < group.Count)
							{
								countChunks += 1;
							}

							// get count of notifications for each chunk
							var countNotificationsPerChunk = group.Count / countChunks;
							if (countNotificationsPerChunk * countChunks < group.Count)
							{
								countNotificationsPerChunk += 1;
							}

							// remove original group
							groups.RemoveAt(i);
							i -= 1;

							// create new groups which notification matching chunk size
							for (var n = 0; n < countChunks; ++n)
							{
								i += 1;
								groups.Insert(i, group.Skip(n * countNotificationsPerChunk).Take(countNotificationsPerChunk).ToList());
							}
						}
					}

					foreach (var group in groups)
					{
						tasks.Add(Task.Run(() =>
						{
							provider.Send(configuration, group);
						}));
					}
				}

				// wait until tasks finish
				foreach (var task in tasks)
				{
					task.Wait();
				}
			}

			internal override IEnumerable<PushEasyNotification> Check(PushEasyConfiguration configuration)
			{
				// create a task for each provider
				var tasks = new List<Task<IEnumerable<PushEasyNotification>>>();

				foreach (var device in Enum.GetValues(typeof(PushEasyNotification.Devices)).OfType<PushEasyNotification.Devices>())
				{
					var provider = this.GetProviderForDevice(device);

					if (provider == null)
					{
						continue;
					}

					tasks.Add(Task.Run(() =>
					{
						return provider.Check(configuration);
					}));
				}

				var notifications = new List<PushEasyNotification>();

				// wait until tasks finish and collection notifications
				foreach (var task in tasks)
				{
					task.Wait();

					if (task.Result != null)
					{
						notifications.AddRange(task.Result);
					}
				}

				return notifications;
			}

			private Providers.PushEasyProviderBase GetProviderForDevice(PushEasyNotification.Devices device)
			{
				switch (device)
				{
					case PushEasyNotification.Devices.iOS:
						return new PushEasyProviderAPNS();
					case PushEasyNotification.Devices.Android:
						return new PushEasyProviderFirebase();
				}

				return null;
			}
		}
	}
}
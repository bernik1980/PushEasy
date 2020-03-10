using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace PushEasy.Providers
{
	internal abstract class PushEasyProviderBase
	{
		internal virtual List<List<PushEasyNotification>> Group(List<PushEasyNotification> notifications)
		{
			// default is no grouping
			var groups = new List<List<PushEasyNotification>>();
			groups.Add(notifications);

			return groups;
		}

		internal abstract void Send(PushEasyConfiguration configuration, List<PushEasyNotification> notifications);

		protected Dictionary<PushEasyNotification, PushEasyResult> BaseProviderAssignResult(PushEasyNotification notification, PushEasyResult result)
		{
			var results = new Dictionary<PushEasyNotification, PushEasyResult>();
			results.Add(notification, result);

			return results;
		}

		protected void BaseProviderAssignResults(IEnumerable<PushEasyNotification> notifications, PushEasyResult result)
		{
			foreach (var notification in notifications)
			{
				notification.Result = result;
			}
		}

		protected object BaseProviderComplexToSimple(object o)
		{
			if (o is IDictionary)
			{
				var values = new Dictionary<string, object>();

				foreach (string key in (o as IDictionary).Keys)
				{
					var value = (o as IDictionary)[key];

					values.Add(key, this.BaseProviderComplexToSimple(value));
				}

				return values;
			}

			if (o is ICollection)
			{
				var values = new List<object>();

				foreach (var subValue in o as ICollection)
				{
					values.Add(this.BaseProviderComplexToSimple(subValue));
				}

				return values;
			}

			return o;
		}

		protected string BaseProviderToJsonString(object value, out string error)
		{
			error = null;

			try
			{
				return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(value);
			}
			catch (Exception ex)
			{
				error = ex.ToString();

				return null;
			}
		}

		protected T BaseProviderFromJsonString<T>(string jsonString, out string error) where T:class
		{
			error = null;

			try
			{
				return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(jsonString) as T;
			}
			catch (Exception ex)
			{
				error = ex.ToString();

				return null;
			}
		}
	}
}
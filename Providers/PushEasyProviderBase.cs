using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace PushEasy.Providers
{
	/// <summary>
	/// Base class for all providers with some shared functionality.
	/// </summary>
	internal abstract class PushEasyProviderBase
	{
		/// <summary>
		/// If the provider does support grouping, override this.
		/// </summary>
		/// <param name="notifications"></param>
		/// <returns></returns>
		internal virtual List<List<PushEasyNotification>> Group(List<PushEasyNotification> notifications)
		{
			// default is no grouping
			var groups = new List<List<PushEasyNotification>>();
			groups.Add(notifications);

			return groups;
		}

		/// <summary>
		/// Sents notifications with the given configuration.
		/// </summary>
		/// <param name="configuration"></param>
		/// <param name="notifications"></param>
		internal abstract void Send(PushEasyConfiguration configuration, List<PushEasyNotification> notifications);

		/// <summary>
		/// Assigns a result to a notification.
		/// </summary>
		/// <param name="notification"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		protected Dictionary<PushEasyNotification, PushEasyResult> BaseProviderAssignResult(PushEasyNotification notification, PushEasyResult result)
		{
			var results = new Dictionary<PushEasyNotification, PushEasyResult>();
			results.Add(notification, result);

			return results;
		}

		/// <summary>
		/// Assigns a result to a list of notifications.
		/// </summary>
		/// <param name="notifications"></param>
		/// <param name="result"></param>
		protected void BaseProviderAssignResults(IEnumerable<PushEasyNotification> notifications, PushEasyResult result)
		{
			foreach (var notification in notifications)
			{
				notification.Result = result;
			}
		}

		/// <summary>
		/// Converts an object to simple types for better json conversion.
		/// </summary>
		/// <param name="o"></param>
		/// <returns></returns>
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

		/// <summary>
		/// Converts an object to a json-string.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="error"></param>
		/// <returns></returns>
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

		/// <summary>
		/// Converts a json-string to an object.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="jsonString"></param>
		/// <param name="error"></param>
		/// <returns></returns>
		protected T BaseProviderFromJsonString<T>(string jsonString, out string error) where T : class
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
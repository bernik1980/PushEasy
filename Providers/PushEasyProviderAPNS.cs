using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PushEasy.Providers
{
	/// <summary>
	/// Apples Binary Provider API: https://developer.apple.com/library/content/documentation/NetworkingInternet/Conceptual/RemoteNotificationsPG/BinaryProviderAPI.html
	/// </summary>
	internal class PushEasyProviderAPNS : PushEasyProviderBase
	{
		private enum APNSErrorStatusCodes
		{
			NoErrors = 0,
			ProcessingError = 1,
			MissingDeviceToken = 2,
			MissingTopic = 3,
			MissingPayload = 4,
			InvalidTokenSize = 5,
			InvalidTopicSize = 6,
			InvalidPayloadSize = 7,
			InvalidToken = 8,
			Shutdown = 10,
			ConnectionError = 254,
			Unknown = 255
		}

		private const string _hostLive = "gateway.push.apple.com";
		private const string _hostSandbox = "gateway.sandbox.push.apple.com";

		private const int _portSend = 2195;
		private const int _portCheck = 2196;

		internal override void Send(PushEasyConfiguration configuration, List<PushEasyNotification> notifications)
		{
			var tokens = new List<byte[]>();
			var payloads = new List<byte[]>();

			// check for invalid tokens or payload
			var regexValidDeviceToken = new Regex(@"^[0-9A-F]+$", RegexOptions.IgnoreCase);
			foreach (var notification in notifications)
			{
				// check token validity by format
				if (!regexValidDeviceToken.Match(notification.Token).Success)
				{
					notification.Result = new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Device, "Invalid token format.");
					continue;
				}

				var token = new byte[notification.Token.Length / 2];

				// try to convert data to apns format
				try
				{
					for (int i = 0; i < token.Length; ++i)
					{
						token[i] = byte.Parse(notification.Token.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
					}
				}
				catch (Exception ex)
				{
					notification.Result = new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Device, "Could not convert token. Error: " + ex);
					continue;
				}

				// check token length
				if (token.Length < 32)
				{
					notification.Result = new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Device, "Invalid token length.");
					continue;
				}

				// create payload
				var jSONAps = new Dictionary<string, object>();
				// badge
				if (notification.Badge != null && notification.Badge.Value > 0)
				{
					jSONAps.Add("badge", notification.Badge ?? 0);
				}
				// alert
				var jsonAlert = new Dictionary<string, object>();
				jsonAlert.Add("body", notification.Text);
				jSONAps.Add("alert", jsonAlert);
				if (notification.Sound != null)
				{
					jSONAps.Add("sound", notification.Sound);
				}

				var jsonPayload = new Dictionary<string, object>();
				jsonPayload.Add("aps", jSONAps);

				// message payload
				if (notification.Payload != null && notification.Payload.Any())
				{
					foreach (var entry in notification.Payload)
					{
						if (string.IsNullOrEmpty(entry.Key))
						{
							continue;
						}

						if (entry.Key.ToLower() == "aps")
						{
							continue;
						}

						jsonPayload.Add(entry.Key, this.BaseProviderComplexToSimple(entry.Value));
					}
				}

				string error = null;
				var payloadString = this.BaseProviderToJsonString(jsonPayload, out error);

				if (error != null)
				{
					notification.Result = new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Data, "Could not convert APNS payload to json. Error: " + error);
					continue;
				}

				var payload = Encoding.UTF8.GetBytes(payloadString);

				if (payload.Length > 2048)
				{
					notification.Result = new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Data, string.Format("APNS payload size ({0} bytes) exceeded max size of 2048 bytes.", payload.Length));
					continue;
				}

				// at this point the notification is valid
				tokens.Add(token);
				payloads.Add(payload);
			}

			// filter invalid notifications
			for (var i = 0; i < notifications.Count; ++i)
			{
				if (notifications[i].Result.Result == PushEasyResult.Results.Error)
				{
					notifications.RemoveAt(i);
					i -= 1;
				}
			}

			if (notifications.Count == 0)
			{
				return;
			}

			TcpClient client = null;
			SslStream stream = null;

			var result = this.CreateClientAndStream(configuration, _portSend, out client, out stream);

			if (result != null)
			{
				this.BaseProviderAssignResults(notifications, result);
				return;
			}

			// get data to write to stream
			var data = new List<byte>();
			for (var index = 0; index < notifications.Count; ++index)
			{
				// create notification data
				var dataNotification = new List<byte>();

				// 1. Device Token
				dataNotification.Add(0x01);
				dataNotification.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Convert.ToInt16(tokens[index].Length))));
				dataNotification.AddRange(tokens[index]);

				// 2. Payload
				dataNotification.Add(0x02);
				dataNotification.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Convert.ToInt16(payloads[index].Length))));
				dataNotification.AddRange(payloads[index]);

				// 3. Identifier
				dataNotification.Add(0x03);
				dataNotification.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((Int16)4)));
				dataNotification.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(index)));

				// 4. Expiration
				dataNotification.Add(0x04);
				dataNotification.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((Int16)4)));
				dataNotification.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)DateTime.UtcNow.AddMonths(1).Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds)));

				// 5. Priority
				dataNotification.Add(0x05);
				dataNotification.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((Int16)1)));
				dataNotification.Add(5); //LowPriority ? (byte)5 : (byte)10;

				data.Add(0x02); // COMMAND 2 for new format
				data.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((Int32)dataNotification.Count)));
				data.AddRange(dataNotification);
			}

			// check for response
			var errorIndex = -1;
			var errorStatus = APNSErrorStatusCodes.Unknown;

			var startedOn = DateTime.UtcNow;

			if (data.Count > 0)
			{
				// write the data
				stream.Write(data.ToArray(), 0, data.Count);

				for (var i = 0; i < 10; ++i)
				{
					// give apple some time to write to the socket
					Thread.Sleep(100);

					// check if something available
					if (client.Client.Available > 0)
					{
						var buffer = new byte[6];
						var length = stream.Read(buffer, 0, buffer.Length);

						if (length > 0)
						{
							var status = (int)buffer[1];

							// If we made it here, we did receive some data, so let's parse the error
							errorStatus = Enum.IsDefined(typeof(APNSErrorStatusCodes), status) ? (APNSErrorStatusCodes)status : APNSErrorStatusCodes.Unknown;
							// get the identifier of the device failing
							errorIndex = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, 2));
						}

						break;
					}
				}
			}

			var completedOn = DateTime.UtcNow;

			this.DisposeClientAndStream(client, stream);

			for (var index = 0; index < notifications.Count; ++index)
			{
				var notification = notifications[index];

				// error received?
				if (index == errorIndex)
				{
					var error = PushEasyResult.Errors.Unknown;
					switch (errorStatus)
					{
						case APNSErrorStatusCodes.MissingDeviceToken:
						case APNSErrorStatusCodes.InvalidToken:
							error = PushEasyResult.Errors.Device;
							break;
						case APNSErrorStatusCodes.MissingTopic:
						case APNSErrorStatusCodes.MissingPayload:
						case APNSErrorStatusCodes.InvalidTokenSize:
						case APNSErrorStatusCodes.InvalidTopicSize:
						case APNSErrorStatusCodes.InvalidPayloadSize:
						case APNSErrorStatusCodes.NoErrors:
						case APNSErrorStatusCodes.ProcessingError:
						case APNSErrorStatusCodes.Shutdown:
						case APNSErrorStatusCodes.ConnectionError:
						case APNSErrorStatusCodes.Unknown:
							error = PushEasyResult.Errors.Data;
							break;
					}

					notification.Result = new PushEasyResult(PushEasyResult.Results.Error, error, "APNS returned an error: " + errorStatus.ToString(), startedOn);

					// since apple will cancel after the first error, we need to trigger the next sending operation
					if (index < notifications.Count - 1)
					{
						this.Send(configuration, notifications.Skip(index + 1).ToList());
					}

					break;
				}

				// no error
				notification.Result = new PushEasyResult(PushEasyResult.Results.Success, startedOn, completedOn);
			}
		}

		internal override IEnumerable<PushEasyNotification> Check(PushEasyConfiguration configuration)
		{
			TcpClient client = null;
			SslStream stream = null;

			var result = this.CreateClientAndStream(configuration, _portCheck, out client, out stream);

			if (result != null)
			{
				return new List<PushEasyNotification> { new PushEasyNotification { Result = result } };
			}

			var buffer = new byte[4096];
			var bytesRead = 0;
			var data = new List<byte>();

			// get all data from apple
			// it could be many, but since we create a complete list of notifications anyway, we can do it that way, to keep the stream up shortly
			while (true)
			{
				try
				{
					bytesRead = stream.Read(buffer, 0, buffer.Length);
				}
				catch
				{
					break;
				}

				// completed?
				if (bytesRead == 0)
				{
					break;
				}

				// append to possible previous data
				for (int i = 0; i < bytesRead; i++)
				{
					data.Add(buffer[i]);
				}
			}

			this.DisposeClientAndStream(client, stream);

			var notifications = new List<PushEasyNotification>();

			var lengthSeconds = 4;
			var lengthTokenLength = 2;
			var lengthTokenMin = 32;
			// calculate minimum size for a valid packet
			var lengthMin = lengthSeconds + lengthTokenLength + lengthTokenMin;

			// proccess data
			// (we dont care for the timestamp in simple push, so its commented out)
			while (data.Count >= lengthMin)
			{
				// get seconds buffer
				// var secondsBuffer = data.GetRange(0, lengthSeconds).ToArray();
				// get token length buffer (not the token itself, only the length)
				var tokenLengthBuffer = data.GetRange(lengthSeconds, lengthTokenLength).ToArray();

				// Check endianness and reverse if needed
				if (BitConverter.IsLittleEndian)
				{
					// Array.Reverse(secondsBuffer);
					Array.Reverse(tokenLengthBuffer);
				}

				// get the actual length of the token
				var lengthToken = BitConverter.ToInt16(tokenLengthBuffer, 0);
				// at this point we finaly know the whole length of the package
				var lengthTotal = lengthSeconds + lengthTokenLength + lengthToken;

				// sanity check if we received enough data
				if (data.Count < lengthTotal)
				{
					break;
				}

				// get timestamp
				// var seconds = BitConverter.ToInt32(secondsBuffer, 0);
				// var timestamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);

				// get token
				var tokenBuffer = data.GetRange(lengthSeconds + lengthTokenLength, lengthToken).ToArray();
				var token = BitConverter.ToString(tokenBuffer).Replace("-", "").ToLower().Trim();

				// Remove what we parsed from the buffer
				data.RemoveRange(0, lengthTotal);

				notifications.Add(new PushEasyNotification { Device = PushEasyNotification.Devices.iOS, Token = token, Result = new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Device) });
			}

			return notifications;
		}

		private PushEasyResult CreateClientAndStream(PushEasyConfiguration configuration, int port, out TcpClient client, out SslStream stream)
		{
			client = null;
			stream = null;

			// create certificate from path with password
			var certificate = new X509Certificate2(File.ReadAllBytes(configuration.APNSCertificatePath), configuration.APNSCertificatePassword, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
			// need a collection for some calls
			var certificates = new X509Certificate2Collection();
			certificates.Add(certificate);

			var host = !configuration.UseSandbox ? _hostLive : _hostSandbox;

			// connect to apple
			client = new TcpClient();
			client.Connect(host, port);
			client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

			// open stream to write/read
			stream = new SslStream(client.GetStream(), false, (object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors policyErrors) => { return true; }, (sender, targetHost, localCerts, remoteCert, acceptableIssuers) => certificate);
			try
			{
				stream.AuthenticateAsClient(host, certificates, System.Security.Authentication.SslProtocols.Tls, false);
			}
			catch (Exception ex)
			{
				return new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Connection, "Could not create SslStream. Error: " + ex.ToString());
			}

			if (!stream.IsMutuallyAuthenticated)
			{
				return new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Connection, "Stream is not mutally authenticated.");
			}

			if (!stream.CanWrite)
			{
				return new PushEasyResult(PushEasyResult.Results.Error, PushEasyResult.Errors.Connection, "Cannot write to stream.");
			}

			return null;
		}

		private void DisposeClientAndStream(TcpClient client, SslStream stream)
		{
			try
			{
				stream.Close();
				stream.Dispose();
			}
			catch { }

			try
			{
				client.Client.Shutdown(SocketShutdown.Both);
				client.Client.Dispose();
			}
			catch { }

			try
			{
				client.Close();
			}
			catch
			{
			}
		}
	}
}
using System;
using System.Runtime.Serialization;
using System.Security.Authentication;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore
{
	public class WsMessageFilter : ISoapMessageFilter
	{
		private static string _username;
		private static string _password;
		private readonly string _authMissingErrorMessage = "Referenced security token could not be retrieved";
		private readonly string _authInvalidErrorMessage = "Authentication error: Authentication failed: the supplied credential is not right";

		public WsMessageFilter(string username, string password)
		{
			_username = username;
			_password = password;
		}

		public WsMessageFilter(string username, string password, string authMissingErrorMessage, string authInvalidErrorMessage)
		{
			_username = username;
			_password = password;
			_authMissingErrorMessage = authMissingErrorMessage;
			_authInvalidErrorMessage = authInvalidErrorMessage;
		}

		public Task OnMessageReceived(MessageFilterExecutingContext requestContext, MessageFilterExecutionDelegate next)
		{
			WsUsernameToken wsUsernameToken;
			try
			{
				wsUsernameToken = GetWsUsernameToken(requestContext.Message);
			}
			catch (Exception)
			{
				throw new AuthenticationException(_authMissingErrorMessage);
			}

			if (!ValidateWsUsernameToken(wsUsernameToken))
			{
				throw new InvalidCredentialException(_authInvalidErrorMessage);
			}

			return next();
		}

		private WsUsernameToken GetWsUsernameToken(Message message)
		{
			WsUsernameToken wsUsernameToken = null;
			for (var i = 0; i < message.Headers.Count; i++)
			{
				if (message.Headers[i].Name.ToLower() == "security")
				{
					var reader = message.Headers.GetReaderAtHeader(i);
					reader.Read();
					DataContractSerializer serializer = new DataContractSerializer(typeof(WsUsernameToken));
					wsUsernameToken = (WsUsernameToken)serializer.ReadObject(reader, true);
					reader.Close();
				}
			}

			if (wsUsernameToken == null)
			{
				throw new Exception();
			}

			return wsUsernameToken;
		}

		private bool ValidateWsUsernameToken(WsUsernameToken wsUsernameToken)
		{
			return wsUsernameToken.Username == _username && wsUsernameToken.Password == _password;
		}
	}
}

﻿//-----------------------------------------------------------------------
// <copyright file="AccessRequestBindingElement.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace DotNetOpenAuth.OAuthWrap.ChannelElements {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using DotNetOpenAuth.Messaging;
	using DotNetOpenAuth.Messaging.Bindings;

	/// <summary>
	/// Decodes verification codes, refresh tokens and access tokens on incoming messages.
	/// </summary>
	/// <remarks>
	/// This binding element also ensures that the code/token coming in is issued to
	/// the same client that is sending the code/token and that the authorization has
	/// not been revoked and that an access token has not expired.
	/// </remarks>
	internal class AccessRequestBindingElement : AuthServerBindingElementBase {
		/// <summary>
		/// Initializes a new instance of the <see cref="AccessRequestBindingElement"/> class.
		/// </summary>
		internal AccessRequestBindingElement() {
		}

		/// <summary>
		/// Gets the protection commonly offered (if any) by this binding element.
		/// </summary>
		/// <value></value>
		/// <remarks>
		/// This value is used to assist in sorting binding elements in the channel stack.
		/// </remarks>
		public override MessageProtections Protection {
			get { return MessageProtections.None; }
		}

		/// <summary>
		/// Prepares a message for sending based on the rules of this channel binding element.
		/// </summary>
		/// <param name="message">The message to prepare for sending.</param>
		/// <returns>
		/// The protections (if any) that this binding element applied to the message.
		/// Null if this binding element did not even apply to this binding element.
		/// </returns>
		/// <remarks>
		/// Implementations that provide message protection must honor the
		/// <see cref="MessagePartAttribute.RequiredProtection"/> properties where applicable.
		/// </remarks>
		public override MessageProtections? ProcessOutgoingMessage(IProtocolMessage message) {
			var tokenRequest = message as ITokenCarryingRequest;
			if (tokenRequest != null) {
				var tokenBag = (AuthorizationDataBag)tokenRequest.AuthorizationDescription;
				tokenRequest.CodeOrToken = tokenBag.Encode();

				return MessageProtections.None;
			}

			return null;
		}

		/// <summary>
		/// Performs any transformation on an incoming message that may be necessary and/or
		/// validates an incoming message based on the rules of this channel binding element.
		/// </summary>
		/// <param name="message">The incoming message to process.</param>
		/// <returns>
		/// The protections (if any) that this binding element applied to the message.
		/// Null if this binding element did not even apply to this binding element.
		/// </returns>
		/// <exception cref="ProtocolException">
		/// Thrown when the binding element rules indicate that this message is invalid and should
		/// NOT be processed.
		/// </exception>
		/// <remarks>
		/// Implementations that provide message protection must honor the
		/// <see cref="MessagePartAttribute.RequiredProtection"/> properties where applicable.
		/// </remarks>
		public override MessageProtections? ProcessIncomingMessage(IProtocolMessage message) {
			var tokenRequest = message as ITokenCarryingRequest;
			if (tokenRequest != null) {
				try {
					switch (tokenRequest.CodeOrTokenType) {
						case CodeOrTokenType.VerificationCode:
							tokenRequest.AuthorizationDescription = VerificationCode.Decode(this.AuthorizationServer.Secret, this.AuthorizationServer.VerificationCodeNonceStore, tokenRequest.CodeOrToken, message);
							break;
						case CodeOrTokenType.RefreshToken:
							tokenRequest.AuthorizationDescription = RefreshToken.Decode(this.AuthorizationServer.Secret, tokenRequest.CodeOrToken, message);
							break;
						default:
							throw ErrorUtilities.ThrowInternal("Unexpected value for CodeOrTokenType: " + tokenRequest.CodeOrTokenType);
					}
				} catch (ExpiredMessageException ex) {
					throw ErrorUtilities.Wrap(ex, Protocol.authorization_expired);
				}

				var accessRequest = tokenRequest as IAccessTokenRequest;
				if (accessRequest != null) {
					// Make sure the client sending us this token is the client we issued the token to.
					ErrorUtilities.VerifyProtocol(string.Equals(accessRequest.ClientIdentifier, tokenRequest.AuthorizationDescription.ClientIdentifier, StringComparison.Ordinal), Protocol.incorrect_client_credentials);

					// Check that the client secret is correct.
					var client = this.AuthorizationServer.GetClientOrThrow(accessRequest.ClientIdentifier);
					ErrorUtilities.VerifyProtocol(string.Equals(client.Secret, accessRequest.ClientSecret, StringComparison.Ordinal), Protocol.incorrect_client_credentials);
				}

				// Make sure the authorization this token represents hasn't already been revoked.
				ErrorUtilities.VerifyProtocol(this.AuthorizationServer.IsAuthorizationValid(tokenRequest.AuthorizationDescription), Protocol.authorization_expired);

				return MessageProtections.None;
			}

			return null;
		}
	}
}

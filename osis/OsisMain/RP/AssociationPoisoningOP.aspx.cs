﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using DotNetOpenAuth.OpenId.Provider;
using DotNetOpenAuth.Messaging;
using DotNetOpenAuth.OpenId.Messages;
using DotNetOpenAuth.OpenId.ChannelElements;
using DotNetOpenAuth.OpenId;

public partial class RP_AssociationPoisoningOP : System.Web.UI.Page {
	private const string AssociationHandle = "POISONHANDLE";
	private static readonly Uri VictimEndpoint = new Uri("http://test-id.org/rp/AssociationPoisoningOP.aspx");

	protected void Page_Load(object sender, EventArgs e) {
		// Initialize the OP endpoint with a unique store based on the endpoint URL
		bool victimOP = Request.Url == VictimEndpoint;
		OpenIdProvider op = new OpenIdProvider(GetProviderStore(Request.Url));

		IDirectedProtocolMessage requestMessage = op.Channel.ReadFromRequest();

		// Customize handling of association requests such that the association
		// handle is always the same.
		var associateRequest = requestMessage as AssociateRequest;
		if (associateRequest != null) {
			var associateResponse = (AssociateSuccessfulResponse)associateRequest.CreateResponseCore();

			// Create an association and override the handle so it matches between the two OP endpoints.
			Association association = associateResponse.CreateAssociation(associateRequest, op.SecuritySettings);
			association.Handle = AssociationHandle;
			associateResponse.AssociationHandle = AssociationHandle;
			op.AssociationStore.StoreAssociation(AssociationRelyingPartyType.Smart, association);
			op.Channel.Send(associateResponse);
		}

		// Customize responses to authentication requests by always asserting the same
		// identity, regardless of the requested one.  It should be an identity that
		// can only be asserted by the 'victim' OP.
		var authReq = requestMessage as CheckIdRequest;
		if (authReq != null) {
			if (authReq.AssociationHandle == null) {
				// Test is INVALID because the RP is using dumb/stateless mode.
				Response.Redirect("~/RP/AssociationPoisoning.aspx?stateless=1");
			}
			if (authReq.AssociationHandle != AssociationHandle) {
				// Somehow the RP is requesting authentication with something other than the handle
				// we always dish out!
				throw new ArgumentException("RP is requesting authentication with unexpected association handle " + authReq.AssociationHandle);
			}
			var authResp = new PositiveAssertionResponse(authReq);
			authResp.ClaimedIdentifier = new Uri(Request.Url, Page.ResolveUrl("~/RP/AssociationPoisoning.aspx?test=1"));
			authResp.LocalIdentifier = authResp.ClaimedIdentifier;
			authResp.ProviderEndpoint = VictimEndpoint;
			op.Channel.Send(authResp);
		}

		var checkAuthReq = requestMessage as CheckAuthenticationRequest;
		if (checkAuthReq != null) {
			DirectErrorResponse response = new DirectErrorResponse(Protocol.V20.Version, checkAuthReq);
			response.ErrorMessage = "check_auth message not expected in this test scenario.";
			op.Channel.Send(response);
		}
	}

	private IProviderApplicationStore GetProviderStore(Uri requestUrl) {
		UriBuilder opEndpoint = new UriBuilder(requestUrl);
		opEndpoint.Query = null;
		opEndpoint.Fragment = null;
		string key = "OPAppStore" + opEndpoint.Uri.AbsoluteUri;
		IProviderApplicationStore store;
		Application.Lock();
		try {
			store = Application[key] as IProviderApplicationStore;
			if (store == null) {
				Application[key] = store = new StandardProviderApplicationStore();
			}
		} finally {
			Application.UnLock();
		}

		return store;
	}
}
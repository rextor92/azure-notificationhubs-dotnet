// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for
// license information.

using AppBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.NotificationHubs.Messaging;
using System.Net;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace AppBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RegisterController : ControllerBase
    {
        private readonly NotificationHubClient _hub;

        public RegisterController()
        {
            _hub = Notifications.Instance.Hub;
        }

        public record DeviceRegistration(string Platform, string Handle, string[] Tags);

        // POST api/register
        // This creates a registration id
        [HttpPost]
        public async Task<ActionResult<string>> Post(string handle = null)
        {
            string newRegistrationId = null;

            // make sure there are no existing registrations for this push handle (used for iOS and Android)
            if (!string.IsNullOrWhiteSpace(handle))
            {
                var registrations = await _hub.GetRegistrationsByChannelAsync(handle, 100);

                foreach (var registration in registrations)
                {
                    if (newRegistrationId == null)
                    {
                        newRegistrationId = registration.RegistrationId;
                    }
                    else
                    {
                        await _hub.DeleteRegistrationAsync(registration);
                    }
                }
            }

            newRegistrationId ??= await _hub.CreateRegistrationIdAsync();

            return Ok(newRegistrationId);
        }

        // PUT api/register/5
        // This creates or updates a registration (with provided channelURI) at the specified id
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(string id, DeviceRegistration deviceUpdate)
        {
            RegistrationDescription registration;
            switch (deviceUpdate.Platform.ToLowerInvariant())
            {
                case "wns":
                    registration = new WindowsRegistrationDescription(deviceUpdate.Handle);
                    break;

                case "apns":
                    registration = new AppleRegistrationDescription(deviceUpdate.Handle);
                    break;

                case "fcmv1":
                    registration = new FcmV1RegistrationDescription(deviceUpdate.Handle);
                    break;

                default:
                    return BadRequest("Unsupported platform.");
            }

            registration.RegistrationId = id;
            var username = HttpContext.User.Identity.Name;

            registration.Tags = new HashSet<string>(deviceUpdate.Tags)
            {
                "username:" + username
            };

            try
            {
                await _hub.CreateOrUpdateRegistrationAsync(registration);
            }
            catch (MessagingException e)
            {
                if (e.IsTransient)
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Transient fault detected, try again later.");
                }
                ReturnGoneIfHubResponseIsGone(e);
            }

            return Ok();
        }

        // DELETE api/register/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _hub.DeleteRegistrationAsync(id);
            return Ok();
        }

        private static void ReturnGoneIfHubResponseIsGone(MessagingException e)
        {
            if (e.InnerException is WebException webex
                && webex.Status == WebExceptionStatus.ProtocolError)
            {
                if (webex.Response is HttpWebResponse response && response.StatusCode == HttpStatusCode.Gone)
                {
                    throw new HttpRequestException(HttpStatusCode.Gone.ToString());
                }
            }
        }
    }
}

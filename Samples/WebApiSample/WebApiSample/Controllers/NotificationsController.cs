// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for
// license information.

using AppBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.NotificationHubs;
using System.Net;

namespace AppBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Post([FromQuery] string pns, [FromBody] string message, [FromQuery] string to_tag)
        {
            var user = HttpContext.User.Identity.Name;
            var userTag = new[]
            {
                $"username:{to_tag}",
                $"from:{user}"
            };

            NotificationOutcome outcome = null;
            var ret = HttpStatusCode.InternalServerError;

            switch (pns.ToLower())
            {
                case "wns":
                    // Windows 8.1 / Windows Phone 8.1
                    var toast = $@"<toast><visual><binding template=""ToastText01""><text id=""1"">From {user}: {message}</text></binding></visual></toast>";
                    outcome = await Notifications.Instance.Hub.SendWindowsNativeNotificationAsync(toast, userTag);
                    break;

                case "apns":
                    // iOS
                    var alert = "{\"aps\":{\"alert\":\"" + "From " + user + ": " + message + "\"}}";
                    outcome = await Notifications.Instance.Hub.SendAppleNativeNotificationAsync(alert, userTag);
                    break;

                case "fcmv1":
                    // Android
                    var notif = "{ \"message\":{\"data\" : {\"message\":\"" + "From " + user + ": " + message + "\"}}}";
                    outcome = await Notifications.Instance.Hub.SendFcmV1NativeNotificationAsync(notif, userTag);
                    break;
            }

            if (outcome != null
                && outcome.State != NotificationOutcomeState.Abandoned
                && outcome.State != NotificationOutcomeState.Unknown)
            {
                ret = HttpStatusCode.OK;
            }

            return StatusCode((int)ret);
        }
    }
}
